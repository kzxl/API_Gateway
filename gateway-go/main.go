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
	"strings"
	"sync"
	"sync/atomic"
	"syscall"
	"time"

	cache "github.com/patrickmn/go-cache"
	"github.com/golang-jwt/jwt/v5"
	"github.com/gorilla/mux"
	"github.com/gorilla/websocket"
	_ "modernc.org/sqlite"
	"github.com/rs/cors"
	"golang.org/x/crypto/bcrypt"
	"golang.org/x/time/rate"
)

// Configuration
var (
	Port      = getEnv("PORT", "8887")
	JWTSecret = []byte(getEnv("JWT_SECRET", "GatewaySecretKey-Change-This-In-Production-Min32Chars!"))
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
	ID                 int64
	RouteID            string
	ClusterID          string
	MatchPath          string
	RateLimitPerSecond int
	IsActive           bool
	Methods                       string
	CircuitBreakerThreshold       int
	CircuitBreakerDurationSeconds int
	IpWhitelist                   string
	IpBlacklist                   string
	CacheTtlSeconds               int
	TransformsJson                string
}

type Cluster struct {
	ID                  int64
	ClusterID           string
	DestinationsJSON    string
	LoadBalancingPolicy string
	IsActive            bool
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
type Metrics struct {
	TotalRequests   int64
	SuccessRequests int64
	FailedRequests  int64
	TotalLatency    int64
	WSConnections   int64
	WSMessages      int64
	StartTime       time.Time
}

var metrics = &Metrics{StartTime: time.Now()}

// Database
var db *sql.DB

// Cache (L1 - In-Memory)
var (
	routeCache    *cache.Cache
	clusterCache  *cache.Cache
	userCache     *cache.Cache
)

// Rate limiters
var rateLimiters sync.Map

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

	// Initialize cache
	initCache()

	// Initialize database
	initDB()
	defer db.Close()

	// Create router
	r := mux.NewRouter()

	// Middleware
	r.Use(loggingMiddleware)

	// Health check
	r.HandleFunc("/health", healthHandler).Methods("GET")

	// Auth endpoints
	r.HandleFunc("/auth/login", loginHandler).Methods("POST")
	r.HandleFunc("/auth/refresh", refreshHandler).Methods("POST")
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
	admin.HandleFunc("/stats", statsHandler).Methods("GET")
	admin.HandleFunc("/health", healthHandler).Methods("GET")

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
		Addr:         "0.0.0.0:" + Port,
		Handler:      handler,
		ReadTimeout:  15 * time.Second,
		WriteTimeout: 15 * time.Second,
		IdleTimeout:  60 * time.Second,
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
	routeCache = cache.New(5*time.Minute, 10*time.Minute)    // Routes: 5min TTL
	clusterCache = cache.New(1*time.Minute, 2*time.Minute)   // Clusters: 1min TTL
	userCache = cache.New(2*time.Minute, 5*time.Minute)      // Users: 2min TTL

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

		// Wrap response writer to capture status code
		wrapped := &responseWriter{ResponseWriter: w, statusCode: http.StatusOK}

		next.ServeHTTP(wrapped, r)

		latency := time.Since(start).Milliseconds()

		atomic.AddInt64(&metrics.TotalRequests, 1)
		atomic.AddInt64(&metrics.TotalLatency, latency)

		if wrapped.statusCode < 400 {
			atomic.AddInt64(&metrics.SuccessRequests, 1)
		} else {
			atomic.AddInt64(&metrics.FailedRequests, 1)
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

		tokenString := authHeader[7:] // Remove "Bearer "

		token, err := jwt.Parse(tokenString, func(token *jwt.Token) (interface{}, error) {
			return JWTSecret, nil
		})

		if err != nil || !token.Valid {
			respondJSON(w, http.StatusForbidden, map[string]string{"error": "Invalid token"})
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
            
            if niEh.Valid { eh = int(niEh.Int64) }
            if nsHpath.Valid { hpath = nsHpath.String }
            if niHint.Valid { hint = int(niHint.Int64) }
            
            var dests []Destination
            json.Unmarshal([]byte(djson), &dests)
            for _, d := range dests {
                healthCheckStr := "Disabled"
                if eh == 1 {
                    healthCheckStr = "Enabled"
                }
                destinations = append(destinations, healthDest{
                    ClusterId: cid,
                    Address: d.Address,
                    Role: d.Health,
                    HealthCheck: healthCheckStr,
                    HealthCheckPath: hpath,
                    HealthCheckIntervalSeconds: hint,
                })
            }
        }
    }

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"status":        "ok",
		"timestamp":     time.Now().Format(time.RFC3339),
		"uptime":        int(time.Since(metrics.StartTime).Seconds()),
		"wsConnections": atomic.LoadInt64(&metrics.WSConnections),
        "gateway": map[string]interface{}{
            "totalRoutes": totalRoutes,
            "totalClusters": totalClusters,
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
			accessToken := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
				"id":       user.ID,
				"username": user.Username,
				"role":     user.Role,
				"exp":      time.Now().Add(15 * time.Minute).Unix(),
			})

			accessTokenString, _ := accessToken.SignedString(JWTSecret)

			refreshToken := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
				"id":       user.ID,
				"username": user.Username,
				"exp":      time.Now().Add(7 * 24 * time.Hour).Unix(),
			})

			refreshTokenString, _ := refreshToken.SignedString(JWTSecret)

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
	accessToken := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"id":       user.ID,
		"username": user.Username,
		"role":     user.Role,
		"exp":      time.Now().Add(15 * time.Minute).Unix(),
	})

	accessTokenString, _ := accessToken.SignedString(JWTSecret)

	refreshToken := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"id":       user.ID,
		"username": user.Username,
		"exp":      time.Now().Add(7 * 24 * time.Hour).Unix(),
	})

	refreshTokenString, _ := refreshToken.SignedString(JWTSecret)

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
	// Simplified refresh implementation
	respondJSON(w, http.StatusOK, map[string]string{"message": "Refresh token endpoint"})
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
			"id":                 id,
			"routeId":            routeID,
			"clusterId":          clusterID,
			"matchPath":          matchPath,
			"rateLimitPerSecond": rateLimit,
			"isActive":           isActive,
			"methods":            nsMethods.String,
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
		RouteID            string `json:"routeId"`
		ClusterID          string `json:"clusterId"`
		MatchPath          string `json:"matchPath"`
		RateLimitPerSecond int    `json:"rateLimitPerSecond"`
		IsActive           bool   `json:"isActive"`
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
		"id":                 id,
		"routeId":            req.RouteID,
		"clusterId":          req.ClusterID,
		"matchPath":          req.MatchPath,
		"rateLimitPerSecond": req.RateLimitPerSecond,
		"isActive":           req.IsActive,
		"methods":            req.Methods,
		"circuitBreakerThreshold": req.CircuitBreakerThreshold,
		"circuitBreakerDurationSeconds": req.CircuitBreakerDurationSeconds,
		"ipWhitelist": req.IpWhitelist,
		"ipBlacklist": req.IpBlacklist,
		"cacheTtlSeconds": req.CacheTtlSeconds,
		"transformsJson": req.TransformsJson,
	})
}

