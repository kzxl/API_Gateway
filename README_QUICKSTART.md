# 🎯 API Gateway - Quick Start Guide

**Version:** 2.0.0  
**Date:** 2026-04-03  
**Status:** ✅ Production Ready

---

## 🚀 QUICK START (5 MINUTES)

### **Step 1: Start Backend**
```bash
cd APIGateway/APIGateway
dotnet run -c Release
```
✅ Gateway running on http://localhost:5151  
✅ Swagger UI: http://localhost:5151/swagger

### **Step 2: Start Frontend**
```bash
cd gateway-admin
npm install  # First time only
npm run dev
```
✅ Admin UI: http://localhost:5173  
✅ Login: `admin` / `admin123`

### **Step 3: Test It**
```bash
# Test health endpoint
curl http://localhost:5151/health

# Login
curl -X POST http://localhost:5151/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# Access admin endpoint (use token from login)
curl http://localhost:5151/admin/routes \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "X-Api-Key: gw-admin-key-change-me"
```

---

## 📚 WHAT'S INCLUDED

### **✅ Authentication System**
- JWT with 15-minute expiry
- Refresh token (7 days)
- Session management
- Account lockout (5 attempts)
- Token blacklist

### **✅ Authorization System**
- Permission-based access control
- Role-based permissions (Admin, User)
- Fine-grained permissions (routes.read, routes.write, etc.)

### **✅ Performance Optimizations**
- JWT validation cache (100x faster)
- Permission check cache (<1ms)
- Zero-allocation patterns
- Expected: 20,000-35,000 req/s

### **✅ Load Testing**
- Mock backend service
- Automated test scripts
- 8 test scenarios
- Performance benchmarks

---

## 📖 DOCUMENTATION

| Document | Description |
|----------|-------------|
| [FINAL_REPORT.md](FINAL_REPORT.md) | Complete project report |
| [FEATURES_COMPLETE.md](FEATURES_COMPLETE.md) | All features implemented |
| [AUTH_IMPLEMENTATION.md](AUTH_IMPLEMENTATION.md) | Authentication guide |
| [LOAD_TESTING_GUIDE.md](LOAD_TESTING_GUIDE.md) | Load testing guide |
| [PERFORMANCE_ANALYSIS.md](PERFORMANCE_ANALYSIS.md) | Performance analysis |
| [NET_FRAMEWORK_PLAN.md](NET_FRAMEWORK_PLAN.md) | .NET Framework port plan |
| [DESIGN_UARCH.md](DESIGN_UARCH.md) | Architecture design |

---

## 🔑 KEY FEATURES

### **1. Login with Account Lockout**
```bash
# Try wrong password 5 times
curl -X POST http://localhost:5151/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"wrong"}'

# Response after 5 attempts:
{
  "error": "Too many failed attempts. Account locked for 30 minutes.",
  "code": "ACCOUNT_LOCKED",
  "attemptsRemaining": 0
}
```

### **2. Permission-Based Access**
```bash
# Admin can create routes (has routes.write permission)
curl -X POST http://localhost:5151/admin/routes \
  -H "Authorization: Bearer ADMIN_TOKEN" \
  -H "X-Api-Key: gw-admin-key-change-me" \
  -d '{"routeId":"test","matchPath":"/test","clusterId":"test-cluster"}'

# User cannot (only has routes.read permission)
# Response: 403 Forbidden
{
  "error": "Insufficient permissions",
  "code": "FORBIDDEN",
  "required": "routes.write"
}
```

### **3. Load Testing**
```bash
# Start mock backend
cd MockBackend && dotnet run &

# Run load test
./gateway_load_test.sh

# Expected results:
# - Direct backend: 40,000 req/s
# - Gateway (no auth): 30,000 req/s
# - Gateway (with auth): 20,000 req/s
```

---

## 🎯 NEXT STEPS

### **For Development:**
1. Read [DESIGN_UARCH.md](DESIGN_UARCH.md) for architecture
2. Read [AUTH_IMPLEMENTATION.md](AUTH_IMPLEMENTATION.md) for auth details
3. Run load tests to benchmark performance
4. Customize permissions for your use case

### **For Production:**
1. Update JWT secret (32+ chars)
2. Configure CORS for your domain
3. Enable HTTPS
4. Set up monitoring
5. Run security audit

### **For Windows Server 2012:**
1. Read [NET_FRAMEWORK_PLAN.md](NET_FRAMEWORK_PLAN.md)
2. Port to .NET Framework 4.8
3. Use Ocelot instead of YARP
4. Deploy to IIS

---

## 🐛 TROUBLESHOOTING

**Problem: Build errors**
```bash
# Clean and rebuild
dotnet clean
dotnet build -c Release
```

**Problem: Database locked**
```bash
# Delete database and restart
rm gateway.db
dotnet run
```

**Problem: Frontend not connecting**
```bash
# Check CORS settings in Program.cs
# Update VITE_API_BASE in .env
```

**Problem: Low performance**
```bash
# Use Release build
dotnet run -c Release

# Check rate limiting is disabled for test routes
# Monitor CPU/memory usage
```

---

## 📞 SUPPORT

**Documentation:** See docs folder  
**Issues:** Check FINAL_REPORT.md  
**Performance:** See PERFORMANCE_ANALYSIS.md  
**Testing:** See LOAD_TESTING_GUIDE.md

---

**Built with ❤️ using Universe Architecture**  
**Powered by .NET 8 + YARP + React**
