// Feature: Auth - Service
const { getDatabase } = require('../../core/database');
const { JWT_SECRET, JWT_ACCESS_EXPIRY, JWT_REFRESH_EXPIRY, ACCOUNT_LOCKOUT_ATTEMPTS, ACCOUNT_LOCKOUT_DURATION } = require('../../core/config');
const bcrypt = require('bcryptjs');
const jwt = require('jsonwebtoken');

class AuthService {
  login(username, password, callback) {
    const db = getDatabase();

    db.get('SELECT * FROM Users WHERE Username = ?', [username], (err, user) => {
      if (err || !user) {
        return callback({ status: 401, error: 'Invalid credentials' });
      }

      // Check if account is locked
      if (user.LockedUntil) {
        const lockedUntil = new Date(user.LockedUntil);
        if (lockedUntil > new Date()) {
          return callback({ status: 423, error: 'Account locked', lockedUntil: user.LockedUntil });
        } else {
          // Unlock account
          db.run('UPDATE Users SET FailedLoginAttempts = 0, LockedUntil = NULL WHERE Id = ?', [user.Id]);
        }
      }

      if (!user.IsActive) {
        return callback({ status: 403, error: 'Account disabled' });
      }

      if (!bcrypt.compareSync(password, user.PasswordHash)) {
        // Increment failed attempts
        const failedAttempts = (user.FailedLoginAttempts || 0) + 1;

        if (failedAttempts >= ACCOUNT_LOCKOUT_ATTEMPTS) {
          // Lock account
          const lockedUntil = new Date(Date.now() + ACCOUNT_LOCKOUT_DURATION).toISOString();
          db.run('UPDATE Users SET FailedLoginAttempts = ?, LockedUntil = ? WHERE Id = ?',
            [failedAttempts, lockedUntil, user.Id]);
          return callback({ status: 423, error: 'Account locked due to too many failed attempts' });
        } else {
          db.run('UPDATE Users SET FailedLoginAttempts = ? WHERE Id = ?', [failedAttempts, user.Id]);
          return callback({ status: 401, error: 'Invalid credentials', attemptsLeft: ACCOUNT_LOCKOUT_ATTEMPTS - failedAttempts });
        }
      }

      // Reset failed attempts on successful login
      db.run('UPDATE Users SET FailedLoginAttempts = 0, LockedUntil = NULL WHERE Id = ?', [user.Id]);

      const accessToken = jwt.sign(
        { id: user.Id, username: user.Username, role: user.Role },
        JWT_SECRET,
        { expiresIn: JWT_ACCESS_EXPIRY }
      );

      const refreshToken = jwt.sign(
        { id: user.Id, username: user.Username },
        JWT_SECRET,
        { expiresIn: JWT_REFRESH_EXPIRY }
      );

      // Store refresh token
      const expiresAt = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
      db.run('INSERT INTO RefreshTokens (UserId, Token, ExpiresAt) VALUES (?, ?, ?)',
        [user.Id, refreshToken, expiresAt]);

      callback(null, {
        accessToken,
        refreshToken,
        user: {
          id: user.Id,
          username: user.Username,
          role: user.Role
        }
      });
    });
  }

  refresh(refreshToken, callback) {
    const db = getDatabase();

    if (!refreshToken) {
      return callback({ status: 401, error: 'Refresh token required' });
    }

    db.get('SELECT * FROM RefreshTokens WHERE Token = ?', [refreshToken], (err, tokenRecord) => {
      if (err || !tokenRecord) {
        return callback({ status: 403, error: 'Invalid refresh token' });
      }

      if (new Date(tokenRecord.ExpiresAt) < new Date()) {
        db.run('DELETE FROM RefreshTokens WHERE Token = ?', [refreshToken]);
        return callback({ status: 403, error: 'Refresh token expired' });
      }

      jwt.verify(refreshToken, JWT_SECRET, (err, decoded) => {
        if (err) {
          return callback({ status: 403, error: 'Invalid refresh token' });
        }

        db.get('SELECT * FROM Users WHERE Id = ? AND IsActive = 1', [decoded.id], (err, user) => {
          if (err || !user) {
            return callback({ status: 403, error: 'User not found' });
          }

          const accessToken = jwt.sign(
            { id: user.Id, username: user.Username, role: user.Role },
            JWT_SECRET,
            { expiresIn: JWT_ACCESS_EXPIRY }
          );

          callback(null, { accessToken });
        });
      });
    });
  }

  logout(refreshToken, callback) {
    const db = getDatabase();

    if (refreshToken) {
      db.run('DELETE FROM RefreshTokens WHERE Token = ?', [refreshToken], () => {
        callback(null, { success: true });
      });
    } else {
      callback(null, { success: true });
    }
  }
}

module.exports = new AuthService();
