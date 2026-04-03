# 📊 Performance Analysis & Req/s Estimation

## 🎯 HIỆN TRẠNG HIỆU SUẤT

### **Theo README.md - Đã đạt được:**
```
Total Requests:     53,073
Throughput:         5,280.46 req/s  🚀
Success (2xx):      1,000
Rate Limited (429): 52,073
Errors (5xx):       0
```

**Điều kiện test:**
- 1000 concurrent threads
- 10 seconds duration
- Rate limit: 100 req/s per IP
- Localhost testing (no network latency)

---

## 🔍 PHÂN TÍCH PERFORMANCE SAU KHI THÊM AUTH

### **1. Middleware Pipeline - Thêm Overhead**

**TRƯỚC (Original):**
```
Request → MetricsMiddleware → GatewayProtectionMiddleware → YARP Proxy
          ↓ ~0.1ms            ↓ ~1-2ms                      ↓ ~1-5ms
```

**SAU (With Auth):**
```
Request → MetricsMiddleware → GatewayProtectionMiddleware → JwtValidationMiddleware → Authentication → Authorization → YARP Proxy
          ↓ ~0.1ms            ↓ ~1-2ms                      ↓ ~0.5ms                 ↓ ~1ms          ↓ ~0.2ms        ↓ ~1-5ms
```

**Overhead mới:**
- JwtValidationMiddleware: ~0.5ms (in-memory blacklist lookup)
- Authentication: ~1ms (JWT validation)
- Authorization: ~0.2ms (role check)
- **Total overhead: ~1.7ms**

---

## 📈 DỰ ĐOÁN REQ/S SAU KHI THÊM AUTH

### **Scenario 1: Admin Endpoints (Có Auth)**

**Latency breakdown:**
```
Metrics:              0.1ms
GatewayProtection:    1.5ms (rate limit + circuit breaker)
JwtValidation:        0.5ms (blacklist check)
Authentication:       1.0ms (JWT decode + validate)
Authorization:        0.2ms (role check)
Controller:           0.5ms (thin adapter)
Service:              2.0ms (L1 cache lookup)
─────────────────────────────
Total:                5.8ms per request
```

**Throughput calculation:**
```
1 thread = 1000ms / 5.8ms = 172 req/s
100 threads = 172 * 100 = 17,200 req/s (theoretical max)

Realistic (with contention): ~12,000-15,000 req/s
```

**Bottlenecks:**
- Database connection pool (SQLite single-writer)
- Memory cache contention
- Thread pool saturation

---

### **Scenario 2: Proxy Endpoints (Có Auth + Rate Limit)**

**Latency breakdown:**
```
Metrics:              0.1ms
GatewayProtection:    1.5ms (rate limit check)
JwtValidation:        0.5ms (blacklist check)
Authentication:       1.0ms (JWT validation)
Authorization:        0.2ms
YARP Proxy:           2.0ms (backend latency)
─────────────────────────────
Total:                5.3ms per request
```

**Throughput với rate limit 100 req/s:**
```
Enforced limit: 100 req/s per IP
Multiple IPs:   100 * N (N = số IP khác nhau)

Example:
- 10 IPs:  1,000 req/s
- 100 IPs: 10,000 req/s
```

---

### **Scenario 3: Public Endpoints (Không Auth)**

**Latency breakdown:**
```
Metrics:              0.1ms
GatewayProtection:    1.5ms
YARP Proxy:           2.0ms
─────────────────────────────
Total:                3.6ms per request
```

**Throughput:**
```
1 thread = 1000ms / 3.6ms = 277 req/s
100 threads = 277 * 100 = 27,700 req/s (theoretical)

Realistic: ~20,000-25,000 req/s
```

---

## 🚀 TỐI ƯU ĐỂ ĐẠT REQ/S CAO HƠN

### **Optimization 1: JWT Validation Cache**

