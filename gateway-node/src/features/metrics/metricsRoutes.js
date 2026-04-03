// Feature: Metrics - Routes
const express = require('express');
const metricsService = require('./metricsService');
const authenticateToken = require('../../infrastructure/authMiddleware');

const router = express.Router();

// Get metrics
router.get('/metrics', authenticateToken, (req, res) => {
  metricsService.getMetrics((err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Get stats
router.get('/stats', authenticateToken, (req, res) => {
  metricsService.getStats((err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Get permissions
router.get('/permissions', authenticateToken, (req, res) => {
  metricsService.getPermissions((err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

module.exports = router;
