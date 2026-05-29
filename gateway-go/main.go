package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"net/http/httputil"
	"net/url"
	"os"
	"os/signal"
	"strconv"
	"strings"
	"sync"
	"sync/atomic"
	"syscall"
	"time"

	"github.com/golang-jwt/jwt/v5"
	"github.com/gorilla/mux"
	"github.com/gorilla/websocket"
	cache "github.com/patrickmn/go-cache"
	"github.com/rs/cors"
	"golang.org/x/crypto/bcrypt"
	"golang.org/x/time/rate"
	_ "modernc.org/sqlite"
)

// Configuration
var (
	Port      = getEnv("PORT", "8887")
	JWTSecret = []byte(getEnv("JWT_SECRET", "GatewaySecretKey-Change-This-In-Production-Min32Chars!"))

	// Token lifetimes (overridable via env, in minutes for access / hours for refresh)
	AccessTokenTTL  = time.Duration(getEnvInt("ACCESS_TOKEN_TTL_MIN", 15)) * time.Minute
	RefreshTokenTTL = time.Duration(getEnvInt("REFRESH_TOKEN_TTL_HOURS", 7*24)) * time.Hour

	// Request-log retention. Logs older than LogRetentionDays are purged
	// automatically every LogCleanupIntervalHours. Set LogRetentionDays<=0 to
	// disable automatic cleanup.
	LogRetentionDays        = getEnvInt("LOG_RETENTION_DAYS", 7)
	LogCleanupIntervalHours = getEnvInt("LOG_CLEANUP_INTERVAL_HOURS", 6)
)

// Models
type User struct {
	ID                  int64
	Username            string
	PasswordHash        string
	Role                string
	IsActive            bool
	FailedLoginAttempts int
	LockedUntil         *time.Time
}

type Route struct {
	ID                            int64
	RouteID                       string
	ClusterID                     string
	MatchPath                     string
	RateLimitPerSecond            int
	IsActive                      bool
	Methods                       string
	CircuitBreakerThreshold       int
	CircuitBreakerDurationSeconds int
	IpWhitelist                   string
	IpBlacklist                   string
	CacheTtlSeconds               int
	TransformsJson                string
}

type Cluster struct {
	ID                         int64
	ClusterID                  string
	DestinationsJSON           string
	LoadBalancingPolicy        string
	IsActive                   bool
	EnableHealthCheck          int
	HealthCheckPath            string
	HealthCheckIntervalSeconds int
	HealthCheckTimeoutSeconds  int
	RetryCount                 int
	RetryDelayMs               int
}

type Destination struct {
	ID      string `json:"id"`
	Address string `json:"address"`
	Health  string `json:"health"`
}

// Metrics
type RouteMetrics struct {
	TotalRequests       int64   `json:"totalRequests"`
	SuccessCount        int64   `json:"successCount"`
	ErrorCount          int64   `json:"errorCount"`
	TotalLatencyMs      int64   `json:"-"`
	AvgLatencyMs        int     `json:"avgLatencyMs"`
	MaxLatencyMs        int     `json:"maxLatencyMs"`
	ThroughputPerSecond float64 `json:"throughputPerSecond"`
	ErrorRate           float64 `json:"errorRate"`
	UptimeSeconds       int64   `json:"uptimeSeconds"`
}

// routeStat holds lock-free atomic counters updated on the request hot path.
// Derived values (avg, throughput, error rate) are computed at read time.
type routeStat struct {
	total        int64
	success      int64
	errCount     int64
	totalLatency int64
	maxLatency   int64
}

type GlobalMetrics struct {
	WSConnections int64
	WSMessages    int64
	StartTime     time.Time
}

var globalMetrics = &GlobalMetrics{StartTime: time.Now()}
var routeMetricsMap sync.Map // map[string]*routeStat

// Logs
type LogEntry struct {
	Timestamp  time.Time
	Method     string
	Path       string
	StatusCode int
	LatencyMs  int
	ClientIp   string
	RouteId    string
}

var logQueue = make(chan LogEntry, 2000)
var logWorkerStarted = false

func logWorker() {
	for entry := range logQueue {
		if db == nil {
			continue
		}
		_, err := db.Exec("INSERT INTO RequestLogs (Timestamp, Method, Path, StatusCode, LatencyMs, ClientIp, RouteId) VALUES (?, ?, ?, ?, ?, ?, ?)",
			entry.Timestamp.Format(time.RFC3339),
			entry.Method,
			entry.Path,
			entry.StatusCode,
			entry.LatencyMs,
			entry.ClientIp,
			entry.RouteId,
		)
		if err != nil {
			log.Printf("⚠️ Failed to write log to DB: %v", err)
		}
	}
}

// purgeOldLogs deletes request logs older than LogRetentionDays. Returns the
// number of rows removed.
func purgeOldLogs() (int64, error) {
	if db == nil || LogRetentionDays <= 0 {
		return 0, nil
	}
	cutoff := time.Now().AddDate(0, 0, -LogRetentionDays).Format(time.RFC3339)
	res, err := db.Exec("DELETE FROM RequestLogs WHERE Timestamp < ?", cutoff)
	if err != nil {
		return 0, err
	}
	rows, _ := res.RowsAffected()
	return rows, nil
}

// logRetentionWorker periodically purges old request logs so the DB stays light.
// Disabled when LogRetentionDays<=0.
func logRetentionWorker() {
	if LogRetentionDays <= 0 {
		log.Println("ℹ️ Log auto-cleanup disabled (LOG_RETENTION_DAYS<=0)")
		return
	}
	interval := time.Duration(LogCleanupIntervalHours) * time.Hour
	if interval <= 0 {
		interval = 6 * time.Hour
	}

	log.Printf("🧹 Log auto-cleanup enabled: keep %d day(s), run every %v\n", LogRetentionDays, interval)

	// Run once shortly after startup, then on a ticker.
	if n, err := purgeOldLogs(); err != nil {
		log.Printf("⚠️ Log cleanup failed: %v", err)
	} else if n > 0 {
		log.Printf("🧹 Log cleanup: removed %d old log entries", n)
	}

	ticker := time.NewTicker(interval)
	defer ticker.Stop()
	for range ticker.C {
		if n, err := purgeOldLogs(); err != nil {
			log.Printf("⚠️ Log cleanup failed: %v", err)
		} else if n > 0 {
			log.Printf("🧹 Log cleanup: removed %d old log entries", n)
		}
	}
}

// Database
var db *sql.DB

// Cache (L1 - In-Memory)
var (
	routeCache   *cache.Cache
	clusterCache *cache.Cache
	userCache    *cache.Cache
)

// Rate limiters
var rateLimiters sync.Map

// Shared, tuned HTTP transport for all upstream proxying. Reusing one transport
// keeps the connection pool warm instead of relying on per-request defaults.
var proxyTransport = &http.Transport{
	Proxy:                 http.ProxyFromEnvironment,
	MaxIdleConns:          1000,
	MaxIdleConnsPerHost:   200,
	IdleConnTimeout:       90 * time.Second,
	TLSHandshakeTimeout:   10 * time.Second,
	ExpectContinueTimeout: 1 * time.Second,
	ForceAttemptHTTP2:     true,
}

// proxyCache holds one *httputil.ReverseProxy per upstream target so we don't
// allocate a fresh proxy on every request (a hot-path bottleneck).
var proxyCache sync.Map // map[string]*httputil.ReverseProxy

// getReverseProxy returns a cached reverse proxy for the given target URL,
// creating and storing one on first use.
func getReverseProxy(target *url.URL) *httputil.ReverseProxy {
	key := target.Scheme + "://" + target.Host
	if p, ok := proxyCache.Load(key); ok {
		return p.(*httputil.ReverseProxy)
	}
	proxy := httputil.NewSingleHostReverseProxy(target)
	proxy.Transport = proxyTransport
	proxy.ErrorHandler = func(w http.ResponseWriter, r *http.Request, err error) {
		respondJSON(w, http.StatusBadGateway, map[string]string{"error": "Upstream request failed"})
	}
	actual, _ := proxyCache.LoadOrStore(key, proxy)
	return actual.(*httputil.ReverseProxy)
}

