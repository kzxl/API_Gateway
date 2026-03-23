# 🚀 API Gateway

<div align="center">

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![YARP](https://img.shields.io/badge/YARP-2.3-blue)
![React](https://img.shields.io/badge/React-19-61DAFB?logo=react)
![Ant Design](https://img.shields.io/badge/Ant%20Design-5-0170FE?logo=antdesign)
![SQLite](https://img.shields.io/badge/SQLite-3-003B57?logo=sqlite)
![License](https://img.shields.io/badge/License-MIT-green)

**Production-ready API Gateway** built with YARP reverse proxy, featuring JWT authentication, automatic failover, rate limiting, circuit breaker, traffic metrics, and a full admin dashboard.

</div>

---

## 📐 Architecture

```
                          ┌─────────────────────────────────────────┐
                          │            API Gateway (:5151)          │
                          │                                         │
  Client ──── JWT Auth ──▶│  Rate Limit → IP Filter → Circuit Breaker │
                          │       ↓                                 │
                          │  YARP Reverse Proxy                     │
                          │       ↓              ↓                  │
                          │  ┌─────────┐   ┌──────────┐            │
                          │  │ Primary │   │ Standby  │            │
                          │  │  :5001  │   │  :5002   │            │
                          │  └─────────┘   └──────────┘            │
                          │       ↑                                 │
                          │  Health Check Probe (auto-failover)     │
                          │  Request Logging → Metrics Tracking     │
                          └─────────────────────────────────────────┘
                                         ▲
                                  ┌──────┴───────┐
                                  │ Admin Panel  │
                                  │ React + Vite │
                                  │    :5173     │
                                  └──────────────┘
```

**Key Design Principles:**
- Gateway là **điểm xác thực duy nhất** — backend services chỉ nhận internal calls
- **Failover tự động** — khi primary down, traffic chuyển sang standby
- **Per-route protection** — rate limit, circuit breaker, IP filter riêng từng route

---

## ✨ Features (14)

### 🔴 Tier 1 — Critical
| Feature | Description |
|---------|-------------|
| **Rate Limiting** | Per-route, per-IP sliding window. Trả `429 Too Many Requests` khi vượt ngưỡng |
| **Circuit Breaker** | Tự động ngắt route khi error rate vượt threshold, auto-recovery sau duration |
| **Request Logging** | Async background write vào DB, UI với pagination + filters + stats |
| **User Management** | CRUD users, BCrypt password hashing, role-based (Admin/User) |

### 🟡 Tier 2 — Enhancement
| Feature | Description |
|---------|-------------|
| **JWT Authentication** | Gateway issue JWT token, backend services không cần auth riêng |
| **Failover (Health Check)** | YARP Active/Passive health check, Primary/Standby destinations |
| **IP Whitelist/Blacklist** | Per-route IP filtering |
| **Request Transforms** | YARP native transforms (path rewrite, add/remove headers) |
| **Response Caching** | Configurable TTL per route cho GET requests |
| **Config Import/Export** | JSON backup/restore toàn bộ config |

### 🟢 Tier 3 — Enterprise
| Feature | Description |
|---------|-------------|
| **Traffic Metrics** | Per-route: throughput, latency (avg/min/max), error rate, sliding window 60s |
| **Retry Policy** | Per-cluster configurable retries + delay |
| **Load Balancing** | RoundRobin, Random, LeastRequests, PowerOfTwoChoices |
| **Swagger UI** | Built-in API documentation tại `/swagger` |

---

## 🛠 Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Backend** | .NET | 8.0 |
| **Proxy Engine** | YARP (Yet Another Reverse Proxy) | 2.3 |
| **ORM** | Entity Framework Core | 8.0 |
| **Database** | SQLite | 3.x |
| **Auth** | JWT Bearer + BCrypt | — |
| **Frontend** | React + Vite | 19 / 7.1 |
| **UI Library** | Ant Design | 5.x |
| **HTTP Client** | Axios | — |

---

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)

### Backend
```bash
cd APIGateway/APIGateway
dotnet run
# → http://localhost:5151
# → Swagger: http://localhost:5151/swagger
```

### Frontend
```bash
cd gateway-admin
npm install
npm run dev
# → http://localhost:5173
```

### Default Credentials
| Type | Value |
|------|-------|
| Admin Login | `admin` / `admin123` |
| Admin API Key | `X-Api-Key: gw-admin-key-change-me` |

> ⚠️ **Change these in production!** Edit `appsettings.json` for JWT secret and API key.

---

## 📡 API Reference

### Auth (Public)
| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/auth/login` | Login, nhận JWT token |
| `POST` | `/auth/validate` | Validate JWT token |

### Admin (API Key required)
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/admin/routes` | List all routes |
| `POST` | `/admin/routes` | Create route |
| `PUT` | `/admin/routes/{id}` | Update route |
| `DELETE` | `/admin/routes/{id}` | Delete route |
| `GET` | `/admin/clusters` | List all clusters |
| `POST` | `/admin/clusters` | Create cluster |
| `PUT` | `/admin/clusters/{id}` | Update cluster |
| `DELETE` | `/admin/clusters/{id}` | Delete cluster |
| `GET` | `/admin/health` | Health status + destinations |
| `GET` | `/admin/metrics` | Per-route traffic metrics |
| `DELETE` | `/admin/metrics` | Reset metrics |
| `GET` | `/admin/users` | List users |
| `POST` | `/admin/users` | Create user |
| `PUT` | `/admin/users/{id}` | Update user |
| `DELETE` | `/admin/users/{id}` | Delete user |
| `GET` | `/admin/logs` | Request logs (paginated) |
| `GET` | `/admin/logs/stats` | Log statistics |
| `DELETE` | `/admin/logs` | Clear all logs |
| `GET` | `/admin/config/export` | Export config JSON |
| `POST` | `/admin/config/import` | Import config JSON |

---

## 🖥 Admin Dashboard (7 Pages)

| Page | Description |
|------|-------------|
| **Dashboard** | Health status, stats, Primary/Standby destinations |
| **Routes** | CRUD routes + protection settings (rate limit, circuit breaker, IP filter, cache, transforms) |
| **Clusters** | CRUD clusters + failover config (Primary/Standby, health check, LB policy, retry) |
| **Traffic** | Real-time per-route metrics: throughput, latency, error rate (auto-refresh 5s) |
| **Logs** | Request logs with filters (method, route, status code) + stats |
| **Users** | User management with roles and BCrypt hashing |
| **Settings** | Config export/import + gateway info |

---

## ⚙️ Configuration

### `appsettings.json`
```json
{
  "ConnectionStrings": {
    "GatewayDb": "Data Source=gateway.db"
  },
  "AdminAuth": {
    "ApiKey": "gw-admin-key-change-me"
  },
  "Jwt": {
    "Secret": "GatewaySecretKey-Change-This-In-Production-Min32Chars!",
    "Issuer": "APIGateway",
    "Audience": "GatewayClients",
    "ExpirationMinutes": 60
  }
}
```

### Route Protection Config
```json
{
  "routeId": "api-route",
  "matchPath": "/api/{**catch-all}",
  "clusterId": "backend-cluster",
  "rateLimitPerSecond": 100,
  "circuitBreakerThreshold": 50,
  "circuitBreakerDurationSeconds": 30,
  "ipWhitelist": "192.168.1.0/24",
  "ipBlacklist": "1.2.3.4",
  "cacheTtlSeconds": 60,
  "transformsJson": "[{\"PathPrefix\":\"/v1\"}]"
}
```

### Cluster Failover Config
```json
{
  "clusterId": "backend-cluster",
  "destinationsJson": "[{\"id\":\"primary\",\"address\":\"http://backend1:5001\",\"health\":\"Active\"},{\"id\":\"standby\",\"address\":\"http://backend2:5002\",\"health\":\"Standby\"}]",
  "enableHealthCheck": true,
  "healthCheckPath": "/health",
  "healthCheckIntervalSeconds": 10,
  "loadBalancingPolicy": "RoundRobin",
  "retryCount": 3,
  "retryDelayMs": 1000
}
```

---

## 🔒 Security

- **JWT Authentication** — Gateway validates tokens, backend services trust internal calls
- **BCrypt Password Hashing** — Passwords never stored in plaintext
- **API Key Protection** — Admin endpoints require `X-Api-Key` header
- **Rate Limiting** — Per-route, per-IP to prevent abuse
- **IP Filtering** — Whitelist/blacklist per route
- **Circuit Breaker** — Prevents cascade failures

---

## 📁 Project Structure

```
API_Gateway/
├── APIGateway/APIGateway/          # Backend (.NET 8)
│   ├── Controllers/
│   │   ├── AdminRoutesController   # Route CRUD
│   │   ├── AdminClustersController # Cluster CRUD
│   │   ├── AdminUsersController    # User management
│   │   ├── AdminLogsController     # Request logs
│   │   ├── AdminConfigController   # Config import/export
│   │   ├── AuthController          # JWT login/validate
│   │   ├── HealthController        # Health status
│   │   └── MetricsController       # Traffic metrics
│   ├── Middleware/
│   │   ├── ApiKeyAuthMiddleware    # Admin API key auth
│   │   ├── GatewayProtectionMiddleware  # Rate limit + IP filter + Circuit breaker + Logging
│   │   └── MetricsMiddleware       # Per-route metrics tracking
│   ├── Models/
│   │   ├── Route, Cluster, User, RequestLog
│   ├── Services/
│   │   ├── DbProxyConfigProvider   # YARP dynamic config from DB
│   │   └── RouteRepository         # Data access
│   └── Data/
│       └── GatewayDbContext        # EF Core context
│
└── gateway-admin/                  # Frontend (React + Vite)
    └── src/
        ├── pages/
        │   ├── Dashboard.jsx       # Health overview
        │   ├── Routes.jsx          # Route management
        │   ├── Clusters.jsx        # Cluster + failover config
        │   ├── Metrics.jsx         # Traffic monitoring
        │   ├── Logs.jsx            # Request logs
        │   ├── Users.jsx           # User management
        │   └── Settings.jsx        # Config backup + info
        └── api/
            └── gatewayApi.js       # API client
```

---

## 📜 License

MIT License — free for personal and commercial use.