**Hiện tại:**
```csharp
// Validate JWT mỗi request
var principal = handler.ValidateToken(token, validationParams, out _);
```

**Tối ưu:**
```csharp
// Cache validated JWT trong 1 phút
private static readonly MemoryCache _jwtCache = new();

public async Task InvokeAsync(HttpContext context)
{
    var token = GetToken(context);
    var cacheKey = $"jwt:{token.GetHashCode()}";
    
    if (!_jwtCache.TryGetValue(cacheKey, out ClaimsPrincipal? principal))
    {
        principal = handler.ValidateToken(token, validationParams, out _);
        _jwtCache.Set(cacheKey, principal, TimeSpan.FromMinutes(1));
    }
    
    context.User = principal;
    await _next(context);
}
```

**Kết quả:**
- JWT validation: 1ms → 0.01ms (100x faster)
- Total latency: 5.8ms → 4.81ms
- Throughput: +20%

---

### **Optimization 2: Skip Auth for Proxy Traffic**

**Ý tưởng:**
- Admin endpoints: Cần auth (CRUD routes, users, etc.)
- Proxy traffic: Không cần auth (hoặc auth ở backend)

**Implementation:**
```csharp
public async Task InvokeAsync(HttpContext context)
{
    var path = context.Request.Path.Value ?? "";
    
    // Skip auth for proxy traffic
    if (!path.StartsWith("/admin") && !path.StartsWith("/auth"))
    {
        await _next(context);
        return;
    }
    
    // Only validate JWT for admin endpoints
    // ...
}
```

**Kết quả:**
- Proxy traffic: 5.3ms → 3.6ms
- Throughput: +47%

---

### **Optimization 3: Async JWT Validation**

**Hiện tại:**
```csharp
// Blocking JWT validation
var principal = handler.ValidateToken(token, validationParams, out _);
```

**Tối ưu:**
```csharp
// Async validation (if supported)
var result = await handler.ValidateTokenAsync(token, validationParams);
```

**Kết quả:**
- Better thread pool utilization
- Higher concurrency

---

### **Optimization 4: Remove Unnecessary Middleware**

**Hiện tại:**
```csharp
app.UseMiddleware<MetricsMiddleware>();           // Every request
app.UseMiddleware<GatewayProtectionMiddleware>(); // Every request
app.UseMiddleware<JwtValidationMiddleware>();     // Every request
app.UseAuthentication();                          // Every request
app.UseAuthorization();                           // Every request
```

**Tối ưu:**
```csharp
// Conditional middleware based on path
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/admin"), app =>
{
    app.UseMiddleware<JwtValidationMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
});

app.UseMiddleware<MetricsMiddleware>();
app.UseMiddleware<GatewayProtectionMiddleware>();
```

---

## 📊 DỰ ĐOÁN REQ/S SAU TỐI ƯU

### **Admin Endpoints (With Auth)**
```
Before optimization: 12,000-15,000 req/s
After optimization:  15,000-20,000 req/s (+25-33%)
```

### **Proxy Endpoints (No Auth)**
```
Before optimization: 20,000-25,000 req/s
After optimization:  25,000-35,000 req/s (+25-40%)
```

### **Proxy Endpoints (With Rate Limit)**
```
Enforced by rate limiter: 100 req/s per IP
Multiple IPs: 100 * N req/s
```

---

## 🎯 BENCHMARK TARGETS

| Endpoint Type | Current (Est.) | Target | Strategy |
|---------------|----------------|--------|----------|
| **Admin CRUD** | 12,000 req/s | 20,000 req/s | JWT cache + L1 cache |
| **Proxy (No Auth)** | 20,000 req/s | 35,000 req/s | Skip auth middleware |
| **Proxy (With Auth)** | 15,000 req/s | 25,000 req/s | JWT cache + async |
| **Auth Login** | 500 req/s | 1,000 req/s | BCrypt optimization |
| **Auth Refresh** | 10,000 req/s | 20,000 req/s | L1 cache hit rate |

