# 🚀 Load Testing Guide - API Gateway

## 📋 SETUP

### **1. Start Mock Backend**
```bash
cd MockBackend
dotnet run
# Running on http://localhost:5001
```

### **2. Start API Gateway**
```bash
cd APIGateway/APIGateway
dotnet run
# Running on http://localhost:5151
```

### **3. Verify Setup**
```bash
# Test backend directly
curl http://localhost:5001/test/health

# Test through gateway
curl http://localhost:5151/test/health
```

---

## 🧪 QUICK TESTS

### **Test 1: Simple Echo (No Auth)**
```bash
# Direct backend (baseline)
curl http://localhost:5001/test/echo

# Through gateway
curl http://localhost:5151/test/echo
```

### **Test 2: With Authentication**
```bash
# Login first
curl -X POST http://localhost:5151/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# Use the accessToken
curl http://localhost:5151/test/echo \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### **Test 3: Load Test Controller Stats**
```bash
# Get current stats
curl http://localhost:5151/test/stats

# Reset stats
curl -X POST http://localhost:5151/test/stats/reset
```

---

## 📊 LOAD TESTING

### **Option 1: Using Apache Bench (Recommended)**

**Install Apache Bench:**
```bash
# Windows: Download from https://www.apachelounge.com/download/
# Linux: sudo apt-get install apache2-utils
# Mac: brew install httpd (ab included)
```

**Run Load Tests:**
```bash
# Linux/Mac
chmod +x gateway_load_test.sh
./gateway_load_test.sh