// WebSocket upgrader
var upgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool {
		return true
	},
}

func main() {
	// Initialize logging to file
	logFile, err := os.OpenFile("gateway.log", os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0666)
	if err == nil {
		multiWriter := io.MultiWriter(os.Stdout, logFile)
		log.SetOutput(multiWriter)
		log.Println("✅ Log file initialized: gateway.log")
	} else {
		log.Println("⚠️ Failed to initialize log file, using default stdout")
	}

	// Start log worker
	if !logWorkerStarted {
		go logWorker()
		logWorkerStarted = true
	}

	// Initialize cache
	initCache()

	// Initialize database
	initDB()
	defer db.Close()

	// Start background log retention/cleanup worker
	go logRetentionWorker()

	// Create router
	r := mux.NewRouter()

	// Middleware
	r.Use(loggingMiddleware)

	// Health check
	r.HandleFunc("/health", healthHandler).Methods("GET")

	// Auth endpoints
	r.HandleFunc("/auth/login", loginHandler).Methods("POST")
	r.HandleFunc("/auth/refresh", refreshHandler).Methods("POST")
	r.HandleFunc("/auth/validate", validateHandler).Methods("POST")
	r.Handle("/auth/logout", authMiddleware(http.HandlerFunc(logoutHandler))).Methods("POST")

	// Admin endpoints (require auth)
	admin := r.PathPrefix("/admin").Subrouter()
	admin.Use(authMiddleware)

	admin.HandleFunc("/users", getUsersHandler).Methods("GET")
	admin.HandleFunc("/users", createUserHandler).Methods("POST")
	admin.HandleFunc("/users/{id}", updateUserHandler).Methods("PUT")
	admin.HandleFunc("/users/{id}", deleteUserHandler).Methods("DELETE")

	admin.HandleFunc("/routes", getRoutesHandler).Methods("GET")
	admin.HandleFunc("/routes", createRouteHandler).Methods("POST")
	admin.HandleFunc("/routes/{id}", updateRouteHandler).Methods("PUT")
	admin.HandleFunc("/routes/{id}", deleteRouteHandler).Methods("DELETE")

	admin.HandleFunc("/clusters", getClustersHandler).Methods("GET")
	admin.HandleFunc("/clusters", createClusterHandler).Methods("POST")
	admin.HandleFunc("/clusters/{id}", updateClusterHandler).Methods("PUT")
	admin.HandleFunc("/clusters/{id}", deleteClusterHandler).Methods("DELETE")

	admin.HandleFunc("/metrics", metricsHandler).Methods("GET")
	admin.HandleFunc("/metrics", resetMetricsHandler).Methods("DELETE")
	admin.HandleFunc("/stats", statsHandler).Methods("GET")
	admin.HandleFunc("/health", healthHandler).Methods("GET")
	admin.HandleFunc("/logs", getLogsHandler).Methods("GET")
	admin.HandleFunc("/logs", clearLogsHandler).Methods("DELETE")
	admin.HandleFunc("/logs/stats", getLogStatsHandler).Methods("GET")

	// Setup proxy routes
	setupProxyRoutes(r)

	// CORS
	handler := cors.New(cors.Options{
		AllowedOrigins:   []string{"*"},
		AllowedMethods:   []string{"GET", "POST", "PUT", "DELETE", "OPTIONS"},
		AllowedHeaders:   []string{"*"},
		AllowCredentials: true,
	}).Handler(r)

	// Start server
	srv := &http.Server{
		Addr:    "0.0.0.0:" + Port,
		Handler: handler,
		// ReadHeaderTimeout guards against slowloris while allowing slow bodies.
		ReadHeaderTimeout: 15 * time.Second,
		// WriteTimeout is intentionally 0: this is a proxy that supports
		// WebSocket upgrades and long/streaming upstream responses, which a
		// fixed write deadline would abruptly cut off.
		WriteTimeout: 0,
		IdleTimeout:  120 * time.Second,
	}

	// Graceful shutdown
	go func() {
		sigint := make(chan os.Signal, 1)
		signal.Notify(sigint, os.Interrupt, syscall.SIGTERM)
		<-sigint

		log.Println("\nShutting down gracefully...")
		ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()

		if err := srv.Shutdown(ctx); err != nil {
			log.Printf("Server shutdown error: %v\n", err)
		}
	}()

	log.Printf("\n🚀 Go API Gateway running on http://0.0.0.0:%s\n", Port)
	log.Printf("📊 Admin API: http://0.0.0.0:%s/admin\n", Port)
	log.Printf("🔐 Login: POST /auth/login\n")
	log.Printf("\nDefault credentials: admin / admin123\n\n")

	if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatalf("Server error: %v\n", err)
	}
}

// Cache initialization
func initCache() {
	// L1 Cache: In-Memory with TTL
	routeCache = cache.New(5*time.Minute, 10*time.Minute)  // Routes: 5min TTL
	clusterCache = cache.New(1*time.Minute, 2*time.Minute) // Clusters: 1min TTL
	userCache = cache.New(2*time.Minute, 5*time.Minute)    // Users: 2min TTL

	log.Println("✅ Cache initialized (L1 In-Memory)")
}

// Database initialization
func initDB() {
	var err error
	db, err = sql.Open("sqlite", "./gateway.db?_pragma=journal_mode(WAL)&_pragma=busy_timeout(5000)")
	if err != nil {
		log.Fatal(err)
	}

	// Create tables
	createTables()

	db.Exec("ALTER TABLE Routes ADD COLUMN Methods TEXT")
	db.Exec("ALTER TABLE Routes ADD COLUMN CircuitBreakerThreshold INTEGER DEFAULT 0")
	db.Exec("ALTER TABLE Routes ADD COLUMN CircuitBreakerDurationSeconds INTEGER DEFAULT 30")
	db.Exec("ALTER TABLE Routes ADD COLUMN IpWhitelist TEXT")
	db.Exec("ALTER TABLE Routes ADD COLUMN IpBlacklist TEXT")
	db.Exec("ALTER TABLE Routes ADD COLUMN CacheTtlSeconds INTEGER DEFAULT 0")
	db.Exec("ALTER TABLE Routes ADD COLUMN TransformsJson TEXT")

	db.Exec("ALTER TABLE Clusters ADD COLUMN EnableHealthCheck INTEGER DEFAULT 1")
	db.Exec("ALTER TABLE Clusters ADD COLUMN HealthCheckPath TEXT DEFAULT '/health'")
	db.Exec("ALTER TABLE Clusters ADD COLUMN HealthCheckIntervalSeconds INTEGER DEFAULT 10")
	db.Exec("ALTER TABLE Clusters ADD COLUMN HealthCheckTimeoutSeconds INTEGER DEFAULT 5")
	db.Exec("ALTER TABLE Clusters ADD COLUMN RetryCount INTEGER DEFAULT 0")
	db.Exec("ALTER TABLE Clusters ADD COLUMN RetryDelayMs INTEGER DEFAULT 1000")

	// Seed data
	seedData()

	log.Println("✅ Database initialized")
}

