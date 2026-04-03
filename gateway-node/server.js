const express = require('express');
const http = require('http');
const { createProxyMiddleware } = require('http-proxy-middleware');
const httpProxy = require('http-proxy');
const WebSocket = require('ws');
const cors = require('cors');
const sqlite3 = require('sqlite3').verbose();
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');
const rateLimit = require('express-rate-limit');
const url = require('url');

const app = express();
const server = http.createServer(app);
const wss = new WebSocket.Server({ noServer: true });
const proxy = httpProxy.createProxyServer({});

const PORT = process.env.PORT || 8887;
const JWT_SECRET = process.env.JWT_SECRET || 'GatewaySecretKey-Change-This-In-Production-Min32Chars!';

// Middleware
app.use(cors());
app.use(express.json());

// Metrics tracking
let metrics = {
  totalRequests: 0,
  successRequests: 0,
  failedRequests: 0,
  totalLatency: 0,
  wsConnections: 0,
  wsMessages: 0,
  startTime: Date.now()
};

// Database setup
const db = new sqlite3.Database('./gateway.db', (err) => {
  if (err) {
    console.error('Database connection error:', err);
  } else {
    console.log('Connected to SQLite database');
    initDatabase();
  }
});

// Initialize database tables
function initDatabase() {
  db.serialize(() => {
    // Users table
    db.run(`CREATE TABLE IF NOT EXISTS Users (
      Id INTEGER PRIMARY KEY AUTOINCREMENT,
      Username TEXT UNIQUE NOT NULL,
      PasswordHash TEXT NOT NULL,
      Role TEXT NOT NULL,
      IsActive INTEGER DEFAULT 1,
      FailedLoginAttempts INTEGER DEFAULT 0,
      LockedUntil TEXT,
      CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
    )`);

    // RefreshTokens table
    db.run(`CREATE TABLE IF NOT EXISTS RefreshTokens (
      Id INTEGER PRIMARY KEY AUTOINCREMENT,
      UserId INTEGER NOT NULL,
      Token TEXT UNIQUE NOT NULL,
      ExpiresAt TEXT NOT NULL,
      CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
      FOREIGN KEY (UserId) REFERENCES Users(Id)
    )`);

    // Permissions table
    db.run(`CREATE TABLE IF NOT EXISTS Permissions (
      Id INTEGER PRIMARY KEY AUTOINCREMENT,
      Name TEXT UNIQUE NOT NULL,
      Resource TEXT NOT NULL,
      Action TEXT NOT NULL,
      Description TEXT
    )`);

    // Routes table
    db.run(`CREATE TABLE IF NOT EXISTS Routes (
      Id INTEGER PRIMARY KEY AUTOINCREMENT,
      RouteId TEXT UNIQUE NOT NULL,
      ClusterId TEXT NOT NULL,
      MatchPath TEXT NOT NULL,
      RateLimitPerSecond INTEGER DEFAULT 0,
      IsActive INTEGER DEFAULT 1,
      CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
    )`);

    // Clusters table
    db.run(`CREATE TABLE IF NOT EXISTS Clusters (
      Id INTEGER PRIMARY KEY AUTOINCREMENT,
      ClusterId TEXT UNIQUE NOT NULL,
      DestinationsJson TEXT NOT NULL,
      LoadBalancingPolicy TEXT DEFAULT 'RoundRobin',
      IsActive INTEGER DEFAULT 1,
      CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
    )`);

    // RequestLogs table
    db.run(`CREATE TABLE IF NOT EXISTS RequestLogs (
      Id INTEGER PRIMARY KEY AUTOINCREMENT,
      Timestamp TEXT DEFAULT CURRENT_TIMESTAMP,
      Method TEXT NOT NULL,
      Path TEXT NOT NULL,
      StatusCode INTEGER,
      LatencyMs INTEGER,
      ClientIp TEXT,
      RouteId TEXT,
      UserAgent TEXT
    )`);

    // Seed admin user
    db.get('SELECT * FROM Users WHERE Username = ?', ['admin'], (err, row) => {
      if (!row) {
        const hash = bcrypt.hashSync('admin123', 10);
        db.run('INSERT INTO Users (Username, PasswordHash, Role) VALUES (?, ?, ?)',
          ['admin', hash, 'Admin'], (err) => {
            if (!err) console.log('✅ Admin user created: admin/admin123');
          });
      }
    });

    // Seed permissions
    const permissions = [
      { name: 'routes.read', resource: 'routes', action: 'read', description: 'View routes' },
      { name: 'routes.write', resource: 'routes', action: 'write', description: 'Create/update routes' },
      { name: 'routes.delete', resource: 'routes', action: 'delete', description: 'Delete routes' },
      { name: 'clusters.read', resource: 'clusters', action: 'read', description: 'View clusters' },
      { name: 'clusters.write', resource: 'clusters', action: 'write', description: 'Create/update clusters' },
      { name: 'users.read', resource: 'users', action: 'read', description: 'View users' },
      { name: 'users.write', resource: 'users', action: 'write', description: 'Create/update users' },
      { name: 'logs.read', resource: 'logs', action: 'read', description: 'View logs' }
    ];

    permissions.forEach(perm => {
      db.run(`INSERT OR IGNORE INTO Permissions (Name, Resource, Action, Description)
              VALUES (?, ?, ?, ?)`,
        [perm.name, perm.resource, perm.action, perm.description]);
    });

    // Seed default routes
    db.get('SELECT * FROM Routes WHERE RouteId = ?', ['test-route'], (err, row) => {
      if (!row) {
        db.run(`INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond)
                VALUES (?, ?, ?, ?)`,
          ['test-route', 'test-cluster', '/test', 0]);

        db.run(`INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond)
                VALUES (?, ?, ?, ?)`,
          ['api-route', 'test-cluster', '/api', 100]);

        db.run(`INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond)
                VALUES (?, ?, ?, ?)`,
          ['ws-route', 'test-cluster', '/ws', 0]);
      }
    });

    // Seed default cluster
    db.get('SELECT * FROM Clusters WHERE ClusterId = ?', ['test-cluster'], (err, row) => {
      if (!row) {
        const destinations = JSON.stringify([
          { id: 'backend-1', address: 'http://localhost:5001', health: 'Active' }
        ]);
        db.run(`INSERT INTO Clusters (ClusterId, DestinationsJson) VALUES (?, ?)`,
          ['test-cluster', destinations], () => {
            console.log('✅ Default cluster created');
          });
      }
    });
  });
}

