// Infrastructure: Logging middleware
const { getDatabase } = require('../core/database');
const metricsService = require('../core/metrics');

function loggingMiddleware(req, res, next) {
  const start = Date.now();

  res.on('finish', () => {
    const latency = Date.now() - start;

    metricsService.incrementRequests();
    metricsService.addLatency(latency);

    if (res.statusCode < 400) {
      metricsService.incrementSuccess();
    } else {
      metricsService.incrementFailed();
    }

    // Log to database (async, don't block)
    const db = getDatabase();
    const clientIp = req.ip || req.connection.remoteAddress;

    db.run(`INSERT INTO RequestLogs (Method, Path, StatusCode, LatencyMs, ClientIp, UserAgent)
            VALUES (?, ?, ?, ?, ?, ?)`,
      [req.method, req.path, res.statusCode, latency, clientIp, req.get('user-agent') || '']);
  });

  next();
}

module.exports = loggingMiddleware;
