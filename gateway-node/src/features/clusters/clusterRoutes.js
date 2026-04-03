// Feature: Clusters - Routes
const express = require('express');
const clusterService = require('./clusterService');
const authenticateToken = require('../../infrastructure/authMiddleware');

const router = express.Router();

// Get all clusters
router.get('/', authenticateToken, (req, res) => {
  clusterService.getAll((err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Create cluster
router.post('/', authenticateToken, (req, res) => {
  clusterService.create(req.body, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Update cluster
router.put('/:id', authenticateToken, (req, res) => {
  clusterService.update(req.params.id, req.body, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Delete cluster
router.delete('/:id', authenticateToken, (req, res) => {
  clusterService.delete(req.params.id, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

module.exports = router;