func updateRouteHandler(w http.ResponseWriter, r *http.Request) {
	vars := mux.Vars(r)
	id := vars["id"]

	var req struct {
		RouteID            string `json:"routeId"`
		ClusterID          string `json:"clusterId"`
		MatchPath          string `json:"matchPath"`
		RateLimitPerSecond int    `json:"rateLimitPerSecond"`
		IsActive           bool   `json:"isActive"`
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
		"id":                 id,
		"routeId":            req.RouteID,
		"clusterId":          req.ClusterID,
		"matchPath":          req.MatchPath,
		"rateLimitPerSecond": req.RateLimitPerSecond,
		"isActive":           req.IsActive,
		"methods":            req.Methods,
		"circuitBreakerThreshold": req.CircuitBreakerThreshold,
		"circuitBreakerDurationSeconds": req.CircuitBreakerDurationSeconds,
		"ipWhitelist": req.IpWhitelist,
		"ipBlacklist": req.IpBlacklist,
		"cacheTtlSeconds": req.CacheTtlSeconds,
		"transformsJson": req.TransformsJson,
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
        if niEh.Valid && niEh.Int64 == 0 { eh = false }

		clusters = append(clusters, map[string]interface{}{
			"id":                  id,
			"clusterId":           clusterID,
			"destinationsJson":    destJSON,
			"loadBalancingPolicy": lbPolicy,
			"isActive":            isActive,
            "enableHealthCheck":   eh,
            "healthCheckPath":     nsHth.String,
            "healthCheckIntervalSeconds": niHint.Int64,
            "healthCheckTimeoutSeconds": niHtout.Int64,
            "retryCount":          niRtry.Int64,
            "retryDelayMs":        niRdel.Int64,
		})
	}

	// Cache the result
	clusterCache.Set("clusters:all", clusters, cache.DefaultExpiration)

	respondJSON(w, http.StatusOK, clusters)
}