// Rate limiter
const limiter = rateLimit({
  windowMs: 1000,
  max: 100,
  message: { error: 'Too many requests' }
});

// Auth middleware
function authenticateToken(req, res, next) {
  const authHeader = req.headers['authorization'];
  const token = authHeader && authHeader.split(' ')[1];

  if (!token) {
    return res.status(401).json({ error: 'Access token required' });
  }

  jwt.verify(token, JWT_SECRET, (err, user) => {
    if (err) {
      return res.status(403).json({ error: 'Invalid token' });
    }
    req.user = user;
    next();
  });
}

// Logging middleware
function logRequest(req, res, next) {
  const start = Date.now();

  res.on('finish', () => {
    const latency = Date.now() - start;
    metrics.totalRequests++;
    metrics.totalLatency += latency;

    if (res.statusCode < 400) {
      metrics.successRequests++;
    } else {
      metrics.failedRequests++;
    }

    // Log to database (async, don't block)
    const clientIp = req.ip || req.connection.remoteAddress;
    db.run(`INSERT INTO RequestLogs (Method, Path, StatusCode, LatencyMs, ClientIp, UserAgent)
            VALUES (?, ?, ?, ?, ?, ?)`,
      [req.method, req.path, res.statusCode, latency, clientIp, req.get('user-agent') || '']);
  });

  next();
}

