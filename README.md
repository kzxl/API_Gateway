# 🚀 API Gateway - Production Ready

**High-Performance API Gateway with Universe Architecture**

[![Node.js](https://img.shields.io/badge/Node.js-20.x-green.svg)](https://nodejs.org/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Performance](https://img.shields.io/badge/throughput-180k%20req%2Fs-brightgreen.svg)](PERFORMANCE_12CPU.md)

**Version:** 2.1.0  
**Status:** ✅ Production Ready  
**Date:** 2026-04-03

---

## 📋 Overview

Enterprise-grade API Gateway built with **Universe Architecture** principles, supporting HTTP/WebSocket forwarding, authentication, rate limiting, and auto-scaling.

**Key Features:**
- 🚀 **150k-180k req/s** throughput (12 vCPU cluster mode)
- 🔌 **WebSocket** forwarding support
- 🔐 **JWT Authentication** with refresh tokens
- 📊 **Real-time metrics** and monitoring
- 🎯 **Auto-scaling** with PM2 cluster mode
- 🏗️ **Universe Architecture** (feature-based)
- 💾 **SQLite** database (zero config)
- 🖥️ **Windows Server 2012** compatible

---

## 🎯 Quick Start

### **Node.js Version (Recommended for Windows Server 2012)**

```bash
# 1. Install dependencies
cd gateway-node
npm install

# 2. Start gateway (single instance)
npm start

# 3. Or start with PM2 cluster (production)
pm2 start ecosystem.config.js

# 4. Access
# Backend:  http://localhost:8887
# Admin UI: http://localhost:8888
# Login:    admin / admin123
```

### **.NET 8 Version (Windows Server 2016+)**

```bash
# Backend
cd APIGateway/APIGateway
dotnet run -c Release

# Admin UI
cd gateway-admin
npm run dev

# Login: admin / admin123
```

---

## 📊 Performance

### **Node.js (PM2 Cluster Mode)**

**Single Instance:**
```
Throughput:     10,000 - 15,000 req/s
Latency (p50):  5-10ms
Memory:         50-100 MB
```

**Cluster Mode (12 vCPU):**
```
Throughput:     150,000 - 180,000 req/s
Latency (p50):  5-10ms
Memory:         1.2 - 1.5 GB (12 instances)
CPU:            95-100%
```

### **.NET 8 (YARP)**

```
Throughput:     120,000 - 150,000 req/s
Latency (p50):  0.8ms
Memory:         500 MB - 1 GB
```

**See:** [BACKEND_COMPARISON.md](BACKEND_COMPARISON.md) | [PERFORMANCE_12CPU.md](PERFORMANCE_12CPU.md)

---

## 🏗️ Architecture

### **Node.js - Universe Architecture (Feature-Based)**

```
gateway-node/
├── server-uarch.js              # Main entry point
├── ecosystem.config.js          # PM2 cluster config
└── src/
    ├── core/                    # Core services (singleton)
    │   ├── config.js
    │   ├── database.js
    │   └── metrics.js
    ├── infrastructure/          # Cross-cutting concerns
    │   ├── dbInit.js
    │   ├── loggingMiddleware.js
    │   └── authMiddleware.js
    └── features/                # Feature modules
        ├── auth/               # Authentication
        ├── users/              # User management
        ├── routes/             # Route management
        ├── clusters/           # Cluster management
        ├── logs/               # Log management
        ├── metrics/            # Metrics & stats
        ├── proxy/              # HTTP proxy
        └── websocket/          # WebSocket proxy
```

### **.NET 8 - Universe Architecture**

```
APIGateway/
├── Program.cs                   # Main entry point
├── Features/                    # Feature modules
│   ├── Auth/
│   ├── Routing/
│   ├── Clustering/
│   └── Monitoring/
├── Infrastructure/              # Middleware
└── Core/                        # Interfaces & Constants
```

---

## ✨ Features

### **Authentication**
- ✅ JWT access tokens (15 min expiry)
- ✅ Refresh token rotation (7 days)
- ✅ Account lockout (5 attempts, 30 min)
- ✅ Password hashing (bcrypt)
- ✅ Session management

### **Routing**
- ✅ Dynamic HTTP routing
- ✅ WebSocket forwarding
- ✅ Path-based matching
- ✅ Load balancing (round-robin)
- ✅ Rate limiting

### **Management**
- ✅ User management (CRUD)
- ✅ Route management (CRUD)
- ✅ Cluster management (CRUD)
- ✅ Request logging
- ✅ Real-time metrics
- ✅ Permission-based access control

### **Operations**
- ✅ Auto-scaling (PM2 cluster)
- ✅ Auto-restart on crash
- ✅ Zero-downtime reload
- ✅ Graceful shutdown
- ✅ Health checks
- ✅ Error handling

---

## 🔌 API Endpoints

### **Authentication**
```
POST   /auth/login          - Login
POST   /auth/refresh        - Refresh token
POST   /auth/logout         - Logout
```

### **Management (requires auth)**
```
GET    /admin/users         - List users
POST   /admin/users         - Create user
PUT    /admin/users/:id     - Update user
DELETE /admin/users/:id     - Delete user

GET    /admin/routes        - List routes
POST   /admin/routes        - Create route
PUT    /admin/routes/:id    - Update route
DELETE /admin/routes/:id    - Delete route

GET    /admin/clusters      - List clusters
POST   /admin/clusters      - Create cluster
PUT    /admin/clusters/:id  - Update cluster
DELETE /admin/clusters/:id  - Delete cluster

GET    /admin/logs          - Get logs
DELETE /admin/logs          - Delete old logs

GET    /admin/metrics       - Get metrics
GET    /admin/stats         - Get stats
GET    /admin/permissions   - Get permissions
```

### **Health Check**
```
GET    /health              - Health check
```

---

## 🚀 Deployment

### **Node.js - Production Deployment**

```bash
# 1. Install PM2
npm install -g pm2

# 2. Start cluster (auto-scale to CPU cores)
cd gateway-node
pm2 start ecosystem.config.js

# 3. Save configuration
pm2 save

# 4. Auto-start on boot
pm2 startup
```

### **.NET 8 - Production Deployment**

```bash
# Publish
cd APIGateway/APIGateway
dotnet publish -c Release -o ../../publish/backend

# Run
cd ../../publish/backend
./APIGateway
```

### **Admin UI - Production Build**

```bash
cd gateway-admin
npm run build

# Deploy dist/ to nginx or static hosting
```

**See:** [DEPLOYMENT.md](gateway-node/DEPLOYMENT.md) | [DEPLOYMENT_PACKAGE.md](DEPLOYMENT_PACKAGE.md)

---

## 📈 Auto-Scaling (Node.js)

### **PM2 Cluster Mode**

```javascript
// ecosystem.config.js
module.exports = {
  apps: [{
    name: 'gateway-node',
    script: './server-uarch.js',
    instances: 'max',        // Use all CPU cores
    exec_mode: 'cluster',    // Enable cluster mode
    max_memory_restart: '500M'
  }]
};
```

### **Scaling Commands**

```bash
# Scale to 8 instances
pm2 scale gateway-node 8

# Scale to max CPU cores
pm2 scale gateway-node max

# Reload without downtime
pm2 reload gateway-node

# Monitor
pm2 monit
```

**See:** [AUTO_SCALING.md](gateway-node/AUTO_SCALING.md)

---

## 🔧 Configuration

### **Node.js (.env)**

```env
PORT=8887
JWT_SECRET=your-secret-key-min-32-chars
NODE_ENV=production
```

### **.NET 8 (appsettings.json)**

```json
{
  "ConnectionStrings": {
    "GatewayDb": "Data Source=gateway.db"
  },
  "Jwt": {
    "Secret": "your-secret-key",
    "Issuer": "APIGateway",
    "Audience": "GatewayClients"
  }
}
```

---

## 📊 Monitoring

### **PM2 Monitoring (Node.js)**

```bash
# Real-time monitoring
pm2 monit

# Process list
pm2 list

# Logs
pm2 logs gateway-node

# Metrics
pm2 describe gateway-node
```

### **Application Metrics**

```bash
curl -H "Authorization: Bearer <token>" \
  http://localhost:8887/admin/metrics
```

**Response:**
```json
{
  "totalRequests": 1000000,
  "successRequests": 950000,
  "failedRequests": 50000,
  "successRate": "95.00",
  "avgLatency": 8,
  "wsConnections": 100,
  "wsMessages": 50000,
  "uptime": 86400
}
```

---

## 🧪 Testing

### **Health Check**

```bash
curl http://localhost:8887/health
```

### **Login**

```bash
curl -X POST http://localhost:8887/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'
```

### **WebSocket Test**

Open `websocket-test.html` in browser or use wscat:

```bash
wscat -c ws://localhost:8887/ws/echo
```

---

## 📚 Documentation

### **Deployment & Operations**
- [DEPLOYMENT.md](gateway-node/DEPLOYMENT.md) - Full deployment guide
- [AUTO_SCALING.md](gateway-node/AUTO_SCALING.md) - PM2 cluster mode
- [DEPLOYMENT_PACKAGE.md](DEPLOYMENT_PACKAGE.md) - Complete package

### **Performance & Analysis**
- [BACKEND_COMPARISON.md](BACKEND_COMPARISON.md) - .NET 8 vs Go vs Node.js
- [PERFORMANCE_12CPU.md](PERFORMANCE_12CPU.md) - 12 vCPU analysis
- [MINIMAL_ROUTING_PERFORMANCE.md](MINIMAL_ROUTING_PERFORMANCE.md) - Routing performance
- [GO_BACKEND_ANALYSIS.md](GO_BACKEND_ANALYSIS.md) - Go implementation

### **Features & Capabilities**
- [WEBSOCKET_SUPPORT.md](WEBSOCKET_SUPPORT.md) - WebSocket capabilities
- [FEATURES_COMPLETE.md](FEATURES_COMPLETE.md) - Feature list
- [AUTH_IMPLEMENTATION.md](AUTH_IMPLEMENTATION.md) - Auth guide

### **.NET 8 Specific**
- [LOAD_TESTING_GUIDE.md](LOAD_TESTING_GUIDE.md) - Load testing
- [DESIGN_UARCH.md](DESIGN_UARCH.md) - Architecture design

---

## 🏆 Performance Comparison

| Backend | Throughput (12 CPU) | Memory | Latency | Windows 2012 |
|---------|---------------------|--------|---------|--------------|
| **Node.js (Cluster)** | 150k-180k req/s | 1.2 GB | 5-10ms | ✅ |
| **Go (fasthttp)** | 200k-250k req/s | 100 MB | 0.3ms | ✅ |
| **.NET 8 (YARP)** | 150k-200k req/s | 1 GB | 0.8ms | ❌ |

**Recommendation:**
- **Windows Server 2012:** Node.js (best compatibility)
- **Windows Server 2016+:** .NET 8 (best developer experience)
- **Maximum Performance:** Go (fastest, lowest memory)

**See:** [BACKEND_COMPARISON.md](BACKEND_COMPARISON.md)

---

## 🛡️ Security

- ✅ JWT authentication (15 min expiry)
- ✅ Refresh token rotation (7 days)
- ✅ Account lockout protection (5 attempts, 30 min)
- ✅ Password hashing (bcrypt)
- ✅ CORS configuration
- ✅ Rate limiting (100 req/s default)
- ✅ Input validation
- ✅ SQL injection prevention (parameterized queries)
- ✅ Token blacklist
- ✅ Session management

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

---

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details

---

## 🙏 Acknowledgments

- Built with [Universe Architecture](https://github.com/anthropics/universe-architecture) principles
- Powered by [Node.js](https://nodejs.org/) and [Express](https://expressjs.com/)
- Process management by [PM2](https://pm2.keymetrics.io/)
- Admin UI built with [React](https://react.dev/) and [Ant Design](https://ant.design/)
- .NET version powered by [YARP](https://microsoft.github.io/reverse-proxy/)

---

## 📞 Support

For issues and questions:
- 📖 Check [Documentation](gateway-node/DEPLOYMENT.md)
- 🐛 Report bugs via GitHub Issues
- 💬 Discussions via GitHub Discussions

---

**Made with ❤️ using Universe Architecture**

**Status:** ✅ Production Ready  
**Version:** 2.1.0  
**Last Updated:** 2026-04-03
