// Feature: Metrics - Service
const { getDatabase } = require('../../core/database');
const metricsService = require('../../core/metrics');

class MetricsService {
  getMetrics(callback) {
    callback(null, metricsService.getMetrics());
  }

  getStats(callback) {
    const db = getDatabase();
    const stats = {
      totalRoutes: 0,
      totalClusters: 0,
      totalUsers: 0,
      totalLogs: 0,
      wsConnections: metricsService.getMetrics().wsConnections,
      uptime: metricsService.getMetrics().uptime
    };

    db.get('SELECT COUNT(*) as count FROM Routes', [], (err, row) => {
      stats.totalRoutes = row ? row.count : 0;

      db.get('SELECT COUNT(*) as count FROM Clusters', [], (err, row) => {
        stats.totalClusters = row ? row.count : 0;

        db.get('SELECT COUNT(*) as count FROM Users', [], (err, row) => {
          stats.totalUsers = row ? row.count : 0;

          db.get('SELECT COUNT(*) as count FROM RequestLogs', [], (err, row) => {
            stats.totalLogs = row ? row.count : 0;
            callback(null, stats);
          });
        });
      });
    });
  }

  getPermissions(callback) {
    const db = getDatabase();
    db.all('SELECT * FROM Permissions ORDER BY Resource, Action', [], (err, rows) => {
      if (err) {
        return callback({ status: 500, error: err.message });
      }
      callback(null, rows);
    });
  }
}

module.exports = new MetricsService();
