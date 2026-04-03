# 🚀 Authentication System Implementation Guide

## ✅ Completed Features

### Backend (.NET 8)
- ✅ RefreshToken model with expiration tracking
- ✅ UserSession model for audit and multi-device management
- ✅ TokenService with L1 cache optimization
- ✅ Enhanced AuthController with refresh/logout endpoints
- ✅ JWT Validation Middleware with token blacklist
- ✅ Database schema with proper indexes

### Frontend (React)
- ✅ AuthContext with automatic token refresh
- ✅ Login page with optimized UX
- ✅ ProtectedRoute wrapper
- ✅ Axios interceptor for 401 handling
- ✅ User dropdown menu with logout

---

## 🔧 Setup Instructions

### 1. Backend Setup

**Update appsettings.json:**
```json
{
  "Jwt": {
    "Secret": "GatewaySecretKey-Change-This-In-Production-Min32Chars!",
    "Issuer": "APIGateway",
    "Audience": "GatewayClients",
    "ExpirationMinutes": "15"
  }
}
```

**Run the application:**
```bash
cd APIGateway/APIGateway
dotnet ef database update  # If using EF migrations
# OR manually run the SQL migration:
# sqlite3 gateway.db < Data/Migrations/001_AddAuthTables.sql

dotnet run
```

The database will auto-create tables on first run via `EnsureCreated()`.

### 2. Frontend Setup

**Install dependencies (if needed):**
```bash
cd gateway-admin
npm install
```

**Run the dev server:**
```bash
npm run dev
```

**Access the admin panel:**
- URL: http://localhost:5173
- Default credentials: `admin` / `admin123`

---

## 🎯 How It Works

### Authentication Flow

```
1. User enters credentials
   ↓
2. POST /auth/login
   ↓
3. Backend validates credentials
   ↓
4. Generate Access Token (15 min) + Refresh Token (7 days)
   ↓
5. Create UserSession record
   ↓
6. Return tokens to frontend
   ↓
7. Frontend stores in localStorage
   ↓
8. All API requests include: Authorization: Bearer {accessToken}
```

### Token Refresh Flow

```
1. Access token expires (15 min)
   ↓
2. API returns 401 Unauthorized
   ↓
3. Axios interceptor catches 401
   ↓
4. POST /auth/refresh with refreshToken
   ↓
5. Backend validates & rotates refresh token
   ↓
6. Return new access token + new refresh token
   ↓
7. Retry original request with new token
```

### Logout Flow

```
1. User clicks Logout
   ↓
2. POST /auth/logout with refreshToken
   ↓
3. Backend revokes refresh token
   ↓
4. Backend blacklists current access token (JTI)
   ↓
5. Frontend clears localStorage
   ↓
6. Redirect to /login
```

---

## 🔒 Security Features

### ✅ Implemented
- Short-lived access tokens (15 minutes)
- Long-lived refresh tokens (7 days)
- Refresh token rotation on use
- Token blacklist for immediate revocation
- IP tracking for audit
- Session management
- Secure token generation (32 bytes random)
- CORS protection
- JWT validation middleware

### 🎯 Performance Optimizations

**L1 Cache (In-Memory):**
- Blacklisted JTIs: `ConcurrentDictionary` (nanosecond lookup)
- Refresh tokens: `IMemoryCache` (5 min TTL)
- Routes: `IMemoryCache` (forever, invalidated on change)

**Zero-Allocation Patterns:**
- Pre-allocated static dictionaries
- Fire-and-forget async operations
- Minimal string allocations

**Database Indexes:**
- RefreshTokens: `Token` (unique), `UserId`
- UserSessions: `SessionId` (unique), `AccessTokenJti`, `UserId`

---

## 📊 API Endpoints

### Public Endpoints (No Auth Required)

**POST /auth/login**
```json
Request:
{
  "username": "admin",
  "password": "admin123"
}

Response:
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "xYz123...",
  "expiresIn": 900,
  "tokenType": "Bearer",
  "user": {
    "id": 1,
    "username": "admin",
    "role": "Admin"
  }
}
```

