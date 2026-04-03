# 🎯 .NET Framework 4.8 Port + Missing Features Plan

**Date:** 2026-04-03  
**Goal:** Port API Gateway to .NET Framework 4.8 + Add missing critical features

---

## 📊 PHÂN TÍCH TÍNH NĂNG CÒN THIẾU

### **1. Authentication & Security** ⭐ **CRITICAL**

#### **Đã có:**
- ✅ JWT Authentication (Login, Refresh, Logout)
- ✅ Refresh Token with rotation
- ✅ Session Management
- ✅ Token Blacklist
- ✅ BCrypt password hashing
- ✅ IP tracking

#### **Còn thiếu:**
- ❌ **Account Lockout** - Khóa tài khoản sau N lần login fail
- ❌ **Password Reset Flow** - Quên mật khẩu
- ❌ **Email Verification** - Xác thực email
- ❌ **2FA/MFA** - Two-Factor Authentication
- ❌ **OAuth2 Integration** - Google, Microsoft login
- ❌ **API Key Management** - CRUD API keys per user
- ❌ **Session Management UI** - View/revoke active sessions

---

### **2. Authorization & Permissions** ⭐ **HIGH PRIORITY**

#### **Đã có:**
- ✅ Role-based access (Admin, User)
- ✅ JWT claims

#### **Còn thiếu:**
- ❌ **Permission-Based Access Control (PBAC)** - Fine-grained permissions
- ❌ **Resource-level permissions** - Per route, per cluster
- ❌ **Permission UI** - Manage permissions in admin panel
- ❌ **Role hierarchy** - Super Admin > Admin > User
- ❌ **Dynamic role assignment** - Change roles without restart

---

### **3. Monitoring & Observability** ⭐ **HIGH PRIORITY**

#### **Đã có:**
- ✅ Request logging (batch to DB)
- ✅ Metrics middleware
- ✅ Circuit breaker state

#### **Còn thiếu:**
- ❌ **Real-time Dashboard** - Live metrics, charts
- ❌ **Alerting System** - Email/Slack alerts on errors
- ❌ **Distributed Tracing** - OpenTelemetry integration
- ❌ **Health Check Dashboard** - Backend health status
- ❌ **Performance Profiling** - Slow query detection
- ❌ **Audit Log UI** - View all admin actions
- ❌ **Log Search & Filter** - Advanced log queries

---

### **4. Rate Limiting & Protection** ⭐ **MEDIUM PRIORITY**

#### **Đã có:**
- ✅ Rate limiting per IP (TokenBucket)
- ✅ IP whitelist/blacklist
- ✅ Circuit breaker

#### **Còn thiếu:**
- ❌ **Rate limiting per User** - Not just IP
- ❌ **Rate limiting per API Key** - Different limits per key
- ❌ **Dynamic rate limit adjustment** - Change without restart
- ❌ **Rate limit UI** - Configure in admin panel
- ❌ **DDoS Protection** - Advanced attack detection
- ❌ **Request throttling** - Gradual slowdown instead of hard limit
- ❌ **Quota management** - Monthly/daily quotas

---

### **5. Caching & Performance** ⭐ **MEDIUM PRIORITY**

#### **Đã có:**
- ✅ L1 cache (MemoryCache)
- ✅ L2 cache sync (GoCache)
- ✅ JWT validation cache

#### **Còn thiếu:**
- ❌ **Response Caching** - Cache GET responses
- ❌ **Redis Integration** - Distributed cache
- ❌ **Cache warming** - Pre-load cache on startup
- ❌ **Cache invalidation UI** - Manual cache clear
- ❌ **CDN Integration** - Cloudflare, Akamai
- ❌ **Compression** - Gzip, Brotli

---

### **6. Configuration & Management** ⭐ **HIGH PRIORITY**

#### **Đã có:**
- ✅ Route CRUD
- ✅ Cluster CRUD
- ✅ User CRUD

#### **Còn thiếu:**
- ❌ **Configuration Versioning** - Track config changes
- ❌ **Configuration Rollback** - Undo changes
- ❌ **Configuration Import/Export** - Backup/restore
- ❌ **Multi-environment support** - Dev, Staging, Prod
- ❌ **Feature flags** - Enable/disable features
- ❌ **A/B Testing** - Route traffic to different backends
- ❌ **Blue-Green Deployment** - Zero-downtime updates

---

### **7. Developer Experience** ⭐ **LOW PRIORITY**

#### **Còn thiếu:**
- ❌ **API Documentation** - Swagger/OpenAPI
- ❌ **SDK/Client Libraries** - C#, Python, JS
- ❌ **Webhooks** - Event notifications
- ❌ **GraphQL Support** - Alternative to REST
- ❌ **WebSocket Support** - Real-time connections
- ❌ **gRPC Support** - High-performance RPC

---

## 🏗️ .NET FRAMEWORK 4.8 PORT PLAN

### **Phase 1: Core Infrastructure**