func createClusterHandler(w http.ResponseWriter, r *http.Request) {
	var req struct {
		ClusterID           string        `json:"clusterId"`
		DestinationsJson    string        `json:"destinationsJson"`
		LoadBalancingPolicy string        `json:"loadBalancingPolicy"`
		IsActive            bool          `json:"isActive"`
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
	if req.EnableHealthCheck { eh = 1 }

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
		"id":                  id,
		"clusterId":           req.ClusterID,
		"destinationsJson":    req.DestinationsJson,
		"loadBalancingPolicy": req.LoadBalancingPolicy,
		"isActive":            req.IsActive,
		"enableHealthCheck":   req.EnableHealthCheck,
		"healthCheckPath":     req.HealthCheckPath,
		"healthCheckIntervalSeconds": req.HealthCheckIntervalSeconds,
		"healthCheckTimeoutSeconds":  req.HealthCheckTimeoutSeconds,
		"retryCount":          req.RetryCount,
		"retryDelayMs":        req.RetryDelayMs,
	})
}

func updateClusterHandler(w http.ResponseWriter, r *http.Request) {
	vars := mux.Vars(r)
	id := vars["id"]

	var req struct {
		ClusterID           string        `json:"clusterId"`
		DestinationsJson    string        `json:"destinationsJson"`
		LoadBalancingPolicy string        `json:"loadBalancingPolicy"`
		IsActive            bool          `json:"isActive"`
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
	if req.EnableHealthCheck { eh = 1 }

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
		"id":                  id,
		"clusterId":           req.ClusterID,
		"destinationsJson":    req.DestinationsJson,
		"loadBalancingPolicy": req.LoadBalancingPolicy,
		"isActive":            req.IsActive,
		"enableHealthCheck":   req.EnableHealthCheck,
		"healthCheckPath":     req.HealthCheckPath,
		"healthCheckIntervalSeconds": req.HealthCheckIntervalSeconds,
		"healthCheckTimeoutSeconds":  req.HealthCheckTimeoutSeconds,
		"retryCount":          req.RetryCount,
		"retryDelayMs":        req.RetryDelayMs,
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
	total := atomic.LoadInt64(&metrics.TotalRequests)
	success := atomic.LoadInt64(&metrics.SuccessRequests)
	failed := atomic.LoadInt64(&metrics.FailedRequests)
	latency := atomic.LoadInt64(&metrics.TotalLatency)

	avgLatency := int64(0)
	if total > 0 {
		avgLatency = latency / total
	}

	successRate := float64(0)
	if total > 0 {
		successRate = float64(success) / float64(total) * 100
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"totalRequests":   total,
		"successRequests": success,
		"failedRequests":  failed,
		"successRate":     fmt.Sprintf("%.2f", successRate),
		"avgLatency":      avgLatency,
		"wsConnections":   atomic.LoadInt64(&metrics.WSConnections),
		"wsMessages":      atomic.LoadInt64(&metrics.WSMessages),
		"uptime":          int(time.Since(metrics.StartTime).Seconds()),
		"timestamp":       time.Now().Format(time.RFC3339),
	})
}

func statsHandler(w http.ResponseWriter, r *http.Request) {
	var routeCount, clusterCount, userCount int
	db.QueryRow("SELECT COUNT(*) FROM Routes").Scan(&routeCount)
	db.QueryRow("SELECT COUNT(*) FROM Clusters").Scan(&clusterCount)
	db.QueryRow("SELECT COUNT(*) FROM Users").Scan(&userCount)

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"totalRoutes":    routeCount,
		"totalClusters":  clusterCount,
		"totalUsers":     userCount,
		"wsConnections":  atomic.LoadInt64(&metrics.WSConnections),
		"uptime":         int(time.Since(metrics.StartTime).Seconds()),
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
				"id":                 id,
				"routeId":            routeID,
				"clusterId":          clusterID,
				"matchPath":          matchPath,
				"rateLimitPerSecond": rateLimit,
				"isActive":           isActive,
				"methods":            nsMethods.String,
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
	proxy := httputil.NewSingleHostReverseProxy(targetURL)

	originalPath := req.URL.Path
	req.URL.Path = strings.TrimPrefix(req.URL.Path, prefix)
	if req.URL.Path == "" {
		req.URL.Path = "/"
	}

	log.Printf("[DYNAMIC PROXY] %s %s -> %s%s (stripped: %s)\n", req.Method, originalPath, target, req.URL.Path, prefix)
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

func respondJSON(w http.ResponseWriter, status int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}
