// Infrastructure: Database initialization
const { getDatabase } = require('../core/database');
const bcrypt = require('bcryptjs');

function initDatabase() {
  const db = getDatabase();

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

    // Seed data
    seedData(db);
  });
}

function seedData(db) {
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
              VALUES (?, ?, ?, ?)`, ['test-route', 'test-cluster', '/test', 0]);

      db.run(`INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond)
              VALUES (?, ?, ?, ?)`, ['api-route', 'test-cluster', '/api', 100]);

      db.run(`INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond)
              VALUES (?, ?, ?, ?)`, ['ws-route', 'test-cluster', '/ws', 0]);
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
}

module.exports = { initDatabase };