#### **1.1 Project Setup**
```
APIGateway.NetFramework/
├── APIGateway.NetFramework.sln
├── APIGateway.NetFramework/
│   ├── Web.config
│   ├── Global.asax
│   ├── Startup.cs (OWIN)
│   └── packages.config
├── APIGateway.Shared/  (Shared with .NET 8)
│   ├── Models/
│   ├── DTOs/
│   └── Interfaces/
```

#### **1.2 Technology Stack**

| Component | .NET 8 | .NET Framework 4.8 |
|-----------|--------|-------------------|
| **Web Framework** | ASP.NET Core | ASP.NET Web API + OWIN |
| **Reverse Proxy** | YARP 2.3 | Ocelot 18.0 |
| **ORM** | EF Core 8 | Entity Framework 6.4 |
| **Database** | SQLite | SQL Server Express |
| **DI Container** | Built-in | Autofac |
| **JWT** | Microsoft.AspNetCore.Authentication.JwtBearer | Microsoft.Owin.Security.Jwt |
| **Rate Limiting** | System.Threading.RateLimiting | Custom TokenBucket |
| **Caching** | IMemoryCache | System.Runtime.Caching |
| **HTTP Server** | Kestrel | IIS / HttpListener |

#### **1.3 Key Differences**

**Async/Await:**
```csharp
// .NET 8 - Full async support
public async Task<IActionResult> GetAll()
{
    var routes = await _service.GetAllAsync();
    return Ok(routes);
}

// .NET Framework 4.8 - Limited async
public async Task<IHttpActionResult> GetAll()
{
    var routes = await _service.GetAllAsync();
    return Ok(routes);
}
```

**Middleware vs OWIN:**
```csharp
// .NET 8 - Middleware
app.UseMiddleware<JwtValidationMiddleware>();

// .NET Framework 4.8 - OWIN
app.Use<JwtValidationMiddleware>();
```

**Dependency Injection:**
```csharp
// .NET 8 - Built-in
builder.Services.AddScoped<ITokenService, TokenService>();

// .NET Framework 4.8 - Autofac
var builder = new ContainerBuilder();
builder.RegisterType<TokenService>().As<ITokenService>().InstancePerRequest();
```

---

### **Phase 2: Feature Parity**

#### **2.1 Authentication System**
- ✅ Port RefreshToken model
- ✅ Port UserSession model
- ✅ Port TokenService (with System.Runtime.Caching)
- ✅ Port AuthController
- ✅ Implement OWIN JWT middleware

#### **2.2 Rate Limiting**
```csharp
// Custom TokenBucket for .NET Framework
public class TokenBucketRateLimiter
{
    private readonly int _capacity;
    private readonly int _refillRate;
    private int _tokens;
    private DateTime _lastRefill;
    private readonly object _lock = new object();

    public bool TryAcquire()
    {
        lock (_lock)
        {
            Refill();
            if (_tokens > 0)
            {
                _tokens--;
                return true;
            }
            return false;
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastRefill).TotalSeconds;
        var tokensToAdd = (int)(elapsed * _refillRate);
        
        if (tokensToAdd > 0)
        {
            _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
            _lastRefill = now;
        }
    }
}
```

#### **2.3 Ocelot Configuration**
```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/test/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 5001 }
      ],
      "UpstreamPathTemplate": "/test/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST" ],
      "RateLimitOptions": {
        "EnableRateLimiting": true,
        "Period": "1s",
        "Limit": 100
      }
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:5151"
  }
}
```

---

### **Phase 3: Missing Features Implementation**

#### **Priority 1: Account Lockout (1-2 days)**

**Models:**
```csharp
// Add to User model
public int FailedLoginAttempts { get; set; }
public DateTime? LockedUntil { get; set; }
public DateTime? LastFailedLogin { get; set; }
```

**Service:**
```csharp
public async Task<bool> IncrementFailedLoginAsync(int userId)
{
    var user = await _db.Users.FindAsync(userId);
    if (user == null) return false;

    user.FailedLoginAttempts++;
    user.LastFailedLogin = DateTime.UtcNow;

    // Lock after 5 failed attempts
    if (user.FailedLoginAttempts >= 5)
    {
        user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
    }

    await _db.SaveChangesAsync();
    return true;
}
```

---

#### **Priority 2: Permission System (3-5 days)**

**Models:**
```csharp
public class Permission
{
    public int Id { get; set; }
    public string Name { get; set; }        // "routes.read"
    public string Resource { get; set; }    // "routes"
    public string Action { get; set; }      // "read"
    public string Description { get; set; }
}

public class RolePermission
{
    public int Id { get; set; }
    public string Role { get; set; }
    public int PermissionId { get; set; }
    public Permission Permission { get; set; }
}

public class UserPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PermissionId { get; set; }
    public Permission Permission { get; set; }
}
```

**Attribute:**
```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public string Permission { get; set; }

    protected override bool IsAuthorized(HttpActionContext context)
    {
        var user = context.RequestContext.Principal;
        var permissionService = context.Request.GetDependencyScope()
            .GetService(typeof(IPermissionService)) as IPermissionService;

        return permissionService.HasPermission(user, Permission);
    }
}

// Usage
[RequirePermission(Permission = "routes.write")]
public async Task<IHttpActionResult> CreateRoute(CreateRouteDto dto)
{
    // ...
}
```