app.use(logRequest);

// ==================== AUTH ENDPOINTS ====================

// Login
app.post('/auth/login', (req, res) => {
  const { username, password } = req.body;

  db.get('SELECT * FROM Users WHERE Username = ?', [username], (err, user) => {
    if (err || !user) {
      return res.status(401).json({ error: 'Invalid credentials' });
    }

    // Check if account is locked
    if (user.LockedUntil) {
      const lockedUntil = new Date(user.LockedUntil);
      if (lockedUntil > new Date()) {
        return res.status(423).json({ error: 'Account locked', lockedUntil: user.LockedUntil });
      } else {
        // Unlock account
        db.run('UPDATE Users SET FailedLoginAttempts = 0, LockedUntil = NULL WHERE Id = ?', [user.Id]);
      }
    }

    if (!user.IsActive) {
      return res.status(403).json({ error: 'Account disabled' });
    }

    if (!bcrypt.compareSync(password, user.PasswordHash)) {
      // Increment failed attempts
      const failedAttempts = (user.FailedLoginAttempts || 0) + 1;

      if (failedAttempts >= 5) {
        // Lock account for 30 minutes
        const lockedUntil = new Date(Date.now() + 30 * 60 * 1000).toISOString();
        db.run('UPDATE Users SET FailedLoginAttempts = ?, LockedUntil = ? WHERE Id = ?',
          [failedAttempts, lockedUntil, user.Id]);
        return res.status(423).json({ error: 'Account locked due to too many failed attempts' });
      } else {
        db.run('UPDATE Users SET FailedLoginAttempts = ? WHERE Id = ?', [failedAttempts, user.Id]);
        return res.status(401).json({ error: 'Invalid credentials', attemptsLeft: 5 - failedAttempts });
      }
    }

    // Reset failed attempts on successful login
    db.run('UPDATE Users SET FailedLoginAttempts = 0, LockedUntil = NULL WHERE Id = ?', [user.Id]);

    const accessToken = jwt.sign(
      { id: user.Id, username: user.Username, role: user.Role },
      JWT_SECRET,
      { expiresIn: '15m' }
    );

    const refreshToken = jwt.sign(
      { id: user.Id, username: user.Username },
      JWT_SECRET,
      { expiresIn: '7d' }
    );

    // Store refresh token
    const expiresAt = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
    db.run('INSERT INTO RefreshTokens (UserId, Token, ExpiresAt) VALUES (?, ?, ?)',
      [user.Id, refreshToken, expiresAt]);

    res.json({
      accessToken,
      refreshToken,
      user: {
        id: user.Id,
        username: user.Username,
        role: user.Role
      }
    });
  });
});

// Refresh token
app.post('/auth/refresh', (req, res) => {
  const { refreshToken } = req.body;

  if (!refreshToken) {
    return res.status(401).json({ error: 'Refresh token required' });
  }

  db.get('SELECT * FROM RefreshTokens WHERE Token = ?', [refreshToken], (err, tokenRecord) => {
    if (err || !tokenRecord) {
      return res.status(403).json({ error: 'Invalid refresh token' });
    }

    if (new Date(tokenRecord.ExpiresAt) < new Date()) {
      db.run('DELETE FROM RefreshTokens WHERE Token = ?', [refreshToken]);
      return res.status(403).json({ error: 'Refresh token expired' });
    }

    jwt.verify(refreshToken, JWT_SECRET, (err, decoded) => {
      if (err) {
        return res.status(403).json({ error: 'Invalid refresh token' });
      }

      db.get('SELECT * FROM Users WHERE Id = ? AND IsActive = 1', [decoded.id], (err, user) => {
        if (err || !user) {
          return res.status(403).json({ error: 'User not found' });
        }

        const accessToken = jwt.sign(
          { id: user.Id, username: user.Username, role: user.Role },
          JWT_SECRET,
          { expiresIn: '15m' }
        );

        res.json({ accessToken });
      });
    });
  });
});