func createTables() {
	queries := []string{
		`CREATE TABLE IF NOT EXISTS Users (
			Id INTEGER PRIMARY KEY AUTOINCREMENT,
			Username TEXT UNIQUE NOT NULL,
			PasswordHash TEXT NOT NULL,
			Role TEXT NOT NULL,
			IsActive INTEGER DEFAULT 1,
			FailedLoginAttempts INTEGER DEFAULT 0,
			LockedUntil TEXT,
			CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
		)`,
		`CREATE TABLE IF NOT EXISTS RequestLogs (
			Id INTEGER PRIMARY KEY AUTOINCREMENT,
			Timestamp TEXT,
			Method TEXT,
			Path TEXT,
			StatusCode INTEGER,
			LatencyMs INTEGER,
			ClientIp TEXT,
			RouteId TEXT
		)`,
		`CREATE INDEX IF NOT EXISTS idx_requestlogs_timestamp ON RequestLogs(Timestamp)`,
		`CREATE TABLE IF NOT EXISTS Routes (
			Id INTEGER PRIMARY KEY AUTOINCREMENT,
			RouteId TEXT UNIQUE NOT NULL,
			ClusterId TEXT NOT NULL,
			MatchPath TEXT NOT NULL,
			RateLimitPerSecond INTEGER DEFAULT 0,
			IsActive INTEGER DEFAULT 1,
			CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
		)`,
		`CREATE TABLE IF NOT EXISTS Clusters (
			Id INTEGER PRIMARY KEY AUTOINCREMENT,
			ClusterId TEXT UNIQUE NOT NULL,
			DestinationsJson TEXT NOT NULL,
			LoadBalancingPolicy TEXT DEFAULT 'RoundRobin',
			IsActive INTEGER DEFAULT 1,
			CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
		)`,
	}

	for _, query := range queries {
		if _, err := db.Exec(query); err != nil {
			log.Fatal(err)
		}
	}
}

func seedData() {
	// Seed admin user
	var count int
	db.QueryRow("SELECT COUNT(*) FROM Users WHERE Username = ?", "admin").Scan(&count)
	if count == 0 {
		hash, _ := bcrypt.GenerateFromPassword([]byte("admin123"), bcrypt.DefaultCost)
		db.Exec("INSERT INTO Users (Username, PasswordHash, Role) VALUES (?, ?, ?)",
			"admin", string(hash), "Admin")
		log.Println("✅ Admin user created: admin/admin123")
	}

	// Seed default cluster
	db.QueryRow("SELECT COUNT(*) FROM Clusters WHERE ClusterId = ?", "test-cluster").Scan(&count)
	if count == 0 {
		destinations := `[{"id":"backend-1","address":"http://localhost:5001","health":"Active"}]`
		db.Exec("INSERT INTO Clusters (ClusterId, DestinationsJson) VALUES (?, ?)",
			"test-cluster", destinations)
	}

	// Seed default routes
	db.QueryRow("SELECT COUNT(*) FROM Routes WHERE RouteId = ?", "test-route").Scan(&count)
	if count == 0 {
		db.Exec("INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond) VALUES (?, ?, ?, ?)",
			"test-route", "test-cluster", "/test", 0)
		db.Exec("INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond) VALUES (?, ?, ?, ?)",
			"api-route", "test-cluster", "/api", 100)
	}
}

// Middleware
func loggingMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		originalPath := r.URL.Path

		wrapped := &responseWriter{ResponseWriter: w, statusCode: http.StatusOK}

		clientIp := r.Header.Get("X-Forwarded-For")
		if clientIp == "" {
			clientIp = r.RemoteAddr
			if strings.Contains(clientIp, ":") {
				clientIp = strings.Split(clientIp, ":")[0]
			}
		}

		next.ServeHTTP(wrapped, r)

		latency := time.Since(start).Milliseconds()
		routeID := r.Header.Get("X-Gateway-RouteId")
		if routeID == "" {
			routeID = "Unknown"
		}

		select {
		case logQueue <- LogEntry{
			Timestamp:  time.Now(),
			Method:     r.Method,
			Path:       originalPath,
			StatusCode: wrapped.statusCode,
			LatencyMs:  int(latency),
			ClientIp:   clientIp,
			RouteId:    routeID,
		}:
		default:
		}

		// Lock-free metrics update on the hot path.
		var st *routeStat
		if v, ok := routeMetricsMap.Load(routeID); ok {
			st = v.(*routeStat)
		} else {
			actual, _ := routeMetricsMap.LoadOrStore(routeID, &routeStat{})
			st = actual.(*routeStat)
		}

		atomic.AddInt64(&st.total, 1)
		atomic.AddInt64(&st.totalLatency, latency)
		if wrapped.statusCode < 400 {
			atomic.AddInt64(&st.success, 1)
		} else {
			atomic.AddInt64(&st.errCount, 1)
		}
		// Lock-free max update via compare-and-swap.
		for {
			cur := atomic.LoadInt64(&st.maxLatency)
			if latency <= cur || atomic.CompareAndSwapInt64(&st.maxLatency, cur, latency) {
				break
			}
		}
	})
}

type responseWriter struct {
	http.ResponseWriter
	statusCode int
}

func (rw *responseWriter) WriteHeader(code int) {
	rw.statusCode = code
	rw.ResponseWriter.WriteHeader(code)
}

func authMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		authHeader := r.Header.Get("Authorization")
		if authHeader == "" {
			respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "Access token required"})
			return
		}

		const prefix = "Bearer "
		if len(authHeader) <= len(prefix) || !strings.EqualFold(authHeader[:len(prefix)], prefix) {
			respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "Invalid authorization header"})
			return
		}
		tokenString := authHeader[len(prefix):]

		claims, err := parseToken(tokenString)
		if err != nil {
			// 401 (not 403) so clients know to refresh / re-authenticate
			respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "Invalid or expired token"})
			return
		}

		// Reject refresh tokens used as access tokens on protected routes
		if typ, _ := claims["typ"].(string); typ == "refresh" {
			respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "Invalid token type"})
			return
		}

		next.ServeHTTP(w, r)
	})
}

// Handlers
func healthHandler(w http.ResponseWriter, r *http.Request) {
	var totalRoutes, activeRoutes int
	db.QueryRow("SELECT COUNT(*) FROM Routes").Scan(&totalRoutes)
	db.QueryRow("SELECT COUNT(*) FROM Routes WHERE IsActive = 1").Scan(&activeRoutes)

	var totalClusters int
	db.QueryRow("SELECT COUNT(*) FROM Clusters").Scan(&totalClusters)

	type healthDest struct {
		ClusterId                  string `json:"clusterId"`
		Address                    string `json:"address"`
		Role                       string `json:"role"`
		HealthCheck                string `json:"healthCheck"`
		HealthCheckPath            string `json:"healthCheckPath"`
		HealthCheckIntervalSeconds int    `json:"healthCheckIntervalSeconds"`
	}
	var destinations []healthDest

	rows, err := db.Query("SELECT ClusterId, DestinationsJson, EnableHealthCheck, HealthCheckPath, HealthCheckIntervalSeconds FROM Clusters")
	if err == nil {
		defer rows.Close()
		for rows.Next() {
			var cid, djson string
			var hpath string = "/health"
			var eh, hint int = 1, 10

			// Need nullable scanning for newly added columns
			var niEh, niHint sql.NullInt64
			var nsHpath sql.NullString

			rows.Scan(&cid, &djson, &niEh, &nsHpath, &niHint)

			if niEh.Valid {
				eh = int(niEh.Int64)
			}
			if nsHpath.Valid {
				hpath = nsHpath.String
			}
			if niHint.Valid {
				hint = int(niHint.Int64)
			}

			var dests []Destination
			json.Unmarshal([]byte(djson), &dests)
			for _, d := range dests {
				healthCheckStr := "Disabled"
				if eh == 1 {
					healthCheckStr = "Enabled"
				}
				destinations = append(destinations, healthDest{
					ClusterId:                  cid,
					Address:                    d.Address,
					Role:                       d.Health,
					HealthCheck:                healthCheckStr,
					HealthCheckPath:            hpath,
					HealthCheckIntervalSeconds: hint,
				})
			}
		}
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"status":        "ok",
		"timestamp":     time.Now().Format(time.RFC3339),
		"uptime":        int(time.Since(globalMetrics.StartTime).Seconds()),
		"wsConnections": atomic.LoadInt64(&globalMetrics.WSConnections),
		"gateway": map[string]interface{}{
			"totalRoutes":       totalRoutes,
			"totalClusters":     totalClusters,
			"activeProxyRoutes": activeRoutes,
		},
		"destinations": destinations,
	})
}

