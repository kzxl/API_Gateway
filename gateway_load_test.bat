@echo off
REM Gateway Load Test Script for Windows
REM Requires: Apache Bench (ab) - Download from https://www.apachelounge.com/download/

set API_BASE=http://localhost:5151
set BACKEND_BASE=http://localhost:5001

echo ==========================================
echo API Gateway Proxy Load Test
echo ==========================================
echo.

REM Check if backend is running
echo Checking if backend is available...
curl -s "%BACKEND_BASE%/test/health" > nul 2>&1
if errorlevel 1 (
    echo Backend is not running at %BACKEND_BASE%
    echo Please start the backend service first
    pause
    exit /b 1
)
echo Backend is running
echo.

REM Test 1: Direct Backend Call (Baseline)
echo [BASELINE] Direct Backend Call (No Gateway)
echo Requests: 10,000 ^| Concurrency: 100
ab -n 10000 -c 100 -q "%BACKEND_BASE%/test/echo" 2>&1 | findstr /C:"Requests per second" /C:"Time per request" /C:"Failed requests"
echo.

REM Test 2: Through Gateway (No Auth)
echo [TEST 1] Through Gateway (No Auth)
echo Requests: 10,000 ^| Concurrency: 100
ab -n 10000 -c 100 -q "%API_BASE%/test/echo" 2>&1 | findstr /C:"Requests per second" /C:"Time per request" /C:"Failed requests"
echo.

REM Get access token
echo Getting access token...
curl -s -X POST "%API_BASE%/auth/login" ^
  -H "Content-Type: application/json" ^
  -d "{\"username\":\"admin\",\"password\":\"admin123\"}" > login_response.json

REM Extract token (manual step for Windows)
echo Please extract accessToken from login_response.json
echo.
set /p ACCESS_TOKEN="Enter your access token: "
echo.

REM Test 3: Through Gateway (With Auth)
echo [TEST 2] Through Gateway (With Auth)
echo Requests: 10,000 ^| Concurrency: 100
ab -n 10000 -c 100 -q ^
  -H "Authorization: Bearer %ACCESS_TOKEN%" ^
  "%API_BASE%/test/echo" 2>&1 | findstr /C:"Requests per second" /C:"Time per request" /C:"Failed requests"
echo.

REM Test 4: High Concurrency (No Auth)
echo [TEST 3] High Concurrency (No Auth)
echo Requests: 50,000 ^| Concurrency: 500
ab -n 50000 -c 500 -q "%API_BASE%/test/echo" 2>&1 | findstr /C:"Requests per second" /C:"Time per request" /C:"Failed requests"
echo.

REM Test 5: Sustained Load
echo [TEST 4] Sustained Load Test (60 seconds)
echo Duration: 60s ^| Concurrency: 200
ab -t 60 -c 200 -q "%API_BASE%/test/echo" 2>&1 | findstr /C:"Requests per second" /C:"Time per request" /C:"Failed requests" /C:"Complete requests"
echo.

REM Get statistics
echo [STATS] Gateway Statistics
curl -s "%API_BASE%/test/stats"
echo.
echo.

echo ==========================================
echo Load Test Complete
echo ==========================================
echo.
echo Performance Analysis:
echo 1. Compare Direct Backend vs Through Gateway
echo 2. Compare No Auth vs With Auth
echo 3. Monitor CPU and memory during tests
echo.

pause
