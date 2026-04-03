// Core database connection (singleton)
const sqlite3 = require('sqlite3').verbose();

let db = null;

function getDatabase() {
  if (!db) {
    db = new sqlite3.Database('./gateway.db', (err) => {
      if (err) {
        console.error('Database connection error:', err);
      } else {
        console.log('✅ Connected to SQLite database');
      }
    });
  }
  return db;
}

function closeDatabase() {
  if (db) {
    db.close((err) => {
      if (err) {
        console.error(err.message);
      }
      console.log('Database connection closed');
    });
  }
}

module.exports = { getDatabase, closeDatabase };