**POST /auth/refresh**
```json
Request:
{
  "refreshToken": "xYz123..."
}

Response:
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "aBc456...",
  "expiresIn": 900
}
```

**POST /auth/validate**
```json
Request:
{
  "token": "eyJhbGc..."
}

Response:
{
  "valid": true,
  "claims": [
    { "type": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "value": "1" },
    { "type": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", "value": "admin" },
    { "type": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role", "value": "Admin" }
  ]
}
```

### Protected Endpoints (Requires Auth)

**POST /auth/logout**
```json
Request:
{
  "refreshToken": "xYz123..."
}
Headers:
Authorization: Bearer eyJhbGc...

Response:
{
  "message": "Logged out successfully"
}
```

---

## 🧪 Testing

### Manual Testing

**1. Test Login:**
```bash
curl -X POST http://localhost:5151/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'
```

**2. Test Protected Endpoint:**
```bash
curl http://localhost:5151/admin/routes \
  -H "Authorization: Bearer {accessToken}" \
  -H "X-Api-Key: gw-admin-key-change-me"
```

**3. Test Token Refresh:**
```bash
curl -X POST http://localhost:5151/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"{refreshToken}"}'
```

**4. Test Logout:**
```bash
curl -X POST http://localhost:5151/auth/logout \
  -H "Authorization: Bearer {accessToken}" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"{refreshToken}"}'
```

### Frontend Testing

1. Open http://localhost:5173
2. Should redirect to /login
3. Enter credentials: `admin` / `admin123`
4. Should redirect to dashboard
5. Navigate to different pages (should stay logged in)
6. Refresh browser (should stay logged in)
7. Click logout (should redirect to login)
8. Try accessing /routes directly (should redirect to login)

---

## 📈 Performance Benchmarks

### Expected Performance

| Operation | Target Latency | Strategy |
|-----------|---------------|----------|
| Login | <100ms | BCrypt (optimized rounds) |
| Token Refresh | <50ms | L1 cache lookup |
| Token Validation | <5ms | In-memory blacklist check |
| Logout | <20ms | Async DB write |

### Load Testing

```bash
# Test login endpoint
ab -n 1000 -c 10 -p login.json -T application/json \
  http://localhost:5151/auth/login

# Test protected endpoint with token
ab -n 10000 -c 100 -H "Authorization: Bearer {token}" \
  http://localhost:5151/admin/routes
```

---

## 🔮 Future Enhancements

### Phase 2 (Optional)
- [ ] Account lockout after N failed attempts
- [ ] Password reset flow
- [ ] Email verification
- [ ] 2FA/MFA support
- [ ] OAuth2 integration (Google, Microsoft)
- [ ] Session management UI (view/revoke active sessions)
- [ ] Audit log for authentication events

### Phase 3 (Advanced)
- [ ] Permission-based access control (PBAC)
- [ ] API rate limiting per user
- [ ] IP-based rate limiting on auth endpoints
- [ ] Suspicious activity detection
- [ ] WebAuthn/Passkey support

---

## 🐛 Troubleshooting

**Issue: "Token has been revoked" error**
- Clear localStorage and login again
- Check if logout was called properly

**Issue: Automatic refresh not working**
- Check browser console for errors
- Verify refresh token is stored in localStorage
- Check backend logs for refresh endpoint errors

**Issue: CORS errors**
- Verify CORS policy in Program.cs includes your frontend URL
- Check if credentials are being sent with requests

**Issue: Database locked errors**
- Ensure only one instance of the app is running
- Check if GoFlow sidecar is running (for batch logging)

---

## 📝 Notes

- Access tokens are short-lived (15 min) for security
- Refresh tokens are long-lived (7 days) for UX
- Tokens are automatically refreshed on 401 errors
- Blacklisted tokens are cleaned up every 5 minutes
- Session activity is tracked in background (fire-and-forget)

---

**Implementation Date:** 2026-04-03  
**Status:** ✅ Ready for Testing
