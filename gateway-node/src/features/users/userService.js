// Feature: Users - Service
const { getDatabase } = require('../../core/database');
const bcrypt = require('bcryptjs');

class UserService {
  getAll(callback) {
    const db = getDatabase();
    db.all('SELECT Id, Username, Role, IsActive, CreatedAt FROM Users', [], (err, rows) => {
      if (err) {
        return callback({ status: 500, error: err.message });
      }
      callback(null, rows);
    });
  }

  create(data, callback) {
    const db = getDatabase();
    const { username, password, role } = data;

    if (!username || !password || !role) {
      return callback({ status: 400, error: 'Username, password, and role are required' });
    }

    const hash = bcrypt.hashSync(password, 10);

    db.run('INSERT INTO Users (Username, PasswordHash, Role) VALUES (?, ?, ?)',
      [username, hash, role],
      function(err) {
        if (err) {
          return callback({ status: 400, error: err.message });
        }
        callback(null, { id: this.lastID, username, role });
      }
    );
  }

  update(id, data, callback) {
    const db = getDatabase();
    const { role, isActive, password } = data;

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
        return callback({ status: 400, error: err.message });
      }
      callback(null, { success: true, changes: this.changes });
    });
  }

  delete(id, callback) {
    const db = getDatabase();

    db.run('DELETE FROM Users WHERE Id = ?', [id], function(err) {
      if (err) {
        return callback({ status: 400, error: err.message });
      }
      callback(null, { success: true, changes: this.changes });
    });
  }
}

module.exports = new UserService();
