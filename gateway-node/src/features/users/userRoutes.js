// Feature: Users - Routes
const express = require('express');
const userService = require('./userService');
const authenticateToken = require('../../infrastructure/authMiddleware');

const router = express.Router();

// Get all users
router.get('/', authenticateToken, (req, res) => {
  userService.getAll((err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Create user
router.post('/', authenticateToken, (req, res) => {
  userService.create(req.body, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Update user
router.put('/:id', authenticateToken, (req, res) => {
  userService.update(req.params.id, req.body, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

// Delete user
router.delete('/:id', authenticateToken, (req, res) => {
  userService.delete(req.params.id, (err, result) => {
    if (err) {
      return res.status(err.status).json({ error: err.error });
    }
    res.json(result);
  });
});

module.exports = router;