// Logout
app.post('/auth/logout', authenticateToken, (req, res) => {
  const { refreshToken } = req.body;

  if (refreshToken) {
    db.run('DELETE FROM RefreshTokens WHERE Token = ?', [refreshToken]);
  }

  res.json({ success: true });
});

// ==================== USER ENDPOINTS ====================

// Get all users
app.get('/admin/users', authenticateToken, (req, res) => {
  db.all('SELECT Id, Username, Role, IsActive, CreatedAt FROM Users', [], (err, rows) => {
    if (err) {
      return res.status(500).json({ error: err.message });
    }
    res.json(rows);
  });
});

// Create user
app.post('/admin/users', authenticateToken, (req, res) => {
  const { username, password, role } = req.body;

  if (!username || !password || !role) {
    return res.status(400).json({ error: 'Username, password, and role are required' });
  }

  const hash = bcrypt.hashSync(password, 10);

  db.run('INSERT INTO Users (Username, PasswordHash, Role) VALUES (?, ?, ?)',
    [username, hash, role],
    function(err) {
      if (err) {
        return res.status(400).json({ error: err.message });
      }
      res.json({ id: this.lastID, username, role });
    }
  );
});

// Update user
app.put('/admin/users/:id', authenticateToken, (req, res) => {
  const { id } = req.params;
  const { role, isActive, password } = req.body;

  let query = 'UPDATE Users SET Role = ?, IsActive = ?';
  let params = [role, isActive ? 1 : 0];

  if (password) {
    const hash = bcrypt.hashSync(password, 10);
    query += ', PasswordHash = ?';
    params.push(hash);
  }

  query += ' WHERE Id = ?';
  params.push(id);

  db.run(query, params, function(err) {
    if (err) {
      return res.status(400).json({ error: err.message });
    }
    res.json({ success: true, changes: this.changes });
  });
});

// Delete user
app.delete('/admin/users/:id', authenticateToken, (req, res) => {
  const { id } = req.params;

  db.run('DELETE FROM Users WHERE Id = ?', [id], function(err) {
    if (err) {
      return res.status(400).json({ error: err.message });
    }
    res.json({ success: true, changes: this.changes });
  });
});

// ==================== ROUTE ENDPOINTS ====================

// Get all routes
app.get('/admin/routes', authenticateToken, (req, res) => {
  db.all('SELECT * FROM Routes ORDER BY Id DESC', [], (err, rows) => {
    if (err) {
      return res.status(500).json({ error: err.message });
    }
    res.json(rows);
  });
});

// Create route
app.post('/admin/routes', authenticateToken, (req, res) => {
  const { routeId, clusterId, matchPath, rateLimitPerSecond } = req.body;

  db.run(`INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond)
          VALUES (?, ?, ?, ?)`,
    [routeId, clusterId, matchPath, rateLimitPerSecond || 0],
    function(err) {
      if (err) {
        return res.status(400).json({ error: err.message });
      }
      res.json({ id: this.lastID, routeId, clusterId, matchPath, rateLimitPerSecond });
    }
  );
});

// Update route
app.put('/admin/routes/:id', authenticateToken, (req, res) => {
  const { id } = req.params;
  const { clusterId, matchPath, rateLimitPerSecond, isActive } = req.body;

  db.run(`UPDATE Routes SET ClusterId = ?, MatchPath = ?, RateLimitPerSecond = ?, IsActive = ?
          WHERE Id = ?`,
    [clusterId, matchPath, rateLimitPerSecond, isActive ? 1 : 0, id],
    function(err) {
      if (err) {
        return res.status(400).json({ error: err.message });
      }
      res.json({ success: true, changes: this.changes });
    }
  );
});