---

## 🔥 BOTTLENECKS CHÍNH

### **1. SQLite Single-Writer Lock**
```
Problem: SQLite chỉ cho phép 1 writer tại 1 thời điểm
Impact:  Giới hạn write throughput ~1,000-2,000 writes/s
Solution: 
  - Batch writes (đã có với GoFlow)
  - Read-only replicas
  - Migrate to PostgreSQL/SQL Server
```

### **2. JWT Validation Overhead**
```
Problem: Validate JWT mỗi request (~1ms)
Impact:  20% overhead trên mỗi authenticated request
Solution:
  - Cache validated JWTs (1 min TTL)
  - Use faster JWT library
  - Pre-validate at load balancer
```

### **3. Memory Cache Contention**
```
Problem: Multiple threads competing for cache lock
Impact:  Degraded performance under high concurrency
Solution:
  - Use ConcurrentDictionary instead of IMemoryCache
  - Partition cache by key hash
  - Read-only cache for routes
```

### **4. Thread Pool Saturation**
```
Problem: Default thread pool size may be insufficient
Impact:  Queuing delays under high load
Solution:
  - Increase min thread pool size
  - Use async/await everywhere
  - Avoid blocking calls
```

---

## 🧪 LOAD TESTING SCRIPT

```bash
# Test Admin Endpoints (With Auth)
ab -n 100000 -c 100 \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Api-Key: gw-admin-key-change-me" \
  http://localhost:5151/admin/routes

# Test Proxy Endpoints (No Auth)
ab -n 100000 -c 100 \
  http://localhost:5151/api/test

# Test Auth Login
ab -n 10000 -c 50 \
  -p login.json -T application/json \
  http://localhost:5151/auth/login

# Test Auth Refresh
ab -n 50000 -c 100 \
  -p refresh.json -T application/json \
  http://localhost:5151/auth/refresh
```

---

## 💡 KHUYẾN NGHỊ

### **Ngắn hạn (1-2 tuần):**
1. ✅ Implement JWT validation cache
2. ✅ Skip auth for proxy traffic
3. ✅ Conditional middleware based on path
4. ✅ Increase thread pool size

**Expected gain: +30-50% throughput**

### **Trung hạn (1-2 tháng):**
1. Migrate to PostgreSQL (remove SQLite bottleneck)
2. Add read replicas for scaling reads
3. Implement distributed cache (Redis)
4. Add load balancer (Nginx/HAProxy)

**Expected gain: +200-300% throughput**

### **Dài hạn (3-6 tháng):**
1. Microservices architecture
2. Horizontal scaling (multiple instances)
3. CDN for static content
4. Edge computing (Cloudflare Workers)

**Expected gain: +1000% throughput**

---

## 📊 KẾT LUẬN

### **Hiện tại (Với Auth):**
```
Admin Endpoints:     12,000-15,000 req/s
Proxy (No Auth):     20,000-25,000 req/s
Proxy (With Auth):   15,000-20,000 req/s
Auth Login:          500-1,000 req/s
Auth Refresh:        10,000-15,000 req/s
```

### **Sau tối ưu (1-2 tuần):**
```
Admin Endpoints:     15,000-20,000 req/s  (+25%)
Proxy (No Auth):     25,000-35,000 req/s  (+40%)
Proxy (With Auth):   20,000-25,000 req/s  (+33%)
Auth Login:          1,000-1,500 req/s    (+50%)
Auth Refresh:        15,000-20,000 req/s  (+33%)
```

### **Sau migrate PostgreSQL (1-2 tháng):**
```
Admin Endpoints:     50,000-80,000 req/s   (+300%)
Proxy (No Auth):     80,000-120,000 req/s  (+400%)
Proxy (With Auth):   60,000-90,000 req/s   (+350%)
```

---

**Ngày phân tích:** 2026-04-03  
**Trạng thái:** ⏳ Cần benchmark thực tế để xác nhận
