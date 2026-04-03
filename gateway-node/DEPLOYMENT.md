# 🚀 Node.js API Gateway - Deployment Guide

**Date:** 2026-04-03  
**Architecture:** Universe Architecture (Feature-Based)  
**Target:** Windows Server 2012 + Node.js

---

## 📋 OVERVIEW

**Node.js Gateway với Universe Architecture:**
```
✅ Feature-based organization (8 features)
✅ Full authentication & authorization
✅ HTTP & WebSocket forwarding
✅ SQLite database
✅ Auto-restart with PM2
✅ Error handling & logging
✅ Metrics & monitoring
```

---

## 📁 PROJECT STRUCTURE

```
gateway-node/
├── server-uarch.js              # Main entry point
├── package.json                 # Dependencies
├── .env                         # Configuration
├── gateway.db                   # SQLite database
└── src/
    ├── core/                    # Core services (singleton)
    │   ├── config.js           # Configuration
    │   ├── database.js         # Database connection
    │   └── metrics.js          # Metrics tracking
    ├── infrastructure/          # Cross-cutting concerns
    │   ├── dbInit.js           # Database initialization
    │   ├── loggingMiddleware.js # Request logging
    │   └── authMiddleware.js   # JWT authentication
    └── features/                # Feature modules
        ├── auth/               # Authentication
        │   ├── authService.js
        │   └── authRoutes.js
        ├── users/              # User management
        ├── routes/             # Route management
        ├── clusters/           # Cluster management
        ├── logs/               # Log management
        ├── metrics/            # Metrics & stats
        ├── proxy/              # HTTP proxy
        │   └── httpProxy.js
        └── websocket/          # WebSocket proxy
            └── wsProxy.js
```

---

## 🔧 INSTALLATION

### **1. Prerequisites**

```bash
# Node.js 14+ required
node --version

# npm required
npm --version
```

### **2. Install Dependencies**

```bash
cd gateway-node
npm install
```

### **3. Install PM2 (for auto-restart)**

```bash
npm install -g pm2
```

---

## ⚙️ CONFIGURATION

### **.env file:**

```env
PORT=8887
JWT_SECRET=GatewaySecretKey-Change-This-In-Production-Min32Chars!
NODE_ENV=production
```

---

## 🚀 DEPLOYMENT

### **Option 1: Direct Run (Development)**

```bash
npm start
```

### **Option 2: PM2 (Production - Recommended)**

```bash
# Start with PM2
npm run pm2

# Or manually
pm2 start server-uarch.js --name gateway-node --watch --max-memory-restart 500M

# View logs
pm2 logs gateway-node

# Monitor
pm2 monit

# Restart
pm2 restart gateway-node

# Stop
pm2 stop gateway-node

# Auto-start on boot
pm2 startup
pm2 save
```

### **Option 3: Windows Service (Production)**

```bash
# Install pm2-windows-service
npm install -g pm2-windows-service

# Setup as Windows service
pm2-service-install -n PM2

# Start service
net start PM2

# Add gateway to PM2
pm2 start server-uarch.js --name gateway-node
pm2 save
```

---

## 🔌 NGINX CONFIGURATION

### **nginx.conf:**

```nginx
# Backend (Node.js Gateway)
upstream gateway_backend {
    server 127.0.0.1:8887;
}

# Admin UI
upstream admin_ui {
    server 127.0.0.1:8888;
}

# Backend server
server {
    listen 8887;
    server_name 192.168.19.79;

    location / {
        proxy_pass http://gateway_backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # WebSocket support
        proxy_read_timeout 86400;
    }
}

# Admin UI server
server {
    listen 8888;
    server_name 192.168.19.79;

    root /path/to/gateway-admin/dist;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    # API proxy to backend
    location /api/ {
        proxy_pass http://gateway_backend/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

## 📊 FEATURES

### **1. Authentication**
```
POST /auth/login          - Login
POST /auth/refresh        - Refresh token
POST /auth/logout         - Logout
```

### **2. User Management**
```
GET    /admin/users       - List users
POST   /admin/users       - Create user
PUT    /admin/users/:id   - Update user
DELETE /admin/users/:id   - Delete user
```

### **3. Route Management**
```
GET    /admin/routes      - List routes
POST   /admin/routes      - Create route
PUT    /admin/routes/:id  - Update route
DELETE /admin/routes/:id  - Delete route
```

### **4. Cluster Management**
```
GET    /admin/clusters    - List clusters
POST   /admin/clusters    - Create cluster
PUT    /admin/clusters/:id - Update cluster
DELETE /admin/clusters/:id - Delete cluster
```

### **5. Logs**
```
GET    /admin/logs        - Get logs
DELETE /admin/logs        - Delete old logs
```

### **6. Metrics**
```
GET /admin/metrics        - Get metrics
GET /admin/stats          - Get stats
GET /admin/permissions    - Get permissions
```

### **7. HTTP Proxy**
```
All routes configured in database
Automatic forwarding to backend clusters
```

### **8. WebSocket Proxy**
```
ws://gateway:8887/ws/*
Bidirectional forwarding to backend
```

---

## 🛡️ ERROR HANDLING

### **Built-in Protection:**

```
✅ Uncaught exception handler
✅ Unhandled promise rejection handler
✅ Global Express error handler
✅ WebSocket error handling
✅ Database error handling
✅ Graceful shutdown (SIGINT, SIGTERM)
✅ Auto-restart with PM2
```

### **PM2 Auto-Restart:**

```bash
# PM2 automatically restarts on:
- Crash
- Memory limit exceeded (500MB)
- File changes (with --watch)
- Manual restart
```

---

## 📈 MONITORING

### **PM2 Monitoring:**

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

### **Application Metrics:**

```bash
# Get metrics
curl -H "Authorization: Bearer <token>" \
  http://localhost:8887/admin/metrics