// Delete route
app.delete('/admin/routes/:id', authenticateToken, (req, res) => {
  const { id } = req.params;

  db.run('DELETE FROM Routes WHERE Id = ?', [id], function(err) {
    if (err) {
      return res.status(400).json({ error: err.message });
    }
    res.json({ success: true, changes: this.changes });
  });
});

// ==================== CLUSTER ENDPOINTS ====================

// Get all clusters
app.get('/admin/clusters', authenticateToken, (req, res) => {
  db.all('SELECT * FROM Clusters ORDER BY Id DESC', [], (err, rows) => {
    if (err) {
      return res.status(500).json({ error: err.message });
    }
    // Parse DestinationsJson for each cluster
    const clusters = rows.map(cluster => ({
      ...cluster,
      Destinations: JSON.parse(cluster.DestinationsJson)
    }));
    res.json(clusters);
  });
});

// Create cluster
app.post('/admin/clusters', authenticateToken, (req, res) => {
  const { clusterId, destinations, loadBalancingPolicy } = req.body;
  const destinationsJson = JSON.stringify(destinations);

  db.run(`INSERT INTO Clusters (ClusterId, DestinationsJson, LoadBalancingPolicy)
          VALUES (?, ?, ?)`,
    [clusterId, destinationsJson, loadBalancingPolicy || 'RoundRobin'],
    function(err) {
      if (err) {
        return res.status(400).json({ error: err.message });
      }
      res.json({ id: this.lastID, clusterId, destinations, loadBalancingPolicy });
    }
  );
});

// Update cluster
app.put('/admin/clusters/:id', authenticateToken, (req, res) => {
  const { id } = req.params;
  const { destinations, loadBalancingPolicy, isActive } = req.body;
  const destinationsJson = JSON.stringify(destinations);

  db.run(`UPDATE Clusters SET DestinationsJson = ?, LoadBalancingPolicy = ?, IsActive = ?
          WHERE Id = ?`,
    [destinationsJson, loadBalancingPolicy, isActive ? 1 : 0, id],
    function(err) {
      if (err) {
        return res.status(400).json({ error: err.message });
      }
      res.json({ success: true, changes: this.changes });
    }
  );
});

// Delete cluster
app.delete('/admin/clusters/:id', authenticateToken, (req, res) => {
  const { id } = req.params;

  db.run('DELETE FROM Clusters WHERE Id = ?', [id], function(err) {
    if (err) {
      return res.status(400).json({ error: err.message });
    }
    res.json({ success: true, changes: this.changes });
  });
});

// ==================== LOG ENDPOINTS ====================

// Get logs
app.get('/admin/logs', authenticateToken, (req, res) => {
  const limit = parseInt(req.query.limit) || 100;
  const offset = parseInt(req.query.offset) || 0;

  db.all(`SELECT * FROM RequestLogs ORDER BY Id DESC LIMIT ? OFFSET ?`,
    [limit, offset],
    (err, rows) => {
      if (err) {
        return res.status(500).json({ error: err.message });
      }
      res.json(rows);
    }
  );
});

// Delete old logs
app.delete('/admin/logs', authenticateToken, (req, res) => {
  const daysToKeep = parseInt(req.query.days) || 7;
  const cutoffDate = new Date(Date.now() - daysToKeep * 24 * 60 * 60 * 1000).toISOString();

  db.run('DELETE FROM RequestLogs WHERE Timestamp < ?', [cutoffDate], function(err) {
    if (err) {
      return res.status(400).json({ error: err.message });
    }
    res.json({ success: true, deleted: this.changes });
  });
});

// ==================== PERMISSION ENDPOINTS ====================

// Get all permissions
app.get('/admin/permissions', authenticateToken, (req, res) => {
  db.all('SELECT * FROM Permissions ORDER BY Resource, Action', [], (err, rows) => {
    if (err) {
      return res.status(500).json({ error: err.message });
    }
    res.json(rows);
  });
});

