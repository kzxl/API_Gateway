import os
import re

with open('gateway-go/main.go', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. Models and Variables
old_metrics = """// Metrics
type Metrics struct {
	TotalRequests   int64
	SuccessRequests int64
	FailedRequests  int64
	TotalLatency    int64
	WSConnections   int64
	WSMessages      int64
	StartTime       time.Time
}

var metrics = &Metrics{StartTime: time.Now()}"""

new_metrics = """// Metrics
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

type GlobalMetrics struct {
	WSConnections int64
	WSMessages    int64
	StartTime     time.Time
}

var globalMetrics = &GlobalMetrics{StartTime: time.Now()}
var routeMetricsMap = make(map[string]*RouteMetrics)
var routeMetricsMu sync.RWMutex

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
}"""
if old_metrics in code:
    code = code.replace(old_metrics, new_metrics)
else:
    print("Could not find old Metrics block")

# 2. Main initialize worker
if "	// Initialize cache\n\tinitCache()" in code:
    code = code.replace("	// Initialize cache\n\tinitCache()", "	// Start log worker\n\tif !logWorkerStarted {\n\t\tgo logWorker()\n\t\tlogWorkerStarted = true\n\t}\n\n	// Initialize cache\n\tinitCache()")
else:
    print("Could not find main init cache")

# 3. Create tables hook
old_tables = """`CREATE TABLE IF NOT EXISTS Routes (
			Id INTEGER PRIMARY KEY AUTOINCREMENT,"""
new_tables = """`CREATE TABLE IF NOT EXISTS RequestLogs (
			Id INTEGER PRIMARY KEY AUTOINCREMENT,
			Timestamp TEXT,
			Method TEXT,
			Path TEXT,
			StatusCode INTEGER,
			LatencyMs INTEGER,
			ClientIp TEXT,
			RouteId TEXT
		)`,
		`CREATE TABLE IF NOT EXISTS Routes (
			Id INTEGER PRIMARY KEY AUTOINCREMENT,"""
code = code.replace(old_tables, new_tables)

# 4. Endpoints registration
old_admin = """	admin.HandleFunc("/metrics", metricsHandler).Methods("GET")
	admin.HandleFunc("/stats", statsHandler).Methods("GET")
	admin.HandleFunc("/health", healthHandler).Methods("GET")"""

new_admin = """	admin.HandleFunc("/metrics", metricsHandler).Methods("GET")
	admin.HandleFunc("/metrics", resetMetricsHandler).Methods("DELETE")
	admin.HandleFunc("/stats", statsHandler).Methods("GET")
	admin.HandleFunc("/health", healthHandler).Methods("GET")
	admin.HandleFunc("/logs", getLogsHandler).Methods("GET")
	admin.HandleFunc("/logs", clearLogsHandler).Methods("DELETE")
	admin.HandleFunc("/logs/stats", getLogStatsHandler).Methods("GET")"""
code = code.replace(old_admin, new_admin)


# 5. Health and Stats Handler
code = code.replace("metrics.StartTime", "globalMetrics.StartTime")
code = code.replace("metrics.WSConnections", "globalMetrics.WSConnections")


# 6. Logging Middleware
old_log_mid = """func loggingMiddleware(next http.Handler) http.Handler {
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
}"""

new_log_mid = """func loggingMiddleware(next http.Handler) http.Handler {
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

		routeMetricsMu.Lock()
		m, ok := routeMetricsMap[routeID]
		if !ok {
			m = &RouteMetrics{}
			routeMetricsMap[routeID] = m
		}
		
		m.TotalRequests++
		m.TotalLatencyMs += latency
		
		if int(latency) > m.MaxLatencyMs {
			m.MaxLatencyMs = int(latency)
		}

		if m.TotalRequests > 0 {
			m.AvgLatencyMs = int(m.TotalLatencyMs / m.TotalRequests)
		}

		uptime := int64(time.Since(globalMetrics.StartTime).Seconds())
		m.UptimeSeconds = uptime
		if uptime > 0 {
			m.ThroughputPerSecond = float64(m.TotalRequests) / float64(uptime)
		}

		if wrapped.statusCode < 400 {
			m.SuccessCount++
		} else {
			m.ErrorCount++
		}

		if m.TotalRequests > 0 {
			m.ErrorRate = (float64(m.ErrorCount) / float64(m.TotalRequests)) * 100
		}
		routeMetricsMu.Unlock()
	})
}"""
if old_log_mid in code:
    code = code.replace(old_log_mid, new_log_mid)
else:
    print("Could not find loggingMiddleware")


# 7. Metrics Handler replacement
r_metrics = re.sub(r'func metricsHandler.*?\}\n', """func metricsHandler(w http.ResponseWriter, r *http.Request) {
	routeMetricsMu.RLock()
	defer routeMetricsMu.RUnlock()

	routesMap := make(map[string]RouteMetrics)
	for k, v := range routeMetricsMap {
		routesMap[k] = *v
	}

	respondJSON(w, http.StatusOK, map[string]interface{}{
		"routes":        routesMap,
		"wsConnections": atomic.LoadInt64(&globalMetrics.WSConnections),
		"wsMessages":    atomic.LoadInt64(&globalMetrics.WSMessages),
		"timestamp":     time.Now().Format(time.RFC3339),
	})
}

func resetMetricsHandler(w http.ResponseWriter, r *http.Request) {
	routeMetricsMu.Lock()
	routeMetricsMap = make(map[string]*RouteMetrics)
	routeMetricsMu.Unlock()
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
	db.QueryRow("SELECT COUNT(*) FROM RequestLogs WHERE " + whereQuery, args...).Scan(&total)

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
	})
}\n""", code, flags=re.DOTALL)
if r_metrics != code:
    code = r_metrics
else:
    print("Could not patch metricsHandler")

# 8. Inject routeId in dynamicProxyHandler
if "routeID := matchedRoute[\"routeId\"].(string)" in code:
    code = code.replace(
        "routeID := matchedRoute[\"routeId\"].(string)",
        "routeID := matchedRoute[\"routeId\"].(string)\n\treq.Header.Set(\"X-Gateway-RouteId\", routeID)"
    )

with open('gateway-go/main.go', 'w', encoding='utf-8') as f:
    f.write(code)
print("Patch OK")
