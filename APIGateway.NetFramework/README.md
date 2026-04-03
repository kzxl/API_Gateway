# 🏗️ .NET Framework 4.8 Port - Implementation Guide

**Date:** 2026-04-03  
**Status:** 🔄 In Progress  
**Target:** Windows Server 2012 Support

---

## 📊 PROJECT STRUCTURE

```
APIGateway.NetFramework/
├── APIGateway.NetFramework.csproj  ✅ Created
├── Web.config                       ✅ Created
├── ocelot.json                      ✅ Created
├── Startup.cs                       ✅ Created
├── Controllers/
│   └── AuthController.cs            ✅ Created
├── Infrastructure/
│   ├── TokenBucketRateLimiter.cs    ✅ Created
│   └── RequirePermissionAttribute.cs ✅ Created
├── Models/                          🔄 To be copied from .NET 8
├── Services/                        🔄 To be ported
└── Middleware/                      🔄 To be ported
```

---

## ✅ COMPLETED

### **1. Project Setup**
```
✅ .NET Framework 4.8 project file
✅ Web.config with JWT settings
✅ Ocelot configuration
✅ OWIN Startup class
```

### **2. Core Infrastructure**
```
✅ Custom TokenBucket rate limiter
✅ RequirePermission attribute
✅ AuthController (basic structure)
```

---

## 🔄 NEXT STEPS

### **Phase 1: Core Components (2-3 days)**

#### **1.1 Install NuGet Packages**
```powershell
# In Package Manager Console
Install-Package Ocelot -Version 18.0.0
Install-Package EntityFramework -Version 6.4.4
Install-Package System.Data.SQLite -Version 1.0.118
Install-Package System.Data.SQLite.EF6 -Version 1.0.118
Install-Package Autofac -Version 6.5.0
Install-Package Autofac.WebApi2 -Version 6.1.1
Install-Package Microsoft.Owin -Version 4.2.2
Install-Package Microsoft.Owin.Host.SystemWeb -Version 4.2.2
Install-Package Microsoft.Owin.Security.Jwt -Version 4.2.2
Install-Package Microsoft.AspNet.WebApi.Owin -Version 5.2.9
Install-Package BCrypt.Net-Next -Version 4.0.3
Install-Package Newtonsoft.Json -Version 13.0.3
```

#### **1.2 Copy Shared Models**
```bash
# Copy models from .NET 8 version
cp APIGateway/APIGateway/Models/*.cs APIGateway.NetFramework/Models/

# Models to copy:
- User.cs
- RefreshToken.cs
- UserSession.cs
- Permission.cs
- Route.cs
- Cluster.cs
- RequestLog.cs
```

#### **1.3 Port Services**
```
🔄 IUserService + UserService
🔄 ITokenService + TokenService
🔄 IPermissionService + PermissionService
🔄 IRouteService + RouteService
```

**Key Differences:**
```csharp
// .NET 8
public async Task<UserDto?> GetByIdAsync(int id)

// .NET Framework 4.8
public async Task<UserDto> GetByIdAsync(int id)
// Note: No nullable reference types
```

---

### **Phase 2: Database Layer (1-2 days)**

#### **2.1 Entity Framework 6.4 DbContext**
```csharp
using System.Data.Entity;

namespace APIGateway.NetFramework.Data
{
    public class GatewayDbContext : DbContext
    {
        public GatewayDbContext() : base("GatewayDb")
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Configure relationships and indexes
            modelBuilder.Entity<RefreshToken>()
                .HasRequired(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId);

            // ... more configurations
        }
    }
}
```

#### **2.2 Database Initialization**
```csharp
public class GatewayDbInitializer : CreateDatabaseIfNotExists<GatewayDbContext>
{
    protected override void Seed(GatewayDbContext context)
    {
        // Seed default admin user
        context.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "Admin",
            IsActive = true
        });

        // Seed permissions
        // ... seed logic

        context.SaveChanges();
    }
}
```

---

### **Phase 3: Middleware (2-3 days)**

#### **3.1 OWIN Middleware**
```csharp
// Global Exception Middleware
public class GlobalExceptionMiddleware : OwinMiddleware
{
    public GlobalExceptionMiddleware(OwinMiddleware next) : base(next) { }

    public override async Task Invoke(IOwinContext context)
    {
        try
        {
            await Next.Invoke(context);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonConvert.SerializeObject(new { error = ex.Message })
            );
        }
    }
}
```

