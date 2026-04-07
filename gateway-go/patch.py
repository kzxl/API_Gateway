import re

with open('main.go', 'r', encoding='utf-8') as f:
    code = f.read()

# 1. Update Route struct
code = code.replace(
"""type Route struct {
	ID                 int64
	RouteID            string
	ClusterID          string
	MatchPath          string
	RateLimitPerSecond int
	IsActive           bool
}""",
"""type Route struct {
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
}"""
)

# 2. Update Cluster struct
code = code.replace(
"""type Cluster struct {
	ID                  int64
	ClusterID           string
	DestinationsJSON    string
	LoadBalancingPolicy string
	IsActive            bool
}""",
"""type Cluster struct {
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
}"""
)

# 3. Add migrations to initDB
initdb_patch = """	// Create tables
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

	// Seed data"""

if "// Create tables\n\tcreateTables()\n\n\t// Seed data" in code:
    code = code.replace("""	// Create tables
	createTables()

	// Seed data""", initdb_patch)
else:
    print("Could not find initDB hook")

# 4. healthHandler implementation
old_health = """func healthHandler(w http.ResponseWriter, r *http.Request) {
	respondJSON(w, http.StatusOK, map[string]interface{}{
		"status":        "ok",
		"timestamp":     time.Now().Format(time.RFC3339),
		"uptime":        int(time.Since(metrics.StartTime).Seconds()),
		"wsConnections": atomic.LoadInt64(&metrics.WSConnections),
	})
}"""

new_health = """func healthHandler(w http.ResponseWriter, r *http.Request) {
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
}"""

if old_health in code:
    code = code.replace(old_health, new_health)
else:
    print("Could not find healthHandler")

# 5. create/update Route
r_getRoute = re.sub(r'func getRoutesHandler.*?// Cache the result', """func getRoutesHandler(w http.ResponseWriter, r *http.Request) {
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

	var routes []map[string]interface{}
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

	// Cache the result""", code, flags=re.DOTALL)
if r_getRoute != code:
    code = r_getRoute
else:
    print("Could not patch getRoutesHandler")

# Create route handler req struct
code = code.replace(
"""	var req struct {
		RouteID            string `json:"routeId"`
		ClusterID          string `json:"clusterId"`
		MatchPath          string `json:"matchPath"`
		RateLimitPerSecond int    `json:"rateLimitPerSecond"`
		IsActive           bool   `json:"isActive"`
	}""",
"""	var req struct {
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
	}""")

code = code.replace(
"""	result, err := db.Exec("INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond, IsActive) VALUES (?, ?, ?, ?, ?)",
		req.RouteID, req.ClusterID, req.MatchPath, req.RateLimitPerSecond, req.IsActive)""",
"""	result, err := db.Exec("INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond, IsActive, Methods, CircuitBreakerThreshold, CircuitBreakerDurationSeconds, IpWhitelist, IpBlacklist, CacheTtlSeconds, TransformsJson) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
		req.RouteID, req.ClusterID, req.MatchPath, req.RateLimitPerSecond, req.IsActive, req.Methods, req.CircuitBreakerThreshold, req.CircuitBreakerDurationSeconds, req.IpWhitelist, req.IpBlacklist, req.CacheTtlSeconds, req.TransformsJson)""")

code = code.replace(
"""		"rateLimitPerSecond": req.RateLimitPerSecond,
		"isActive":           req.IsActive,
	})""",
"""		"rateLimitPerSecond": req.RateLimitPerSecond,
		"isActive":           req.IsActive,
		"methods":            req.Methods,
		"circuitBreakerThreshold": req.CircuitBreakerThreshold,
		"circuitBreakerDurationSeconds": req.CircuitBreakerDurationSeconds,
		"ipWhitelist": req.IpWhitelist,
		"ipBlacklist": req.IpBlacklist,
		"cacheTtlSeconds": req.CacheTtlSeconds,
		"transformsJson": req.TransformsJson,
	})""")

code = code.replace(
"""	result, err := db.Exec("UPDATE Routes SET RouteId = ?, ClusterId = ?, MatchPath = ?, RateLimitPerSecond = ?, IsActive = ? WHERE Id = ?",
		req.RouteID, req.ClusterID, req.MatchPath, req.RateLimitPerSecond, req.IsActive, id)""",
"""	result, err := db.Exec("UPDATE Routes SET RouteId = ?, ClusterId = ?, MatchPath = ?, RateLimitPerSecond = ?, IsActive = ?, Methods = ?, CircuitBreakerThreshold = ?, CircuitBreakerDurationSeconds = ?, IpWhitelist = ?, IpBlacklist = ?, CacheTtlSeconds = ?, TransformsJson = ? WHERE Id = ?",
		req.RouteID, req.ClusterID, req.MatchPath, req.RateLimitPerSecond, req.IsActive, req.Methods, req.CircuitBreakerThreshold, req.CircuitBreakerDurationSeconds, req.IpWhitelist, req.IpBlacklist, req.CacheTtlSeconds, req.TransformsJson, id)""")

