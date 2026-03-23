# API Gateway

Hệ thống API Gateway sử dụng **YARP (Yet Another Reverse Proxy)** trên **.NET 8**, với **Admin Dashboard** bằng React + Ant Design.

## Kiến trúc

```
┌──────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Client App  │────▶│   API Gateway    │────▶│ Backend Service │
│              │     │  (YARP Proxy)    │     │  :5001, :5002   │
└──────────────┘     └──────────────────┘     └─────────────────┘
                            ▲
                     ┌──────┴───────┐
                     │ Admin Panel  │
                     │ React + Vite │
                     │    :5173     │
                     └──────────────┘
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8, YARP 2.3, EF Core 8, SQLite |
| Frontend | React 19, Vite 7, Ant Design 5 |
| Auth | API Key (header `X-Api-Key`) |

## Quick Start

### Backend
```bash
cd APIGateway/APIGateway
dotnet run
# → http://localhost:5151
# → Swagger UI: http://localhost:5151/swagger
```

### Frontend
```bash
cd gateway-admin
npm install
npm run dev
# → http://localhost:5173
```

### Default API Key
```
X-Api-Key: gw-admin-key-change-me
```
Thay đổi trong `appsettings.json` → `AdminAuth:ApiKey`

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/admin/routes` | Lấy danh sách routes |
| POST | `/admin/routes` | Tạo route mới |
| PUT | `/admin/routes/{id}` | Cập nhật route |
| DELETE | `/admin/routes/{id}` | Xóa route |
| GET | `/admin/clusters` | Lấy danh sách clusters |
| POST | `/admin/clusters` | Tạo cluster mới |
| PUT | `/admin/clusters/{id}` | Cập nhật cluster |
| DELETE | `/admin/clusters/{id}` | Xóa cluster |
| GET | `/admin/health` | Health check + stats |

## Features

- ✅ Dynamic routing (CRUD via API/UI)
- ✅ Multi-destination load balancing
- ✅ Admin Dashboard with live stats
- ✅ API Key authentication
- ✅ Auto-reload proxy config on changes
- ✅ Swagger API documentation
- ✅ SQLite persistence (zero-config)