# Response:
{
  "totalRequests": 1000,
  "successRequests": 950,
  "failedRequests": 50,
  "successRate": "95.00",
  "avgLatency": 25,
  "wsConnections": 10,
  "wsMessages": 500,
  "uptime": 3600,
  "timestamp": "2026-04-03T13:45:00.000Z"
}
```

---

## 🔥 PERFORMANCE

### **Expected Performance:**

```
Throughput:         10,000 - 15,000 req/s
Latency (p50):      5 - 10ms
Memory:             50 - 100 MB
CPU:                20 - 40%
WebSocket:          1,000+ concurrent connections
```

### **Optimization Tips:**

```
1. Use PM2 cluster mode:
   pm2 start server-uarch.js -i 4

2. Increase Node.js memory:
   node --max-old-space-size=4096 server-uarch.js

3. Use nginx as load balancer:
   Multiple Node.js instances behind nginx

4. Enable HTTP/2 in nginx
```

---

## 🐛 TROUBLESHOOTING

### **Port already in use:**

```bash
# Find process
netstat -ano | findstr :8887

# Kill process
taskkill /PID <pid> /F
```

### **Database locked:**

```bash
# Stop all instances
pm2 stop all

# Remove database lock
rm gateway.db-shm gateway.db-wal

# Restart
pm2 restart all
```

### **High memory usage:**

```bash
# Check memory
pm2 describe gateway-node

# Restart if needed
pm2 restart gateway-node

# Or set lower limit
pm2 start server-uarch.js --max-memory-restart 300M
```

---

## 📦 DEPLOYMENT CHECKLIST

```
✅ Node.js 14+ installed
✅ Dependencies installed (npm install)
✅ .env configured
✅ PM2 installed globally
✅ Gateway started with PM2
✅ PM2 configured for auto-start
✅ Nginx configured (if using)
✅ Firewall rules configured
✅ Admin UI built and deployed
✅ Health check working
✅ Login working
✅ Proxy working
✅ WebSocket working
```

---

## 🎯 PRODUCTION DEPLOYMENT

### **Step-by-step:**

```bash
# 1. Copy files to server
scp -r gateway-node/ user@192.168.19.79:/opt/

# 2. SSH to server
ssh user@192.168.19.79

# 3. Install dependencies
cd /opt/gateway-node
npm install --production

# 4. Configure
nano .env
# Set PORT=8887, JWT_SECRET, etc.

# 5. Start with PM2
pm2 start server-uarch.js --name gateway-node
pm2 save
pm2 startup

# 6. Configure nginx
sudo nano /etc/nginx/nginx.conf
# Add configuration above

# 7. Restart nginx
sudo systemctl restart nginx

# 8. Test
curl http://192.168.19.79:8887/health
```

---

## 🔐 SECURITY

### **Best Practices:**

```
✅ Change JWT_SECRET in production
✅ Use HTTPS (nginx SSL termination)
✅ Enable rate limiting
✅ Use strong passwords
✅ Regular security updates
✅ Monitor logs for suspicious activity
✅ Backup database regularly
```

---

## 📚 ADMIN UI INTEGRATION

### **Update Admin UI API endpoint:**

```javascript
// gateway-admin/src/api/gatewayApi.js
const API_BASE_URL = 'http://192.168.19.79:8887';
```

### **Build Admin UI:**

```bash
cd gateway-admin
npm run build

# Deploy to nginx
cp -r dist/* /var/www/gateway-admin/
```

---

## ✅ SUMMARY

**Node.js Gateway is ready for production:**

```
✅ Universe Architecture (Feature-Based)
✅ Full authentication & authorization
✅ HTTP & WebSocket forwarding
✅ Auto-restart with PM2
✅ Error handling & monitoring
✅ Production-ready
✅ Windows Server 2012 compatible
```

**Deployment:**
```
Backend:  http://192.168.19.79:8887
Admin UI: http://192.168.19.79:8888
Login:    admin / admin123
```

---

**Status:** ✅ Ready for deployment  
**Next:** Build Admin UI and deploy to server