func loginHandler(w http.ResponseWriter, r *http.Request) {
	var req struct {
		Username string `json:"username"`
		Password string `json:"password"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request"})
		return
	}

	// Check cache first
	cacheKey := "user:" + req.Username
	if cached, found := userCache.Get(cacheKey); found {
		user := cached.(User)

		// Check if account is locked
		if user.LockedUntil != nil && user.LockedUntil.After(time.Now()) {
			respondJSON(w, http.StatusLocked, map[string]string{"error": "Account locked"})
			return
		}

		if !user.IsActive {
			respondJSON(w, http.StatusForbidden, map[string]string{"error": "Account disabled"})
			return
		}

		// Verify password
		if err := bcrypt.CompareHashAndPassword([]byte(user.PasswordHash), []byte(req.Password)); err != nil {
			// Cache miss on wrong password - invalidate and query DB
			userCache.Delete(cacheKey)
		} else {
			// Password correct - generate tokens
			accessTokenString, refreshTokenString, err := generateTokenPair(user.ID, user.Username, user.Role)
			if err != nil {
				respondJSON(w, http.StatusInternalServerError, map[string]string{"error": "Failed to issue token"})
				return
			}

			respondJSON(w, http.StatusOK, map[string]interface{}{
				"accessToken":  accessTokenString,
				"refreshToken": refreshTokenString,
				"user": map[string]interface{}{
					"id":       user.ID,
					"username": user.Username,
					"role":     user.Role,
				},
			})
			return
		}
	}

	// Cache miss - query database
	var user User
	err := db.QueryRow(`SELECT Id, Username, PasswordHash, Role, IsActive, FailedLoginAttempts, LockedUntil
		FROM Users WHERE Username = ?`, req.Username).Scan(
		&user.ID, &user.Username, &user.PasswordHash, &user.Role,
		&user.IsActive, &user.FailedLoginAttempts, &user.LockedUntil)

	if err != nil {
		respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "Invalid credentials"})
		return
	}

	// Check if account is locked
	if user.LockedUntil != nil && user.LockedUntil.After(time.Now()) {
		respondJSON(w, http.StatusLocked, map[string]string{"error": "Account locked"})
		return
	}

	if !user.IsActive {
		respondJSON(w, http.StatusForbidden, map[string]string{"error": "Account disabled"})
		return
	}

	// Verify password
	if err := bcrypt.CompareHashAndPassword([]byte(user.PasswordHash), []byte(req.Password)); err != nil {
		// Increment failed attempts
		failedAttempts := user.FailedLoginAttempts + 1
		if failedAttempts >= 5 {
			lockedUntil := time.Now().Add(30 * time.Minute)
			db.Exec("UPDATE Users SET FailedLoginAttempts = ?, LockedUntil = ? WHERE Id = ?",
				failedAttempts, lockedUntil, user.ID)
			respondJSON(w, http.StatusLocked, map[string]string{"error": "Account locked"})
		} else {
			db.Exec("UPDATE Users SET FailedLoginAttempts = ? WHERE Id = ?", failedAttempts, user.ID)
			respondJSON(w, http.StatusUnauthorized, map[string]interface{}{
				"error":        "Invalid credentials",
				"attemptsLeft": 5 - failedAttempts,
			})
		}
		return
	}

	// Reset failed attempts
	db.Exec("UPDATE Users SET FailedLoginAttempts = 0, LockedUntil = NULL WHERE Id = ?", user.ID)

	// Cache user for future logins
	userCache.Set(cacheKey, user, cache.DefaultExpiration)

	// Generate tokens
	accessTokenString, refreshTokenString, err := generateTokenPair(user.ID, user.Username, user.Role)
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": "Failed to issue token"})
		return
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"accessToken":  accessTokenString,
		"refreshToken": refreshTokenString,
		"user": map[string]interface{}{
			"id":       user.ID,
			"username": user.Username,
			"role":     user.Role,
		},
	})
}

func refreshHandler(w http.ResponseWriter, r *http.Request) {
	var req struct {
		RefreshToken string `json:"refreshToken"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.RefreshToken == "" {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Refresh token required"})
		return
	}

	claims, err := parseToken(req.RefreshToken)
	if err != nil {
		respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "Invalid or expired refresh token"})
		return
	}

	// Ensure this is actually a refresh token, not an access token being replayed
	if typ, _ := claims["typ"].(string); typ != "refresh" {
		respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "Invalid token type"})
		return
	}

	idFloat, ok := claims["id"].(float64)
	if !ok {
		respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "Invalid token claims"})
		return
	}
	userID := int64(idFloat)

	// Re-load the user to pick up current role / active status (token rotation)
	var user User
	err = db.QueryRow(`SELECT Id, Username, Role, IsActive FROM Users WHERE Id = ?`, userID).Scan(
		&user.ID, &user.Username, &user.Role, &user.IsActive)
	if err != nil {
		respondJSON(w, http.StatusUnauthorized, map[string]string{"error": "User not found"})
		return
	}
	if !user.IsActive {
		respondJSON(w, http.StatusForbidden, map[string]string{"error": "Account disabled"})
		return
	}

	accessTokenString, refreshTokenString, err := generateTokenPair(user.ID, user.Username, user.Role)
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": "Failed to issue token"})
		return
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"accessToken":  accessTokenString,
		"refreshToken": refreshTokenString,
		"user": map[string]interface{}{
			"id":       user.ID,
			"username": user.Username,
			"role":     user.Role,
		},
	})
}

// validateHandler lets the UI check a token on mount and read back its claims.
func validateHandler(w http.ResponseWriter, r *http.Request) {
	var req struct {
		Token string `json:"token"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.Token == "" {
		respondJSON(w, http.StatusOK, map[string]interface{}{"valid": false})
		return
	}

	claims, err := parseToken(req.Token)
	if err != nil {
		respondJSON(w, http.StatusOK, map[string]interface{}{"valid": false})
		return
	}

	// Return claims in a stable, type-tagged shape the UI can map.
	claimList := []map[string]interface{}{}
	if v, ok := claims["id"]; ok {
		claimList = append(claimList, map[string]interface{}{"type": "nameidentifier", "value": fmt.Sprintf("%v", int64(toFloat(v)))})
	}
	if v, ok := claims["username"]; ok {
		claimList = append(claimList, map[string]interface{}{"type": "name", "value": fmt.Sprintf("%v", v)})
	}
	if v, ok := claims["role"]; ok {
		claimList = append(claimList, map[string]interface{}{"type": "role", "value": fmt.Sprintf("%v", v)})
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"valid":  true,
		"claims": claimList,
	})
}

func toFloat(v interface{}) float64 {
	switch n := v.(type) {
	case float64:
		return n
	case int64:
		return float64(n)
	case int:
		return float64(n)
	}
	return 0
}

func logoutHandler(w http.ResponseWriter, r *http.Request) {
	respondJSON(w, http.StatusOK, map[string]bool{"success": true})
}

// User handlers
func getUsersHandler(w http.ResponseWriter, r *http.Request) {
	rows, err := db.Query("SELECT Id, Username, Role, IsActive, CreatedAt FROM Users")
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}
	defer rows.Close()

	var users = make([]map[string]interface{}, 0)
	for rows.Next() {
		var id int64
		var username, role, createdAt string
		var isActive bool
		rows.Scan(&id, &username, &role, &isActive, &createdAt)
		users = append(users, map[string]interface{}{
			"id":        id,
			"username":  username,
			"role":      role,
			"isActive":  isActive,
			"createdAt": createdAt,
		})
	}

	respondJSON(w, http.StatusOK, users)
}

func createUserHandler(w http.ResponseWriter, r *http.Request) {
	var req struct {
		Username string `json:"username"`
		Password string `json:"password"`
		Role     string `json:"role"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request"})
		return
	}

	hash, _ := bcrypt.GenerateFromPassword([]byte(req.Password), bcrypt.DefaultCost)

	result, err := db.Exec("INSERT INTO Users (Username, PasswordHash, Role) VALUES (?, ?, ?)",
		req.Username, string(hash), req.Role)

	if err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}

	id, _ := result.LastInsertId()
	respondJSON(w, http.StatusOK, map[string]interface{}{
		"id":       id,
		"username": req.Username,
		"role":     req.Role,
	})
}

