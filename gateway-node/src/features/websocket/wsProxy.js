// Feature: WebSocket - WebSocket Proxy Service
const WebSocket = require('ws');
const url = require('url');
const { getDatabase } = require('../../core/database');
const metricsService = require('../../core/metrics');

function setupWebSocketProxy(server, wss) {
  server.on('upgrade', (request, socket, head) => {
    const pathname = url.parse(request.url).pathname;

    console.log(`[WS] Upgrade request: ${pathname}`);

    const db = getDatabase();

    // Find matching route for WebSocket
    db.get('SELECT * FROM Routes WHERE MatchPath = ? AND IsActive = 1', [pathname], (err, route) => {
      if (err || !route) {
        console.log(`[WS] No route found for ${pathname}`);
        socket.destroy();
        return;
      }

      // Get cluster
      db.get('SELECT * FROM Clusters WHERE ClusterId = ? AND IsActive = 1', [route.ClusterId], (err, cluster) => {
        if (err || !cluster) {
          console.log(`[WS] No cluster found for ${route.ClusterId}`);
          socket.destroy();
          return;
        }

        const destinations = JSON.parse(cluster.DestinationsJson);
        const activeDestinations = destinations.filter(d => d.health === 'Active');

        if (activeDestinations.length === 0) {
          console.log(`[WS] No active destinations for ${route.ClusterId}`);
          socket.destroy();
          return;
        }

        const target = activeDestinations[0].address.replace('http://', 'ws://').replace('https://', 'wss://');
        const targetUrl = `${target}${pathname}`;

        console.log(`[WS] Proxying ${pathname} -> ${targetUrl}`);

        // Create WebSocket connection to backend
        const backendWs = new WebSocket(targetUrl);

        backendWs.on('open', () => {
          console.log(`[WS] Connected to backend: ${targetUrl}`);

          // Accept client WebSocket connection
          wss.handleUpgrade(request, socket, head, (clientWs) => {
            metricsService.incrementWsConnections();

            // Forward messages from client to backend
            clientWs.on('message', (message) => {
              metricsService.incrementWsMessages();
              if (backendWs.readyState === WebSocket.OPEN) {
                backendWs.send(message);
              }
            });

            // Forward messages from backend to client
            backendWs.on('message', (message) => {
              if (clientWs.readyState === WebSocket.OPEN) {
                clientWs.send(message);
              }
            });

            // Handle client close
            clientWs.on('close', () => {
              console.log('[WS] Client disconnected');
              metricsService.decrementWsConnections();
              if (backendWs.readyState === WebSocket.OPEN) {
                backendWs.close();
              }
            });

            // Handle backend close
            backendWs.on('close', () => {
              console.log('[WS] Backend disconnected');
              if (clientWs.readyState === WebSocket.OPEN) {
                clientWs.close();
              }
            });

            // Handle errors
            clientWs.on('error', (err) => {
              console.error('[WS] Client error:', err.message);
            });

            backendWs.on('error', (err) => {
              console.error('[WS] Backend error:', err.message);
            });
          });
        });

        backendWs.on('error', (err) => {
          console.error('[WS] Backend connection error:', err.message);
          socket.destroy();
        });
      });
    });
  });
}

module.exports = { setupWebSocketProxy };