# 6. Clusters Handler
r_getCluster = re.sub(r'func getClustersHandler.*?// Cache the result', """func getClustersHandler(w http.ResponseWriter, r *http.Request) {
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

	var clusters []map[string]interface{}
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

	// Cache the result""", code, flags=re.DOTALL)
if r_getCluster != code:
    code = r_getCluster
else:
    print("Could not patch getClustersHandler")

# Clusters req JSON structs
old_cluster_req = """	var req struct {
		ClusterID           string        `json:"clusterId"`
		Destinations        []Destination `json:"destinations"`
		LoadBalancingPolicy string        `json:"loadBalancingPolicy"`
		IsActive            bool          `json:"isActive"`
	}"""
new_cluster_req = """	var req struct {
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
	}"""
code = code.replace(old_cluster_req, new_cluster_req)

old_cluster_ins = """	destJSON, _ := json.Marshal(req.Destinations)

	result, err := db.Exec("INSERT INTO Clusters (ClusterId, DestinationsJson, LoadBalancingPolicy, IsActive) VALUES (?, ?, ?, ?)",
		req.ClusterID, string(destJSON), req.LoadBalancingPolicy, req.IsActive)"""
new_cluster_ins = """	eh := 0
	if req.EnableHealthCheck { eh = 1 }

	result, err := db.Exec("INSERT INTO Clusters (ClusterId, DestinationsJson, LoadBalancingPolicy, IsActive, EnableHealthCheck, HealthCheckPath, HealthCheckIntervalSeconds, HealthCheckTimeoutSeconds, RetryCount, RetryDelayMs) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
		req.ClusterID, req.DestinationsJson, req.LoadBalancingPolicy, req.IsActive, eh, req.HealthCheckPath, req.HealthCheckIntervalSeconds, req.HealthCheckTimeoutSeconds, req.RetryCount, req.RetryDelayMs)"""
code = code.replace(old_cluster_ins, new_cluster_ins)

old_cluster_resp = """		"destinations":        req.Destinations,
		"loadBalancingPolicy": req.LoadBalancingPolicy,
		"isActive":            req.IsActive,
	})"""
new_cluster_resp = """		"destinationsJson":    req.DestinationsJson,
		"loadBalancingPolicy": req.LoadBalancingPolicy,
		"isActive":            req.IsActive,
		"enableHealthCheck":   req.EnableHealthCheck,
		"healthCheckPath":     req.HealthCheckPath,
		"healthCheckIntervalSeconds": req.HealthCheckIntervalSeconds,
		"healthCheckTimeoutSeconds":  req.HealthCheckTimeoutSeconds,
		"retryCount":          req.RetryCount,
		"retryDelayMs":        req.RetryDelayMs,
	})"""
code = code.replace(old_cluster_resp, new_cluster_resp)

old_cluster_upd = """	destJSON, _ := json.Marshal(req.Destinations)

	result, err := db.Exec("UPDATE Clusters SET ClusterId = ?, DestinationsJson = ?, LoadBalancingPolicy = ?, IsActive = ? WHERE Id = ?",
		req.ClusterID, string(destJSON), req.LoadBalancingPolicy, req.IsActive, id)"""
new_cluster_upd = """	eh := 0
	if req.EnableHealthCheck { eh = 1 }

	result, err := db.Exec("UPDATE Clusters SET ClusterId = ?, DestinationsJson = ?, LoadBalancingPolicy = ?, IsActive = ?, EnableHealthCheck = ?, HealthCheckPath = ?, HealthCheckIntervalSeconds = ?, HealthCheckTimeoutSeconds = ?, RetryCount = ?, RetryDelayMs = ? WHERE Id = ?",
		req.ClusterID, req.DestinationsJson, req.LoadBalancingPolicy, req.IsActive, eh, req.HealthCheckPath, req.HealthCheckIntervalSeconds, req.HealthCheckTimeoutSeconds, req.RetryCount, req.RetryDelayMs, id)"""
code = code.replace(old_cluster_upd, new_cluster_upd)

with open('main.go', 'w', encoding='utf-8') as f:
    f.write(code)
