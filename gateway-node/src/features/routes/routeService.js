// Feature: Routes - Service
const { getDatabase } = require('../../core/database');

class RouteService {
  getAll(callback) {
    const db = getDatabase();
    db.all('SELECT * FROM Routes ORDER BY Id DESC', [], (err, rows) => {
      if (err) {
        return callback({ status: 500, error: err.message });
      }
      callback(null, rows);
    });
  }

  create(data, callback) {
    const db = getDatabase();
    const { routeId, clusterId, matchPath, rateLimitPerSecond } = data;

    db.run(`INSERT INTO Routes (RouteId, ClusterId, MatchPath, RateLimitPerSecond)
            VALUES (?, ?, ?, ?)`,
      [routeId, clusterId, matchPath, rateLimitPerSecond || 0],
      function(err) {
        if (err) {
          return callback({ status: 400, error: err.message });
        }
        callback(null, { id: this.lastID, routeId, clusterId, matchPath, rateLimitPerSecond });
      }
    );
  }

  update(id, data, callback) {
    const db = getDatabase();
    const { clusterId, matchPath, rateLimitPerSecond, isActive } = data;

    db.run(`UPDATE Routes SET ClusterId = ?, MatchPath = ?, RateLimitPerSecond = ?, IsActive = ?
            WHERE Id = ?`,
      [clusterId, matchPath, rateLimitPerSecond, isActive ? 1 : 0, id],
      function(err) {
        if (err) {
          return callback({ status: 400, error: err.message });
        }
        callback(null, { success: true, changes: this.changes });
      }
    );
  }

  delete(id, callback) {
    const db = getDatabase();

    db.run('DELETE FROM Routes WHERE Id = ?', [id], function(err) {
      if (err) {
        return callback({ status: 400, error: err.message });
      }
      callback(null, { success: true, changes: this.changes });
    });
  }
}

module.exports = new RouteService();