---

#### **Priority 3: Real-time Dashboard (5-7 days)**

**SignalR Hub:**
```csharp
public class MetricsHub : Hub
{
    public async Task SubscribeToMetrics()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "metrics");
    }

    public async Task UnsubscribeFromMetrics()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "metrics");
    }
}

// Background worker
public class MetricsBroadcaster
{
    private readonly IHubContext<MetricsHub> _hubContext;
    private Timer _timer;

    public void Start()
    {
        _timer = new Timer(BroadcastMetrics, null, 0, 1000); // Every 1 second
    }

    private async void BroadcastMetrics(object state)
    {
        var metrics = GetCurrentMetrics();
        await _hubContext.Clients.Group("metrics").SendAsync("metricsUpdate", metrics);
    }
}
```

**React Dashboard:**
```jsx
import { HubConnectionBuilder } from '@microsoft/signalr';

function RealtimeDashboard() {
  const [metrics, setMetrics] = useState({});

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('http://localhost:5151/hubs/metrics')
      .build();

    connection.on('metricsUpdate', (data) => {
      setMetrics(data);
    });

    connection.start();
    connection.invoke('SubscribeToMetrics');

    return () => {
      connection.invoke('UnsubscribeFromMetrics');
      connection.stop();
    };
  }, []);

  return (
    <div>
      <h2>Real-time Metrics</h2>
      <div>Requests/sec: {metrics.requestsPerSecond}</div>
      <div>Active Connections: {metrics.activeConnections}</div>
      <div>Error Rate: {metrics.errorRate}%</div>
    </div>
  );
}
```

---

#### **Priority 4: Response Caching (2-3 days)**

**Middleware:**
```csharp
public class ResponseCachingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public async Task InvokeAsync(HttpContext context)
    {
        // Only cache GET requests
        if (context.Request.Method != "GET")
        {
            await _next(context);
            return;
        }

        var cacheKey = $"response:{context.Request.Path}:{context.Request.QueryString}";

        if (_cache.TryGetValue(cacheKey, out CachedResponse cached))
        {
            // Cache hit
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = cached.ContentType;
            await context.Response.WriteAsync(cached.Body);
            return;
        }

        // Cache miss - capture response
        var originalBody = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await _next(context);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(memoryStream).ReadToEndAsync();

        // Cache successful responses
        if (context.Response.StatusCode == 200)
        {
            _cache.Set(cacheKey, new CachedResponse
            {
                StatusCode = context.Response.StatusCode,
                ContentType = context.Response.ContentType,
                Body = body
            }, TimeSpan.FromMinutes(5));
        }

        // Write to original response
        memoryStream.Seek(0, SeekOrigin.Begin);
        await memoryStream.CopyToAsync(originalBody);
    }
}
```

---

## 📋 IMPLEMENTATION ROADMAP

### **Sprint 1-2: .NET Framework Port (2 weeks)**
- [ ] Setup .NET Framework 4.8 project
- [ ] Integrate Ocelot
- [ ] Port authentication system
- [ ] Port rate limiting (custom implementation)
- [ ] Port all controllers
- [ ] Testing & debugging

### **Sprint 3: Account Security (1 week)**
- [ ] Account lockout
- [ ] Password reset flow
- [ ] Email service integration
- [ ] Password strength validation

### **Sprint 4: Permission System (1 week)**
- [ ] Permission models & database
- [ ] Permission service
- [ ] RequirePermission attribute
- [ ] Admin UI for permissions

### **Sprint 5: Real-time Dashboard (1 week)**
- [ ] SignalR hub
- [ ] Metrics broadcaster
- [ ] React dashboard with charts
- [ ] WebSocket fallback

### **Sprint 6: Response Caching (1 week)**
- [ ] Response caching middleware
- [ ] Cache invalidation strategy
- [ ] Cache warming
- [ ] Admin UI for cache management

### **Sprint 7: Advanced Features (2 weeks)**
- [ ] Rate limiting per user
- [ ] Audit log UI
- [ ] Configuration versioning
- [ ] Health check dashboard

---

## 🎯 DELIVERABLES

### **.NET Framework 4.8 Version**
- ✅ Full feature parity with .NET 8
- ✅ Ocelot-based reverse proxy
- ✅ Custom rate limiting
- ✅ Entity Framework 6.4
- ✅ OWIN pipeline
- ✅ IIS deployment ready

### **Missing Features**
- ✅ Account lockout
- ✅ Permission system
- ✅ Real-time dashboard
- ✅ Response caching
- ✅ Rate limiting per user
- ✅ Audit log UI

### **Documentation**
- ✅ .NET Framework deployment guide
- ✅ Feature comparison matrix
- ✅ Migration guide (.NET 8 → .NET Framework)
- ✅ Performance benchmarks

---

**Estimated Timeline:** 8-10 weeks  
**Team Size:** 1-2 developers  
**Priority:** High (Windows Server 2012 support)