func updateUserHandler(w http.ResponseWriter, r *http.Request) {
	vars := mux.Vars(r)
	id := vars["id"]

	var req struct {
		Username string `json:"username"`
		Password string `json:"password"`
		Role     string `json:"role"`
		IsActive bool   `json:"isActive"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request"})
		return
	}

	// Build update query
	query := "UPDATE Users SET Username = ?, Role = ?, IsActive = ?"
	args := []interface{}{req.Username, req.Role, req.IsActive}

	// Only update password if provided
	if req.Password != "" {
		hash, _ := bcrypt.GenerateFromPassword([]byte(req.Password), bcrypt.DefaultCost)
		query += ", PasswordHash = ?"
		args = append(args, string(hash))
	}

	query += " WHERE Id = ?"
	args = append(args, id)

	result, err := db.Exec(query, args...)
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}

	rowsAffected, _ := result.RowsAffected()
	if rowsAffected == 0 {
		respondJSON(w, http.StatusNotFound, map[string]string{"error": "User not found"})
		return
	}

	// Invalidate user cache
	userCache.Flush()

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"id":       id,
		"username": req.Username,
		"role":     req.Role,
		"isActive": req.IsActive,
	})
}

func deleteUserHandler(w http.ResponseWriter, r *http.Request) {
	vars := mux.Vars(r)
	id := vars["id"]

	_, err := db.Exec("DELETE FROM Users WHERE Id = ?", id)
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}

	// Invalidate user cache
	userCache.Flush()

	respondJSON(w, http.StatusOK, map[string]bool{"success": true})
}

// Route handlers
func getRoutesHandler(w http.ResponseWriter, r *http.Request) {
	// Check cache first
	if cached, found := routeCache.Get("routes:all"); found {
		respondJSON(w, http.StatusOK, cached)
		return
	}

	// Cache miss - query database
	rows, err := db.Query("SELECT Id, RouteId, ClusterId, MatchPath, RateLimitPerSecond, IsActive, Methods, CircuitBreakerThreshold, CircuitBreakerDurationSeconds, IpWhitelist, IpBlacklist, CacheTtlSeconds, TransformsJson FROM Routes ORDER BY Id DESC")
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}
	defer rows.Close()

	var routes = make([]map[string]interface{}, 0)
	for rows.Next() {
		var id int64
		var routeID, clusterID, matchPath string
		var rateLimit int
		var isActive bool

		// Some old rows might have NULLs for new fields, so we use sql.NullString / sql.NullInt64
		var nsMethods, nsIpW, nsIpB, nsTransforms sql.NullString
		var niCbT, niCbD, niCacheT sql.NullInt64

		rows.Scan(&id, &routeID, &clusterID, &matchPath, &rateLimit, &isActive, &nsMethods, &niCbT, &niCbD, &nsIpW, &nsIpB, &niCacheT, &nsTransforms)

		routes = append(routes, map[string]interface{}{
			"id":                            id,
			"routeId":                       routeID,
			"clusterId":                     clusterID,
			"matchPath":                     matchPath,
			"rateLimitPerSecond":            rateLimit,
			"isActive":                      isActive,
			"methods":                       nsMethods.String,
			"circuitBreakerThreshold":       niCbT.Int64,
			"circuitBreakerDurationSeconds": niCbD.Int64,
			"ipWhitelist":                   nsIpW.String,
			"ipBlacklist":                   nsIpB.String,
			"cacheTtlSeconds":               niCacheT.Int64,
			"transformsJson":                nsTransforms.String,
		})
	}

	// Cache the result
	routeCache.Set("routes:all", routes, cache.DefaultExpiration)

	respondJSON(w, http.StatusOK, routes)
}

func createRouteHandler(w http.ResponseWriter, r *http.Request) {
	var req struct {
		RouteID                       string `json:"routeId"`
		ClusterID                     string `json:"clusterId"`
		MatchPath                     string `json:"matchPath"`
		RateLimitPerSecond            int    `json:"rateLimitPerSecond"`
		IsActive                      bool   `json:"isActive"`
		Methods                       string `json:"methods"`
		CircuitBreakerThreshold       int    `json:"circuitBreakerThreshold"`
		CircuitBreakerDurationSeconds int    `json:"circuitBreakerDurationSeconds"`
		IpWhitelist                   string `json:"ipWhitelist"`
		IpBlacklist                   string `json:"ipBlacklist"`
		CacheTtlSeconds               int    `json:"cacheTtlSeconds"`
		TransformsJson                string `json:"transformsJson"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request"})
		return
	}

	result, err := db.Exec("INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond, IsActive, Methods, CircuitBreakerThreshold, CircuitBreakerDurationSeconds, IpWhitelist, IpBlacklist, CacheTtlSeconds, TransformsJson) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
		req.RouteID, req.ClusterID, req.MatchPath, req.RateLimitPerSecond, true, req.Methods, req.CircuitBreakerThreshold, req.CircuitBreakerDurationSeconds, req.IpWhitelist, req.IpBlacklist, req.CacheTtlSeconds, req.TransformsJson)

	if err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}

	id, _ := result.LastInsertId()

	// Invalidate route cache
	routeCache.Delete("routes:all")

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"id":                            id,
		"routeId":                       req.RouteID,
		"clusterId":                     req.ClusterID,
		"matchPath":                     req.MatchPath,
		"rateLimitPerSecond":            req.RateLimitPerSecond,
		"isActive":                      req.IsActive,
		"methods":                       req.Methods,
		"circuitBreakerThreshold":       req.CircuitBreakerThreshold,
		"circuitBreakerDurationSeconds": req.CircuitBreakerDurationSeconds,
		"ipWhitelist":                   req.IpWhitelist,
		"ipBlacklist":                   req.IpBlacklist,
		"cacheTtlSeconds":               req.CacheTtlSeconds,
		"transformsJson":                req.TransformsJson,
	})
}