// ==================== METRICS ENDPOINTS ====================

// Get metrics
app.get('/admin/metrics', authenticateToken, (req, res) => {
  const uptime = Math.floor((Date.now() - metrics.startTime) / 1000);
  const avgLatency = metrics.totalRequests > 0
    ? Math.round(metrics.totalLatency / metrics.totalRequests)
    : 0;

  res.json({
    totalRequests: metrics.totalRequests,
    successRequests: metrics.successRequests,
    failedRequests: metrics.failedRequests,
    successRate: metrics.totalRequests > 0
      ? ((metrics.successRequests / metrics.totalRequests) * 100).toFixed(2)
      : 0,
    avgLatency,
    wsConnections: metrics.wsConnections,
    wsMessages: metrics.wsMessages,
    uptime,
    timestamp: new Date().toISOString()
  });
});

// Get stats
app.get('/admin/stats', authenticateToken, (req, res) => {
  const stats = {
    totalRoutes: 0,
    totalClusters: 0,
    totalUsers: 0,
    totalLogs: 0,
    wsConnections: metrics.wsConnections,
    uptime: Math.floor((Date.now() - metrics.startTime) / 1000)
  };

  db.get('SELECT COUNT(*) as count FROM Routes', [], (err, row) => {
    stats.totalRoutes = row ? row.count : 0;

    db.get('SELECT COUNT(*) as count FROM Clusters', [], (err, row) => {
      stats.totalClusters = row ? row.count : 0;

      db.get('SELECT COUNT(*) as count FROM Users', [], (err, row) => {
        stats.totalUsers = row ? row.count : 0;

        db.get('SELECT COUNT(*) as count FROM RequestLogs', [], (err, row) => {
          stats.totalLogs = row ? row.count : 0;
          res.json(stats);
        });
      });
    });
  });
});

// ==================== WEBSOCKET SUPPORT ====================

// WebSocket upgrade handler
server.on('upgrade', (request, socket, head) => {
  const pathname = url.parse(request.url).pathname;

  console.log(`[WS] Upgrade request: ${pathname}`);

  // Find matching route for WebSocket
  db.get('SELECT * FROM Routes WHERE MatchPath = ? AND IsActive = 1', [pathname], (err, route) => {
    if (err || !route) {
      console.log(`[WS] No route found for ${pathname}`);
      socket.destroy();
      return;
    }

    // Get cluster
    db.get('SELECT * FROM Clusters WHERE ClusterId = ? AND IsActive = 1', [route.ClusterId], (err, cluster) => {
      if (err || !cluster) {
        console.log(`[WS] No cluster found for ${route.ClusterId}`);
        socket.destroy();
        return;
      }

      const destinations = JSON.parse(cluster.DestinationsJson);
      const activeDestinations = destinations.filter(d => d.health === 'Active');

      if (activeDestinations.length === 0) {
        console.log(`[WS] No active destinations for ${route.ClusterId}`);
        socket.destroy();
        return;
      }

      const target = activeDestinations[0].address.replace('http://', 'ws://').replace('https://', 'wss://');
      const targetUrl = `${target}${pathname}`;

      console.log(`[WS] Proxying ${pathname} -> ${targetUrl}`);

      // Create WebSocket connection to backend
      const backendWs = new WebSocket(targetUrl);

      backendWs.on('open', () => {
        console.log(`[WS] Connected to backend: ${targetUrl}`);

        // Accept client WebSocket connection
        wss.handleUpgrade(request, socket, head, (clientWs) => {
          metrics.wsConnections++;

          // Forward messages from client to backend
          clientWs.on('message', (message) => {
            metrics.wsMessages++;
            if (backendWs.readyState === WebSocket.OPEN) {
              backendWs.send(message);
            }
          });

          // Forward messages from backend to client
          backendWs.on('message', (message) => {
            if (clientWs.readyState === WebSocket.OPEN) {
              clientWs.send(message);
            }
          });

          // Handle client close
          clientWs.on('close', () => {
            console.log('[WS] Client disconnected');
            metrics.wsConnections--;
            if (backendWs.readyState === WebSocket.OPEN) {
              backendWs.close();
            }
          });

          // Handle backend close
          backendWs.on('close', () => {
            console.log('[WS] Backend disconnected');
            if (clientWs.readyState === WebSocket.OPEN) {
              clientWs.close();
            }
          });

          // Handle errors
          clientWs.on('error', (err) => {
            console.error('[WS] Client error:', err.message);
          });

          backendWs.on('error', (err) => {
            console.error('[WS] Backend error:', err.message);
          });
        });
      });

      backendWs.on('error', (err) => {
        console.error('[WS] Backend connection error:', err.message);
        socket.destroy();
      });
    });
  });
});

