// Feature: Clusters - Service
const { getDatabase } = require('../../core/database');

class ClusterService {
  getAll(callback) {
    const db = getDatabase();
    db.all('SELECT * FROM Clusters ORDER BY Id DESC', [], (err, rows) => {
      if (err) {
        return callback({ status: 500, error: err.message });
      }
      // Parse DestinationsJson for each cluster
      const clusters = rows.map(cluster => ({
        ...cluster,
        Destinations: JSON.parse(cluster.DestinationsJson)
      }));
      callback(null, clusters);
    });
  }

  create(data, callback) {
    const db = getDatabase();
    const { clusterId, destinations, loadBalancingPolicy } = data;
    const destinationsJson = JSON.stringify(destinations);

    db.run(`INSERT INTO Clusters (ClusterId, DestinationsJson, LoadBalancingPolicy)
            VALUES (?, ?, ?)`,
      [clusterId, destinationsJson, loadBalancingPolicy || 'RoundRobin'],
      function(err) {
        if (err) {
          return callback({ status: 400, error: err.message });
        }
        callback(null, { id: this.lastID, clusterId, destinations, loadBalancingPolicy });
      }
    );
  }

  update(id, data, callback) {
    const db = getDatabase();
    const { destinations, loadBalancingPolicy, isActive } = data;
    const destinationsJson = JSON.stringify(destinations);

    db.run(`UPDATE Clusters SET DestinationsJson = ?, LoadBalancingPolicy = ?, IsActive = ?
            WHERE Id = ?`,
      [destinationsJson, loadBalancingPolicy, isActive ? 1 : 0, id],
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

    db.run('DELETE FROM Clusters WHERE Id = ?', [id], function(err) {
      if (err) {
        return callback({ status: 400, error: err.message });
      }
      callback(null, { success: true, changes: this.changes });
    });
  }
}

module.exports = new ClusterService();