# Windows
gateway_load_test.bat
```

### **Option 2: Manual Apache Bench Commands**

**Test 1: Baseline (Direct Backend)**
```bash
ab -n 10000 -c 100 http://localhost:5001/test/echo
```

**Test 2: Through Gateway (No Auth)**
```bash
ab -n 10000 -c 100 http://localhost:5151/test/echo
```

**Test 3: Through Gateway (With Auth)**
```bash
# Get token first
TOKEN=$(curl -s -X POST http://localhost:5151/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.accessToken')

# Run load test
ab -n 10000 -c 100 \
  -H "Authorization: Bearer $TOKEN" \
  http://localhost:5151/test/echo
```

**Test 4: High Concurrency**
```bash
ab -n 50000 -c 500 http://localhost:5151/test/echo
```

**Test 5: Extreme Load**
```bash
ab -n 100000 -c 1000 http://localhost:5151/test/echo
```

**Test 6: Sustained Load (60 seconds)**
```bash
ab -t 60 -c 200 http://localhost:5151/test/echo
```

---

## 📈 EXPECTED RESULTS

### **Baseline (Direct Backend)**
```
Requests per second:    30,000-50,000 req/s
Time per request:       0.02-0.03 ms (mean, per request)
Failed requests:        0
```

### **Through Gateway (No Auth)**
```
Requests per second:    25,000-35,000 req/s
Time per request:       0.03-0.04 ms (mean, per request)
Gateway overhead:       ~10-15%
Failed requests:        0
```

### **Through Gateway (With Auth)**
```
Requests per second:    15,000-25,000 req/s
Time per request:       0.04-0.07 ms (mean, per request)
Auth overhead:          ~20-30%
Failed requests:        0
```

### **With JWT Cache Optimization**
```
Requests per second:    20,000-30,000 req/s
Time per request:       0.03-0.05 ms (mean, per request)
Cache hit rate:         >95%
Failed requests:        0
```

---

## 🔍 ANALYZING RESULTS

### **Key Metrics to Watch**

**1. Requests per second (Throughput)**
```
Good:     >20,000 req/s
Excellent: >30,000 req/s
```

**2. Time per request (Latency)**
```
Good:     <5ms
Excellent: <3ms
```

**3. Failed requests**
```
Target: 0 failures
```

**4. Gateway Overhead**
```
Formula: (Gateway Latency - Backend Latency) / Backend Latency * 100%
Target: <15%
```

### **Compare Results**

```bash
# Get gateway stats
curl http://localhost:5151/test/stats

# Example output:
{
  "totalRequests": 53073,
  "uptimeSeconds": 10.5,
  "avgRequestsPerSecond": 5054.57,
  "startTime": "2026-04-03T11:20:00Z",
  "currentTime": "2026-04-03T11:20:10Z"
}
```

---

## 🎯 OPTIMIZATION CHECKLIST

### **Before Testing**
- [ ] Close unnecessary applications
- [ ] Disable antivirus temporarily
- [ ] Use Release build (`dotnet run -c Release`)
- [ ] Increase file descriptor limits (Linux)
- [ ] Warm up the application (run 1000 requests first)

### **During Testing**
- [ ] Monitor CPU usage (should be <80%)
- [ ] Monitor memory usage (should be stable)
- [ ] Check for errors in logs
- [ ] Watch for rate limiting (429 errors)

### **After Testing**
- [ ] Compare with baseline (direct backend)
- [ ] Calculate gateway overhead
- [ ] Identify bottlenecks
- [ ] Check for memory leaks

---

## 🐛 TROUBLESHOOTING

### **Problem: Low throughput (<10,000 req/s)**

**Possible causes:**
1. Debug build instead of Release
2. Antivirus scanning
3. Insufficient system resources
4. Rate limiting enabled
5. Database locks

**Solutions:**
```bash
# Use Release build
dotnet run -c Release

# Disable rate limiting for test route
# (Already configured in Program.cs: RateLimitPerSecond = 0)

# Check system resources
# Windows: Task Manager
# Linux: htop
```

### **Problem: High failure rate**

**Possible causes:**
1. Backend not running
2. Connection pool exhausted
3. Timeout too short
4. Port exhaustion

**Solutions:**
```bash
# Verify backend is running
curl http://localhost:5001/test/health

# Check connection limits
netstat -an | grep 5151 | wc -l

# Increase connection pool (if using database)
# Add to connection string: Max Pool Size=1000
```

### **Problem: Memory leak**

**Symptoms:**
- Memory usage keeps increasing
- Performance degrades over time
- Eventually crashes

**Solutions:**
```bash
# Monitor memory
dotnet-counters monitor --process-id <PID>

# Check for memory leaks
dotnet-dump collect --process-id <PID>
dotnet-dump analyze <dump-file>
```

---

## 📊 BENCHMARK COMPARISON

### **Original (Before Auth)**
```
Throughput: 5,280 req/s (with rate limit 100 req/s)
Success:    1,000 requests
Rate limit: 52,073 requests (429)
```

### **With Auth (Expected)**
```
No Auth:    25,000-35,000 req/s
With Auth:  15,000-25,000 req/s
Admin API:  12,000-20,000 req/s
```

### **With Optimizations (Target)**
```
No Auth:    30,000-40,000 req/s
With Auth:  20,000-30,000 req/s
Admin API:  15,000-25,000 req/s
```

---

## 🚀 NEXT STEPS

1. **Run baseline tests** - Establish current performance
2. **Identify bottlenecks** - CPU, memory, I/O, network
3. **Apply optimizations** - JWT cache, conditional middleware
4. **Re-test** - Measure improvement
5. **Document results** - Update PERFORMANCE_ANALYSIS.md

---

## 📝 EXAMPLE TEST SESSION

```bash
# 1. Start services
cd MockBackend && dotnet run &
cd APIGateway/APIGateway && dotnet run -c Release &

# 2. Warm up
ab -n 1000 -c 10 http://localhost:5151/test/echo

# 3. Reset stats
curl -X POST http://localhost:5151/test/stats/reset

# 4. Run load test
ab -n 100000 -c 1000 http://localhost:5151/test/echo

# 5. Get results
curl http://localhost:5151/test/stats

# 6. Compare with baseline
ab -n 100000 -c 1000 http://localhost:5001/test/echo
```

---

**Created:** 2026-04-03  
**Status:** ✅ Ready for Testing