#### **3.2 JWT Validation Middleware**
```csharp
public class JwtValidationMiddleware : OwinMiddleware
{
    private static readonly ConcurrentDictionary<int, (ClaimsPrincipal, DateTime)> _jwtCache = 
        new ConcurrentDictionary<int, (ClaimsPrincipal, DateTime)>();

    public override async Task Invoke(IOwinContext context)
    {
        var path = context.Request.Path.Value;
        
        // Skip for public endpoints
        if (path.StartsWith("/auth"))
        {
            await Next.Invoke(context);
            return;
        }

        // Extract and validate JWT
        var token = context.Request.Headers["Authorization"]?.Replace("Bearer ", "");
        
        if (!string.IsNullOrEmpty(token))
        {
            var tokenHash = token.GetHashCode();
            
            // Check cache first
            if (_jwtCache.TryGetValue(tokenHash, out var cached))
            {
                if (cached.Item2 > DateTime.UtcNow)
                {
                    context.Request.User = cached.Item1;
                    await Next.Invoke(context);
                    return;
                }
                else
                {
                    _jwtCache.TryRemove(tokenHash, out _);
                }
            }

            // Validate and cache
            // ... validation logic
        }

        await Next.Invoke(context);
    }
}
```

---

### **Phase 4: Dependency Injection (1 day)**

#### **4.1 Autofac Configuration**
```csharp
public class AutofacConfig
{
    public static IContainer RegisterDependencies()
    {
        var builder = new ContainerBuilder();

        // Register DbContext
        builder.RegisterType<GatewayDbContext>()
            .AsSelf()
            .InstancePerRequest();

        // Register services
        builder.RegisterType<UserService>()
            .As<IUserService>()
            .InstancePerRequest();

        builder.RegisterType<TokenService>()
            .As<ITokenService>()
            .InstancePerRequest();

        builder.RegisterType<PermissionService>()
            .As<IPermissionService>()
            .InstancePerRequest();

        // Register controllers
        builder.RegisterApiControllers(Assembly.GetExecutingAssembly());

        return builder.Build();
    }
}
```

---

## 📊 FEATURE PARITY MATRIX

| Feature | .NET 8 | .NET Framework 4.8 | Status |
|---------|--------|-------------------|--------|
| **JWT Authentication** | ✅ | 🔄 | In Progress |
| **Refresh Token** | ✅ | 🔄 | In Progress |
| **Account Lockout** | ✅ | 🔄 | In Progress |
| **Permission System** | ✅ | 🔄 | In Progress |
| **Rate Limiting** | ✅ Native | ✅ Custom | Completed |
| **Circuit Breaker** | ✅ | ✅ Ocelot | Planned |
| **Reverse Proxy** | ✅ YARP | ✅ Ocelot | Configured |
| **Database** | ✅ EF Core 8 | 🔄 EF 6.4 | Planned |
| **Caching** | ✅ IMemoryCache | 🔄 MemoryCache | Planned |

---

## 🎯 TIMELINE

### **Week 1: Core Infrastructure**
- Day 1-2: Install packages, copy models
- Day 3-4: Port services
- Day 5: Database layer

### **Week 2: Middleware & Controllers**
- Day 1-2: OWIN middleware
- Day 3-4: Controllers
- Day 5: Dependency injection

### **Week 3: Testing & Deployment**
- Day 1-2: Unit tests
- Day 3: Integration tests
- Day 4: IIS deployment
- Day 5: Documentation

---

## 📝 DEPLOYMENT TO IIS

### **1. Publish Application**
```powershell
# In Visual Studio
# Right-click project → Publish → Folder
# Target: bin\Release\Publish
```

### **2. Create IIS Application**
```powershell
# In IIS Manager
# Create new Application Pool (.NET Framework 4.8)
# Create new Website/Application
# Point to published folder
```

### **3. Configure Application Pool**
```
.NET CLR Version: v4.0
Managed Pipeline Mode: Integrated
Identity: ApplicationPoolIdentity
```

---

## 🔧 TROUBLESHOOTING

### **Common Issues:**

**1. Assembly Binding Errors**
```xml
<!-- Add to Web.config -->
<runtime>
  <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
    <dependentAssembly>
      <assemblyIdentity name="Newtonsoft.Json" />
      <bindingRedirect oldVersion="0.0.0.0-13.0.0.0" newVersion="13.0.0.0" />
    </dependentAssembly>
  </assemblyBinding>
</runtime>
```

**2. OWIN Startup Not Found**
```csharp
// Add to Web.config
<appSettings>
  <add key="owin:AutomaticAppStartup" value="true" />
</appSettings>
```

**3. SQLite Not Working**
```xml
<!-- Add to Web.config -->
<system.data>
  <DbProviderFactories>
    <add name="SQLite Data Provider" 
         invariant="System.Data.SQLite.EF6" 
         type="System.Data.SQLite.EF6.SQLiteProviderFactory, System.Data.SQLite.EF6" />
  </DbProviderFactories>
</system.data>
```

---

**Status:** 🔄 **30% Complete**  
**Next:** Install NuGet packages and port services  
**ETA:** 2-3 weeks for full feature parity