// ==================== HTTP PROXY MIDDLEWARE ====================

// Dynamic proxy setup
function setupProxy() {
  db.all('SELECT * FROM Routes WHERE IsActive = 1 ORDER BY LENGTH(MatchPath) DESC', [], (err, routes) => {
    if (err || !routes) {
      console.error('Error loading routes:', err);
      return;
    }

    routes.forEach(route => {
      db.get('SELECT * FROM Clusters WHERE ClusterId = ? AND IsActive = 1',
        [route.ClusterId], (err, cluster) => {
          if (err || !cluster) return;

          const destinations = JSON.parse(cluster.DestinationsJson);
          const activeDestinations = destinations.filter(d => d.health === 'Active');

          if (activeDestinations.length === 0) {
            console.warn(`⚠️  No active destinations for cluster: ${cluster.ClusterId}`);
            return;
          }

          const target = activeDestinations[0].address;

          // Create proxy for this route
          const proxyMiddleware = createProxyMiddleware({
            target: target,
            changeOrigin: true,
            ws: false, // WebSocket handled separately
            pathRewrite: (path) => {
              return path.replace(route.MatchPath, '');
            },
            onProxyReq: (proxyReq) => {
              console.log(`[HTTP] ${proxyReq.method} ${proxyReq.path} -> ${target}`);
            },
            onProxyRes: (proxyRes) => {
              proxyRes.headers['X-Gateway'] = 'Node.js-Gateway';
            },
            onError: (err, req, res) => {
              console.error('[HTTP ERROR]', err.message);
              res.status(502).json({ error: 'Bad Gateway', message: err.message });
            }
          });

          app.use(route.MatchPath, limiter, proxyMiddleware);
          console.log(`✅ HTTP Route: ${route.MatchPath} -> ${target}`);
        });
    });
  });
}

// Setup proxy after database initialization
setTimeout(setupProxy, 1000);

// Health check
app.get('/health', (req, res) => {
  res.json({
    status: 'ok',
    timestamp: new Date().toISOString(),
    uptime: Math.floor((Date.now() - metrics.startTime) / 1000),
    wsConnections: metrics.wsConnections
  });
});

// Start server
server.listen(PORT, '0.0.0.0', () => {
  console.log(`\n🚀 API Gateway running on http://0.0.0.0:${PORT}`);
  console.log(`📊 Admin API: http://0.0.0.0:${PORT}/admin`);
  console.log(`🔐 Login: POST /auth/login`);
  console.log(`🔌 WebSocket: ws://0.0.0.0:${PORT}/ws/*`);
  console.log(`\nDefault credentials: admin / admin123\n`);
});

// Graceful shutdown
process.on('SIGINT', () => {
  console.log('\nShutting down gracefully...');

  // Close all WebSocket connections
  wss.clients.forEach((client) => {
    client.close();
  });

  db.close((err) => {
    if (err) {
      console.error(err.message);
    }
    console.log('Database connection closed');
    process.exit(0);
  });
});