func updateRouteHandler(w http.ResponseWriter, r *http.Request) {
	vars := mux.Vars(r)
	id := vars["id"]

	var req struct {
		RouteID                       string `json:"routeId"`
		ClusterID                     string `json:"clusterId"`
		MatchPath                     string `json:"matchPath"`
		RateLimitPerSecond            int    `json:"rateLimitPerSecond"`
		IsActive                      bool   `json:"isActive"`
		Methods                       string `json:"methods"`
		CircuitBreakerThreshold       int    `json:"circuitBreakerThreshold"`
		CircuitBreakerDurationSeconds int    `json:"circuitBreakerDurationSeconds"`
		IpWhitelist                   string `json:"ipWhitelist"`
		IpBlacklist                   string `json:"ipBlacklist"`
		CacheTtlSeconds               int    `json:"cacheTtlSeconds"`
		TransformsJson                string `json:"transformsJson"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request"})
		return
	}

	result, err := db.Exec("UPDATE Routes SET RouteId = ?, ClusterId = ?, MatchPath = ?, RateLimitPerSecond = ?, IsActive = ?, Methods = ?, CircuitBreakerThreshold = ?, CircuitBreakerDurationSeconds = ?, IpWhitelist = ?, IpBlacklist = ?, CacheTtlSeconds = ?, TransformsJson = ? WHERE Id = ?",
		req.RouteID, req.ClusterID, req.MatchPath, req.RateLimitPerSecond, true, req.Methods, req.CircuitBreakerThreshold, req.CircuitBreakerDurationSeconds, req.IpWhitelist, req.IpBlacklist, req.CacheTtlSeconds, req.TransformsJson, id)

	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}

	rowsAffected, _ := result.RowsAffected()
	if rowsAffected == 0 {
		respondJSON(w, http.StatusNotFound, map[string]string{"error": "Route not found"})
		return
	}

	// Invalidate route cache
	routeCache.Delete("routes:all")

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"id":                            id,
		"routeId":                       req.RouteID,
		"clusterId":                     req.ClusterID,
		"matchPath":                     req.MatchPath,
		"rateLimitPerSecond":            req.RateLimitPerSecond,
		"isActive":                      req.IsActive,
		"methods":                       req.Methods,
		"circuitBreakerThreshold":       req.CircuitBreakerThreshold,
		"circuitBreakerDurationSeconds": req.CircuitBreakerDurationSeconds,
		"ipWhitelist":                   req.IpWhitelist,
		"ipBlacklist":                   req.IpBlacklist,
		"cacheTtlSeconds":               req.CacheTtlSeconds,
		"transformsJson":                req.TransformsJson,
	})
}

func deleteRouteHandler(w http.ResponseWriter, r *http.Request) {
	vars := mux.Vars(r)
	id := vars["id"]

	result, err := db.Exec("DELETE FROM Routes WHERE Id = ?", id)
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}

	rowsAffected, _ := result.RowsAffected()
	if rowsAffected == 0 {
		respondJSON(w, http.StatusNotFound, map[string]string{"error": "Route not found"})
		return
	}

	// Invalidate route cache
	routeCache.Delete("routes:all")

	respondJSON(w, http.StatusOK, map[string]string{"message": "Route deleted"})
}

// Cluster handlers
func getClustersHandler(w http.ResponseWriter, r *http.Request) {
	// Check cache first
	if cached, found := clusterCache.Get("clusters:all"); found {
		respondJSON(w, http.StatusOK, cached)
		return
	}

	// Cache miss - query database
	rows, err := db.Query("SELECT Id, ClusterId, DestinationsJson, LoadBalancingPolicy, IsActive, EnableHealthCheck, HealthCheckPath, HealthCheckIntervalSeconds, HealthCheckTimeoutSeconds, RetryCount, RetryDelayMs FROM Clusters ORDER BY Id DESC")
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}
	defer rows.Close()

	var clusters = make([]map[string]interface{}, 0)
	for rows.Next() {
		var id int64
		var clusterID, destJSON, lbPolicy string
		var isActive bool
		var niEh, niHint, niHtout, niRtry, niRdel sql.NullInt64
		var nsHth sql.NullString

		rows.Scan(&id, &clusterID, &destJSON, &lbPolicy, &isActive, &niEh, &nsHth, &niHint, &niHtout, &niRtry, &niRdel)

		eh := true
		if niEh.Valid && niEh.Int64 == 0 {
			eh = false
		}

		clusters = append(clusters, map[string]interface{}{
			"id":                         id,
			"clusterId":                  clusterID,
			"destinationsJson":           destJSON,
			"loadBalancingPolicy":        lbPolicy,
			"isActive":                   isActive,
			"enableHealthCheck":          eh,
			"healthCheckPath":            nsHth.String,
			"healthCheckIntervalSeconds": niHint.Int64,
			"healthCheckTimeoutSeconds":  niHtout.Int64,
			"retryCount":                 niRtry.Int64,
			"retryDelayMs":               niRdel.Int64,
		})
	}

	// Cache the result
	clusterCache.Set("clusters:all", clusters, cache.DefaultExpiration)

	respondJSON(w, http.StatusOK, clusters)
}

func createClusterHandler(w http.ResponseWriter, r *http.Request) {
	var req struct {
		ClusterID                  string `json:"clusterId"`
		DestinationsJson           string `json:"destinationsJson"`
		LoadBalancingPolicy        string `json:"loadBalancingPolicy"`
		IsActive                   bool   `json:"isActive"`
		EnableHealthCheck          bool   `json:"enableHealthCheck"`
		HealthCheckPath            string `json:"healthCheckPath"`
		HealthCheckIntervalSeconds int    `json:"healthCheckIntervalSeconds"`
		HealthCheckTimeoutSeconds  int    `json:"healthCheckTimeoutSeconds"`
		RetryCount                 int    `json:"retryCount"`
		RetryDelayMs               int    `json:"retryDelayMs"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request"})
		return
	}

	eh := 0
	if req.EnableHealthCheck {
		eh = 1
	}

	result, err := db.Exec("INSERT INTO Clusters (ClusterId, DestinationsJson, LoadBalancingPolicy, IsActive, EnableHealthCheck, HealthCheckPath, HealthCheckIntervalSeconds, HealthCheckTimeoutSeconds, RetryCount, RetryDelayMs) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
		req.ClusterID, req.DestinationsJson, req.LoadBalancingPolicy, true, eh, req.HealthCheckPath, req.HealthCheckIntervalSeconds, req.HealthCheckTimeoutSeconds, req.RetryCount, req.RetryDelayMs)

	if err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}

	id, _ := result.LastInsertId()

	// Invalidate cluster cache
	clusterCache.Delete("clusters:all")

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"id":                         id,
		"clusterId":                  req.ClusterID,
		"destinationsJson":           req.DestinationsJson,
		"loadBalancingPolicy":        req.LoadBalancingPolicy,
		"isActive":                   req.IsActive,
		"enableHealthCheck":          req.EnableHealthCheck,
		"healthCheckPath":            req.HealthCheckPath,
		"healthCheckIntervalSeconds": req.HealthCheckIntervalSeconds,
		"healthCheckTimeoutSeconds":  req.HealthCheckTimeoutSeconds,
		"retryCount":                 req.RetryCount,
		"retryDelayMs":               req.RetryDelayMs,
	})
}

func updateClusterHandler(w http.ResponseWriter, r *http.Request) {
	vars := mux.Vars(r)
	id := vars["id"]

	var req struct {
		ClusterID                  string `json:"clusterId"`
		DestinationsJson           string `json:"destinationsJson"`
		LoadBalancingPolicy        string `json:"loadBalancingPolicy"`
		IsActive                   bool   `json:"isActive"`
		EnableHealthCheck          bool   `json:"enableHealthCheck"`
		HealthCheckPath            string `json:"healthCheckPath"`
		HealthCheckIntervalSeconds int    `json:"healthCheckIntervalSeconds"`
		HealthCheckTimeoutSeconds  int    `json:"healthCheckTimeoutSeconds"`
		RetryCount                 int    `json:"retryCount"`
		RetryDelayMs               int    `json:"retryDelayMs"`
	}

	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid request"})
		return
	}

	eh := 0
	if req.EnableHealthCheck {
		eh = 1
	}

	result, err := db.Exec("UPDATE Clusters SET ClusterId = ?, DestinationsJson = ?, LoadBalancingPolicy = ?, IsActive = ?, EnableHealthCheck = ?, HealthCheckPath = ?, HealthCheckIntervalSeconds = ?, HealthCheckTimeoutSeconds = ?, RetryCount = ?, RetryDelayMs = ? WHERE Id = ?",
		req.ClusterID, req.DestinationsJson, req.LoadBalancingPolicy, true, eh, req.HealthCheckPath, req.HealthCheckIntervalSeconds, req.HealthCheckTimeoutSeconds, req.RetryCount, req.RetryDelayMs, id)

	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}

	rowsAffected, _ := result.RowsAffected()
	if rowsAffected == 0 {
		respondJSON(w, http.StatusNotFound, map[string]string{"error": "Cluster not found"})
		return
	}

	// Invalidate cluster cache
	clusterCache.Delete("clusters:all")

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"id":                         id,
		"clusterId":                  req.ClusterID,
		"destinationsJson":           req.DestinationsJson,
		"loadBalancingPolicy":        req.LoadBalancingPolicy,
		"isActive":                   req.IsActive,
		"enableHealthCheck":          req.EnableHealthCheck,
		"healthCheckPath":            req.HealthCheckPath,
		"healthCheckIntervalSeconds": req.HealthCheckIntervalSeconds,
		"healthCheckTimeoutSeconds":  req.HealthCheckTimeoutSeconds,
		"retryCount":                 req.RetryCount,
		"retryDelayMs":               req.RetryDelayMs,
	})
}

