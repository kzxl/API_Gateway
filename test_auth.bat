@echo off
REM API Gateway Authentication Testing Script (Windows)
REM Usage: test_auth.bat

set API_BASE=http://localhost:5151
set API_KEY=gw-admin-key-change-me

echo ==========================================
echo API Gateway Authentication Test Suite
echo ==========================================
echo.

REM Test 1: Login
echo [TEST 1] Login with admin credentials
curl -s -X POST "%API_BASE%/auth/login" ^
  -H "Content-Type: application/json" ^
  -d "{\"username\":\"admin\",\"password\":\"admin123\"}" > login_response.json

type login_response.json
echo.

REM Extract tokens (requires jq or manual parsing)
echo Please manually extract accessToken and refreshToken from login_response.json
echo.

REM Test 2: Validate Token
echo [TEST 2] Validate access token
echo Enter your access token:
set /p ACCESS_TOKEN=

curl -s -X POST "%API_BASE%/auth/validate" ^
  -H "Content-Type: application/json" ^
  -d "{\"token\":\"%ACCESS_TOKEN%\"}"
echo.
echo.

REM Test 3: Access Protected Endpoint
echo [TEST 3] Access protected endpoint (GET /admin/routes)
curl -s -X GET "%API_BASE%/admin/routes" ^
  -H "Authorization: Bearer %ACCESS_TOKEN%" ^
  -H "X-Api-Key: %API_KEY%"
echo.
echo.

REM Test 4: Access Without Token
echo [TEST 4] Access protected endpoint without token (should fail with 401)
curl -s -w "\nHTTP_CODE:%%{http_code}" -X GET "%API_BASE%/admin/routes" ^
  -H "X-Api-Key: %API_KEY%"
echo.
echo.

REM Test 5: Refresh Token
echo [TEST 5] Refresh access token
echo Enter your refresh token:
set /p REFRESH_TOKEN=

curl -s -X POST "%API_BASE%/auth/refresh" ^
  -H "Content-Type: application/json" ^
  -d "{\"refreshToken\":\"%REFRESH_TOKEN%\"}" > refresh_response.json

type refresh_response.json
echo.
echo.

REM Test 6: Logout
echo [TEST 6] Logout (revoke tokens)
curl -s -X POST "%API_BASE%/auth/logout" ^
  -H "Authorization: Bearer %ACCESS_TOKEN%" ^
  -H "Content-Type: application/json" ^
  -d "{\"refreshToken\":\"%REFRESH_TOKEN%\"}"
echo.
echo.

echo ==========================================
echo Test Suite Complete
echo ==========================================
echo.
echo Note: For full automated testing, install jq for JSON parsing
echo Download from: https://stedolan.github.io/jq/download/

pause
