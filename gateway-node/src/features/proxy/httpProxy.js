// Feature: Proxy - HTTP Proxy Service
const { createProxyMiddleware } = require('http-proxy-middleware');
const { getDatabase } = require('../../core/database');
const rateLimit = require('express-rate-limit');
const { RATE_LIMIT_WINDOW, RATE_LIMIT_MAX } = require('../../core/config');

// Rate limiter
const limiter = rateLimit({
  windowMs: RATE_LIMIT_WINDOW,
  max: RATE_LIMIT_MAX,
  message: { error: 'Too many requests' }
});

function setupHttpProxy(app) {
  const db = getDatabase();

  db.all('SELECT * FROM Routes WHERE IsActive = 1 ORDER BY LENGTH(MatchPath) DESC', [], (err, routes) => {
    if (err || !routes) {
      console.error('Error loading routes:', err);
      return;
    }

    routes.forEach(route => {
      db.get('SELECT * FROM Clusters WHERE ClusterId = ? AND IsActive = 1',
        [route.ClusterId], (err, cluster) => {
          if (err || !cluster) return;

          const destinations = JSON.parse(cluster.DestinationsJson);
          const activeDestinations = destinations.filter(d => d.health === 'Active');

          if (activeDestinations.length === 0) {
            console.warn(`⚠️  No active destinations for cluster: ${cluster.ClusterId}`);
            return;
          }

          const target = activeDestinations[0].address;

          // Create proxy for this route
          const proxyMiddleware = createProxyMiddleware({
            target: target,
            changeOrigin: true,
            ws: false, // WebSocket handled separately
            pathRewrite: (path) => {
              return path.replace(route.MatchPath, '');
            },
            onProxyReq: (proxyReq) => {
              console.log(`[HTTP] ${proxyReq.method} ${proxyReq.path} -> ${target}`);
            },
            onProxyRes: (proxyRes) => {
              proxyRes.headers['X-Gateway'] = 'Node.js-Gateway';
            },
            onError: (err, req, res) => {
              console.error('[HTTP ERROR]', err.message);
              res.status(502).json({ error: 'Bad Gateway', message: err.message });
            }
          });

          app.use(route.MatchPath, limiter, proxyMiddleware);
          console.log(`✅ HTTP Route: ${route.MatchPath} -> ${target}`);
        });
    });
  });
}

module.exports = { setupHttpProxy };
