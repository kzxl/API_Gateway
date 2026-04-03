// Feature: Logs - Service
const { getDatabase } = require('../../core/database');

class LogService {
  getAll(limit = 100, offset = 0, callback) {
    const db = getDatabase();

    db.all(`SELECT * FROM RequestLogs ORDER BY Id DESC LIMIT ? OFFSET ?`,
      [limit, offset],
      (err, rows) => {
        if (err) {
          return callback({ status: 500, error: err.message });
        }
        callback(null, rows);
      }
    );
  }

  deleteOld(daysToKeep = 7, callback) {
    const db = getDatabase();
    const cutoffDate = new Date(Date.now() - daysToKeep * 24 * 60 * 60 * 1000).toISOString();

    db.run('DELETE FROM RequestLogs WHERE Timestamp < ?', [cutoffDate], function(err) {
      if (err) {
        return callback({ status: 400, error: err.message });
      }
      callback(null, { success: true, deleted: this.changes });
    });
  }
}

module.exports = new LogService();
