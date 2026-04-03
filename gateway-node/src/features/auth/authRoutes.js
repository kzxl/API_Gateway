// Feature: Auth - Routes
const express = require('express');
const authService = require('./authService');

const router = express.Router();

// Login
router.post('/login', (req, res) => {
  const { username, password } = req.body;

  authService.login(username, password, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error, attemptsLeft: err.attemptsLeft, lockedUntil: err.lockedUntil });
    }
    res.json(result);
  });
});

// Refresh token
router.post('/refresh', (req, res) => {
  const { refreshToken } = req.body;

  authService.refresh(refreshToken, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Logout
router.post('/logout', (req, res) => {
  const { refreshToken } = req.body;

  authService.logout(refreshToken, (err, result) => {
    if (err) {
      return res.status(500).json({ error: err.error });
    }
    res.json(result);
  });
});

module.exports = router;