func deleteClusterHandler(w http.ResponseWriter, r *http.Request) {
	vars := mux.Vars(r)
	id := vars["id"]

	result, err := db.Exec("DELETE FROM Clusters WHERE Id = ?", id)
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}

	rowsAffected, _ := result.RowsAffected()
	if rowsAffected == 0 {
		respondJSON(w, http.StatusNotFound, map[string]string{"error": "Cluster not found"})
		return
	}

	// Invalidate cluster cache
	clusterCache.Delete("clusters:all")

	respondJSON(w, http.StatusOK, map[string]string{"message": "Cluster deleted"})
}

// Metrics handlers
func metricsHandler(w http.ResponseWriter, r *http.Request) {
	uptime := int64(time.Since(globalMetrics.StartTime).Seconds())

	routesMap := make(map[string]RouteMetrics)
	routeMetricsMap.Range(func(key, value interface{}) bool {
		st := value.(*routeStat)
		total := atomic.LoadInt64(&st.total)
		success := atomic.LoadInt64(&st.success)
		errCount := atomic.LoadInt64(&st.errCount)
		totalLatency := atomic.LoadInt64(&st.totalLatency)
		maxLatency := atomic.LoadInt64(&st.maxLatency)

		m := RouteMetrics{
			TotalRequests:  total,
			SuccessCount:   success,
			ErrorCount:     errCount,
			TotalLatencyMs: totalLatency,
			MaxLatencyMs:   int(maxLatency),
			UptimeSeconds:  uptime,
		}
		if total > 0 {
			m.AvgLatencyMs = int(totalLatency / total)
			m.ErrorRate = (float64(errCount) / float64(total)) * 100
		}
		if uptime > 0 {
			m.ThroughputPerSecond = float64(total) / float64(uptime)
		}
		routesMap[key.(string)] = m
		return true
	})

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"routes":        routesMap,
		"wsConnections": atomic.LoadInt64(&globalMetrics.WSConnections),
		"wsMessages":    atomic.LoadInt64(&globalMetrics.WSMessages),
		"timestamp":     time.Now().Format(time.RFC3339),
	})
}

func resetMetricsHandler(w http.ResponseWriter, r *http.Request) {
	routeMetricsMap.Range(func(key, _ interface{}) bool {
		routeMetricsMap.Delete(key)
		return true
	})
	respondJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func getLogsHandler(w http.ResponseWriter, r *http.Request) {
	page := 1
	pageSize := 50

	if p := r.URL.Query().Get("page"); p != "" {
		fmt.Sscanf(p, "%d", &page)
	}
	if ps := r.URL.Query().Get("pageSize"); ps != "" {
		fmt.Sscanf(ps, "%d", &pageSize)
	}

	offset := (page - 1) * pageSize

	whereClauses := []string{"1=1"}
	var args []interface{}

	if routeId := r.URL.Query().Get("routeId"); routeId != "" {
		whereClauses = append(whereClauses, "RouteId = ?")
		args = append(args, routeId)
	}
	if method := r.URL.Query().Get("method"); method != "" {
		whereClauses = append(whereClauses, "Method = ?")
		args = append(args, method)
	}
	if sc := r.URL.Query().Get("statusCode"); sc != "" {
		whereClauses = append(whereClauses, "StatusCode = ?")
		args = append(args, sc)
	}

	whereQuery := strings.Join(whereClauses, " AND ")

	var total int
	db.QueryRow("SELECT COUNT(*) FROM RequestLogs WHERE "+whereQuery, args...).Scan(&total)

	query := "SELECT Id, Timestamp, Method, Path, StatusCode, LatencyMs, ClientIp, RouteId FROM RequestLogs WHERE " + whereQuery + " ORDER BY Id DESC LIMIT ? OFFSET ?"
	args = append(args, pageSize, offset)

	rows, err := db.Query(query, args...)
	if err != nil {
		respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
		return
	}
	defer rows.Close()

	var logs []map[string]interface{}
	for rows.Next() {
		var id, statusCode, latency int
		var ts, method, path, clientIp, routeId string

		rows.Scan(&id, &ts, &method, &path, &statusCode, &latency, &clientIp, &routeId)

		logs = append(logs, map[string]interface{}{
			"id":         id,
			"timestamp":  ts,
			"method":     method,
			"path":       path,
			"statusCode": statusCode,
			"latencyMs":  latency,
			"clientIp":   clientIp,
			"routeId":    routeId,
		})
	}

	if logs == nil {
		logs = []map[string]interface{}{}
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"total": total,
		"logs":  logs,
		"page":  page,
	})
}

func clearLogsHandler(w http.ResponseWriter, r *http.Request) {
	// Optional ?olderThanDays=N purges only logs older than N days; without it,
	// all logs are cleared (preserves existing behaviour).
	if d := r.URL.Query().Get("olderThanDays"); d != "" {
		days, err := strconv.Atoi(d)
		if err != nil || days < 0 {
			respondJSON(w, http.StatusBadRequest, map[string]string{"error": "Invalid olderThanDays"})
			return
		}
		cutoff := time.Now().AddDate(0, 0, -days).Format(time.RFC3339)
		res, err := db.Exec("DELETE FROM RequestLogs WHERE Timestamp < ?", cutoff)
		if err != nil {
			respondJSON(w, http.StatusInternalServerError, map[string]string{"error": err.Error()})
			return
		}
		removed, _ := res.RowsAffected()
		respondJSON(w, http.StatusOK, map[string]interface{}{"status": "ok", "removed": removed})
		return
	}

	db.Exec("DELETE FROM RequestLogs")
	respondJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

func getLogStatsHandler(w http.ResponseWriter, r *http.Request) {
	var total int
	db.QueryRow("SELECT COUNT(*) FROM RequestLogs").Scan(&total)

	var last24h int
	db.QueryRow("SELECT COUNT(*) FROM RequestLogs WHERE Timestamp >= date('now', '-1 day')").Scan(&last24h)

	type StatusStat struct {
		StatusGroup string `json:"statusGroup"`
		Count       int    `json:"count"`
	}
	var byStatus []StatusStat

	rows, err := db.Query("SELECT CASE WHEN StatusCode >= 200 AND StatusCode <= 299 THEN '2xx Success' WHEN StatusCode >= 300 AND StatusCode <= 399 THEN '3xx Redirection' WHEN StatusCode >= 400 AND StatusCode <= 499 THEN '4xx Client Error' WHEN StatusCode >= 500 AND StatusCode <= 599 THEN '5xx Server Error' ELSE 'Other' END as StatusGroup, COUNT(*) FROM RequestLogs GROUP BY StatusGroup ORDER BY StatusGroup")
	if err == nil {
		defer rows.Close()
		for rows.Next() {
			var sg string
			var count int
			rows.Scan(&sg, &count)
			byStatus = append(byStatus, StatusStat{StatusGroup: sg, Count: count})
		}
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"total":    total,
		"last24h":  last24h,
		"byStatus": byStatus,
		"retention": map[string]interface{}{
			"enabled":              LogRetentionDays > 0,
			"retentionDays":        LogRetentionDays,
			"cleanupIntervalHours": LogCleanupIntervalHours,
		},
	})
}

