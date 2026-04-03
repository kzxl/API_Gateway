// Feature: Routes - Routes
const express = require('express');
const routeService = require('./routeService');
const authenticateToken = require('../../infrastructure/authMiddleware');

const router = express.Router();

// Get all routes
router.get('/', authenticateToken, (req, res) => {
  routeService.getAll((err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Create route
router.post('/', authenticateToken, (req, res) => {
  routeService.create(req.body, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Update route
router.put('/:id', authenticateToken, (req, res) => {
  routeService.update(req.params.id, req.body, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Delete route
router.delete('/:id', authenticateToken, (req, res) => {
  routeService.delete(req.params.id, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

module.exports = router;
