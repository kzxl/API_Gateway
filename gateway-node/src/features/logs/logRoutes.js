// Feature: Logs - Routes
const express = require('express');
const logService = require('./logService');
const authenticateToken = require('../../infrastructure/authMiddleware');

const router = express.Router();

// Get logs
router.get('/', authenticateToken, (req, res) => {
  const limit = parseInt(req.query.limit) || 100;
  const offset = parseInt(req.query.offset) || 0;

  logService.getAll(limit, offset, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Delete old logs
router.delete('/', authenticateToken, (req, res) => {
  const daysToKeep = parseInt(req.query.days) || 7;

  logService.deleteOld(daysToKeep, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

module.exports = router;