func statsHandler(w http.ResponseWriter, r *http.Request) {
	var routeCount, clusterCount, userCount int
	db.QueryRow("SELECT COUNT(*) FROM Routes").Scan(&routeCount)
	db.QueryRow("SELECT COUNT(*) FROM Clusters").Scan(&clusterCount)
	db.QueryRow("SELECT COUNT(*) FROM Users").Scan(&userCount)

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"totalRoutes":   routeCount,
		"totalClusters": clusterCount,
		"totalUsers":    userCount,
		"wsConnections": atomic.LoadInt64(&globalMetrics.WSConnections),
		"uptime":        int(time.Since(globalMetrics.StartTime).Seconds()),
	})
}

// Proxy setup
func setupProxyRoutes(r *mux.Router) {
	r.PathPrefix("/").HandlerFunc(dynamicProxyHandler)
}

func dynamicProxyHandler(w http.ResponseWriter, req *http.Request) {
	// Look up routes from cache or DB
	var routes []map[string]interface{}
	if cached, found := routeCache.Get("routes:all"); found {
		routes = cached.([]map[string]interface{})
	} else {
		// Cache miss - query database
		rows, err := db.Query("SELECT Id, RouteId, ClusterId, MatchPath, RateLimitPerSecond, IsActive, Methods, CircuitBreakerThreshold, CircuitBreakerDurationSeconds, IpWhitelist, IpBlacklist, CacheTtlSeconds, TransformsJson FROM Routes ORDER BY LENGTH(MatchPath) DESC")
		if err != nil {
			respondJSON(w, http.StatusInternalServerError, map[string]string{"error": "Failed to load routes"})
			return
		}
		defer rows.Close()

		for rows.Next() {
			var id int64
			var routeID, clusterID, matchPath string
			var rateLimit int
			var isActive bool

			var nsMethods, nsIpW, nsIpB, nsTransforms sql.NullString
			var niCbT, niCbD, niCacheT sql.NullInt64

			rows.Scan(&id, &routeID, &clusterID, &matchPath, &rateLimit, &isActive, &nsMethods, &niCbT, &niCbD, &nsIpW, &nsIpB, &niCacheT, &nsTransforms)

			routes = append(routes, map[string]interface{}{
				"id":                            id,
				"routeId":                       routeID,
				"clusterId":                     clusterID,
				"matchPath":                     matchPath,
				"rateLimitPerSecond":            rateLimit,
				"isActive":                      isActive,
				"methods":                       nsMethods.String,
				"circuitBreakerThreshold":       niCbT.Int64,
				"circuitBreakerDurationSeconds": niCbD.Int64,
				"ipWhitelist":                   nsIpW.String,
				"ipBlacklist":                   nsIpB.String,
				"cacheTtlSeconds":               niCacheT.Int64,
				"transformsJson":                nsTransforms.String,
			})
		}
		routeCache.Set("routes:all", routes, cache.DefaultExpiration)
	}

	var matchedRoute map[string]interface{}
	var prefix string
	reqPath := req.URL.Path

	for _, rt := range routes {
		if isActive, ok := rt["isActive"].(bool); !ok || !isActive {
			continue
		}
		matchPath := rt["matchPath"].(string)

		if reqPath == matchPath || strings.HasPrefix(reqPath, matchPath+"/") {
			matchedRoute = rt
			prefix = matchPath
			break
		}
	}

	if matchedRoute == nil {
		respondJSON(w, http.StatusNotFound, map[string]string{"error": "No route matched"})
		return
	}

	routeID := matchedRoute["routeId"].(string)
	req.Header.Set("X-Gateway-RouteId", routeID)
	clusterID := matchedRoute["clusterId"].(string)

	rateLimit := 0
	if rl, ok := matchedRoute["rateLimitPerSecond"].(int); ok {
		rateLimit = rl
	} else if rl, ok := matchedRoute["rateLimitPerSecond"].(int64); ok {
		rateLimit = int(rl)
	} else if rl, ok := matchedRoute["rateLimitPerSecond"].(float64); ok {
		rateLimit = int(rl)
	}

	if rateLimit > 0 {
		limiter := getRateLimiter(routeID, rateLimit)
		if !limiter.Allow() {
			respondJSON(w, http.StatusTooManyRequests, map[string]string{"error": "Too many requests"})
			return
		}
	}

	// Load destinations
	var destinations []Destination
	cacheKey := "cluster:" + clusterID
	if cached, found := clusterCache.Get(cacheKey); found {
		destinations = cached.([]Destination)
	} else {
		var destJSON string
		err := db.QueryRow("SELECT DestinationsJson FROM Clusters WHERE ClusterId = ? AND IsActive = 1", clusterID).Scan(&destJSON)
		if err == nil && destJSON != "" {
			json.Unmarshal([]byte(destJSON), &destinations)
			clusterCache.Set(cacheKey, destinations, cache.DefaultExpiration)
		}
	}

	if len(destinations) == 0 {
		respondJSON(w, http.StatusBadGateway, map[string]interface{}{"error": "No healthy upstream destinations for cluster " + clusterID})
		return
	}

	target := destinations[0].Address
	if !strings.HasPrefix(target, "http://") && !strings.HasPrefix(target, "https://") {
		target = "http://" + target
	}
	targetURL, err := url.Parse(target)
	if err != nil || targetURL == nil {
		log.Printf("⚠️ Invalid target URL parsed: %s, err: %v", target, err)
		respondJSON(w, http.StatusBadGateway, map[string]string{"error": "Invalid gateway upstream URL"})
		return
	}
	proxy := getReverseProxy(targetURL)

	req.URL.Path = strings.TrimPrefix(req.URL.Path, prefix)
	if req.URL.Path == "" {
		req.URL.Path = "/"
	}

	proxy.ServeHTTP(w, req)
}

func getRateLimiter(key string, rps int) *rate.Limiter {
	if limiter, ok := rateLimiters.Load(key); ok {
		return limiter.(*rate.Limiter)
	}

	limiter := rate.NewLimiter(rate.Limit(rps), rps)
	rateLimiters.Store(key, limiter)
	return limiter
}

// Utilities
func getEnv(key, defaultValue string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}
	return defaultValue
}

func getEnvInt(key string, defaultValue int) int {
	if value := os.Getenv(key); value != "" {
		if n, err := strconv.Atoi(value); err == nil {
			return n
		}
	}
	return defaultValue
}

// generateTokenPair issues a signed access token and refresh token for a user.
// Centralizes claim shape and TTLs so login and refresh stay in sync.
func generateTokenPair(id int64, username, role string) (string, string, error) {
	now := time.Now()

	accessToken := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"id":       id,
		"username": username,
		"role":     role,
		"typ":      "access",
		"exp":      now.Add(AccessTokenTTL).Unix(),
		"iat":      now.Unix(),
	})
	accessTokenString, err := accessToken.SignedString(JWTSecret)
	if err != nil {
		return "", "", err
	}

	refreshToken := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"id":       id,
		"username": username,
		"typ":      "refresh",
		"exp":      now.Add(RefreshTokenTTL).Unix(),
		"iat":      now.Unix(),
	})
	refreshTokenString, err := refreshToken.SignedString(JWTSecret)
	if err != nil {
		return "", "", err
	}

	return accessTokenString, refreshTokenString, nil
}

// parseToken validates a JWT string, pinning the signing method to HMAC to
// prevent algorithm-confusion attacks.
func parseToken(tokenString string) (jwt.MapClaims, error) {
	token, err := jwt.Parse(tokenString, func(t *jwt.Token) (interface{}, error) {
		if _, ok := t.Method.(*jwt.SigningMethodHMAC); !ok {
			return nil, fmt.Errorf("unexpected signing method: %v", t.Header["alg"])
		}
		return JWTSecret, nil
	})
	if err != nil {
		return nil, err
	}
	claims, ok := token.Claims.(jwt.MapClaims)
	if !ok || !token.Valid {
		return nil, fmt.Errorf("invalid token")
	}
	return claims, nil
}

func respondJSON(w http.ResponseWriter, status int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}
