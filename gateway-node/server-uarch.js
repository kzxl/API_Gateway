// Main server file - Universe Architecture with Error Handling
require('dotenv').config();

const express = require('express');
const http = require('http');
const WebSocket = require('ws');
const cors = require('cors');

// Core
const { PORT } = require('./src/core/config');
const { closeDatabase } = require('./src/core/database');
const metricsService = require('./src/core/metrics');

// Infrastructure
const { initDatabase } = require('./src/infrastructure/dbInit');
const loggingMiddleware = require('./src/infrastructure/loggingMiddleware');

// Features
const authRoutes = require('./src/features/auth/authRoutes');
const userRoutes = require('./src/features/users/userRoutes');
const routeRoutes = require('./src/features/routes/routeRoutes');
const clusterRoutes = require('./src/features/clusters/clusterRoutes');
const logRoutes = require('./src/features/logs/logRoutes');
const metricsRoutes = require('./src/features/metrics/metricsRoutes');
const { setupHttpProxy } = require('./src/features/proxy/httpProxy');
const { setupWebSocketProxy } = require('./src/features/websocket/wsProxy');

// Initialize Express app
const app = express();
const server = http.createServer(app);
const wss = new WebSocket.Server({ noServer: true });

// Middleware
app.use(cors());
app.use(express.json());
app.use(loggingMiddleware);

// Global error handler for Express
app.use((err, req, res, next) => {
  console.error('[ERROR]', err.stack);
  res.status(500).json({ error: 'Internal server error', message: err.message });
});

// Initialize database
initDatabase();

// Health check
app.get('/health', (req, res) => {
  res.json({
    status: 'ok',
    timestamp: new Date().toISOString(),
    uptime: metricsService.getMetrics().uptime,
    wsConnections: metricsService.getMetrics().wsConnections
  });
});

// Feature routes
app.use('/auth', authRoutes);
app.use('/admin/users', userRoutes);
app.use('/admin/routes', routeRoutes);
app.use('/admin/clusters', clusterRoutes);
app.use('/admin/logs', logRoutes);
app.use('/admin', metricsRoutes);

// Setup HTTP proxy (after 1 second to allow DB initialization)
setTimeout(() => {
  try {
    setupHttpProxy(app);
  } catch (err) {
    console.error('[PROXY ERROR]', err);
  }
}, 1000);

// Setup WebSocket proxy
try {
  setupWebSocketProxy(server, wss);
} catch (err) {
  console.error('[WS PROXY ERROR]', err);
}

// Start server
server.listen(PORT, '0.0.0.0', () => {
  console.log(`\n🚀 API Gateway running on http://0.0.0.0:${PORT}`);
  console.log(`📊 Admin API: http://0.0.0.0:${PORT}/admin`);
  console.log(`🔐 Login: POST /auth/login`);
  console.log(`🔌 WebSocket: ws://0.0.0.0:${PORT}/ws/*`);
  console.log(`\n✨ Architecture: Universe Architecture (Feature-Based)`);
  console.log(`📁 Features: auth, users, routes, clusters, logs, metrics, proxy, websocket`);
  console.log(`\nDefault credentials: admin / admin123\n`);
});

// Handle server errors
server.on('error', (err) => {
  console.error('[SERVER ERROR]', err);
  if (err.code === 'EADDRINUSE') {
    console.error(`Port ${PORT} is already in use`);
    process.exit(1);
  }
});

// Graceful shutdown
function shutdown() {
  console.log('\nShutting down gracefully...');

  // Close all WebSocket connections
  wss.clients.forEach((client) => {
    try {
      client.close();
    } catch (err) {
      console.error('[WS CLOSE ERROR]', err.message);
    }
  });

  // Close server
  server.close(() => {
    console.log('Server closed');
    closeDatabase();
    process.exit(0);
  });

  // Force exit after 10 seconds
  setTimeout(() => {
    console.error('Forced shutdown after timeout');
    process.exit(1);
  }, 10000);
}

process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);

// Handle uncaught exceptions
process.on('uncaughtException', (err) => {
  console.error('[UNCAUGHT EXCEPTION]', err);
  console.error('Stack:', err.stack);
  // Don't exit immediately, log and continue
});

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason, promise) => {
  console.error('[UNHANDLED REJECTION]', reason);
  console.error('Promise:', promise);
  // Don't exit immediately, log and continue
});
