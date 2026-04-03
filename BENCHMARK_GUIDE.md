# 🚀 Benchmark Script - Minimal Routing Performance

**Date:** 2026-04-03  
**Purpose:** Test YARP routing performance without middleware

---

## 📋 PREREQUISITES

**Install benchmark tools:**

```bash
# Windows (using Chocolatey)
choco install wrk

# Or download from: https://github.com/wg/wrk/releases

# Install hey (Go-based tool)
go install github.com/rakyll/hey@latest

# Apache Bench (comes with Apache)
# Download from: https://www.apachelounge.com/download/
```

---

## 🎯 TEST SCENARIOS

### **Scenario 1: Current Configuration (All Middleware)**

**Expected:**
```
Throughput:     50,000 - 80,000 req/s
Latency (p50):  1.5ms
Latency (p99):  5ms
```

**Run:**
```bash
cd e:\15. Other\API_Gateway\APIGateway\APIGateway

# Start gateway (current config)
dotnet run --urls http://0.0.0.0:5151

# In another terminal, benchmark
wrk -t8 -c1000 -d30s http://localhost:5151/test/echo
```

---

### **Scenario 2: Minimal Configuration (Only YARP)**

**Expected:**
```
Throughput:     120,000 - 150,000 req/s
Latency (p50):  0.5-1ms
Latency (p99):  2-3ms
```

**Setup:**
```bash
cd e:\15. Other\API_Gateway\APIGateway\APIGateway

# Backup current Program.cs
copy Program.cs Program.Full.cs

# Use minimal config
copy Program.Minimal.cs Program.cs

# Build release
dotnet build -c Release

# Run gateway
dotnet run -c Release --urls http://0.0.0.0:5151
```

**Benchmark:**
```bash
# Test 1: wrk (8 threads, 1000 connections, 30 seconds)
wrk -t8 -c1000 -d30s http://localhost:5151/test/echo

# Test 2: wrk with more connections
wrk -t16 -c2000 -d60s http://localhost:5151/test/echo

# Test 3: Apache Bench
ab -n 100000 -c 1000 http://localhost:5151/test/echo

# Test 4: hey
hey -n 100000 -c 1000 http://localhost:5151/test/echo

# Test 5: Different payload sizes
wrk -t8 -c1000 -d30s -s post.lua http://localhost:5151/test/echo
```

**Restore:**
```bash
# Restore original config
copy Program.Full.cs Program.cs
```

---

## 📊 BENCHMARK COMMANDS

### **1. Basic Throughput Test**

```bash
wrk -t8 -c1000 -d30s http://localhost:5151/test/echo
```

**Parameters:**
- `-t8`: 8 threads
- `-c1000`: 1000 concurrent connections
- `-d30s`: 30 seconds duration

**Expected output:**
```
Running 30s test @ http://localhost:5151/test/echo
  8 threads and 1000 connections
  Thread Stats   Avg      Stdev     Max   +/- Stdev
    Latency     0.85ms    1.23ms   50.00ms   95.67%
    Req/Sec    15.2k     2.1k    20.5k    89.23%
  3,650,000 requests in 30.00s, 450.00MB read
Requests/sec: 121,667
Transfer/sec:     15.00MB
```

---

### **2. High Concurrency Test**

```bash
wrk -t16 -c5000 -d60s http://localhost:5151/test/echo
```

**Parameters:**
- `-t16`: 16 threads
- `-c5000`: 5000 concurrent connections
- `-d60s`: 60 seconds duration

---

### **3. POST Request Test**

**Create post.lua:**
```lua
wrk.method = "POST"
wrk.body   = '{"test":"data","timestamp":1234567890}'
wrk.headers["Content-Type"] = "application/json"
```

**Run:**
```bash
wrk -t8 -c1000 -d30s -s post.lua http://localhost:5151/test/echo
```

---

### **4. Latency Distribution Test**

```bash
wrk -t8 -c1000 -d30s --latency http://localhost:5151/test/echo
```

**Expected output:**
```
Latency Distribution
   50%    0.85ms
   75%    1.20ms
   90%    1.80ms
   99%    3.50ms
```

---

### **5. Apache Bench Test**

```bash
ab -n 100000 -c 1000 -g results.tsv http://localhost:5151/test/echo
```

**Parameters:**
- `-n 100000`: 100,000 total requests
- `-c 1000`: 1000 concurrent requests
- `-g results.tsv`: Save results to file

**Expected output:**
```
Requests per second:    120,000 [#/sec] (mean)
Time per request:       8.333 [ms] (mean)
Time per request:       0.008 [ms] (mean, across all concurrent requests)
Transfer rate:          15,000 [Kbytes/sec] received
```

---

### **6. Hey Test (Go-based)**

```bash
hey -n 100000 -c 1000 -q 150000 http://localhost:5151/test/echo
```

**Parameters:**
- `-n 100000`: 100,000 requests
- `-c 1000`: 1000 workers
- `-q 150000`: Rate limit 150k req/s

**Expected output:**
```
Summary:
  Total:        0.8333 secs
  Slowest:      0.0050 secs
  Fastest:      0.0005 secs
  Average:      0.0008 secs
  Requests/sec: 120,000

Response time histogram:
  0.001 [10000]  |■■■■■■■■■■■■■■■■■■■■
  0.002 [70000]  |■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
  0.003 [15000]  |■■■■■■■■
  0.004 [4000]   |■■
  0.005 [1000]   |
```

---

## 📈 COMPARISON TESTS

### **Test A: Direct Backend (Baseline)**

```bash
# Start backend only
cd e:\15. Other\API_Gateway\TestBackend
dotnet run --urls http://0.0.0.0:5001

# Benchmark direct
wrk -t8 -c1000 -d30s http://localhost:5001/echo
```

**Expected:** ~140,000 req/s

---

### **Test B: YARP Gateway (Minimal)**

```bash
# Start gateway (minimal config)
cd e:\15. Other\API_Gateway\APIGateway\APIGateway
dotnet run -c Release --urls http://0.0.0.0:5151

# Benchmark through gateway
wrk -t8 -c1000 -d30s http://localhost:5151/test/echo
```

**Expected:** ~120,000 req/s

---

### **Test C: YARP Gateway (Full Middleware)**

```bash
# Start gateway (full config)
cd e:\15. Other\API_Gateway\APIGateway\APIGateway
# Use Program.Full.cs
dotnet run -c Release --urls http://0.0.0.0:5151

# Benchmark through gateway
wrk -t8 -c1000 -d30s http://localhost:5151/test/echo
```

**Expected:** ~50,000-80,000 req/s

---

### **Overhead Calculation:**

```
Direct Backend:         140,000 req/s (100%)
YARP Minimal:           120,000 req/s (85.7%)
YARP Full:              80,000 req/s (57.1%)

Overhead:
- YARP Minimal:         14.3%
- YARP Full:            42.9%
- Middleware Cost:      28.6%
```

---

## 🔬 DETAILED ANALYSIS

### **CPU Profiling:**

```bash
# Install dotnet-trace
dotnet tool install --global dotnet-trace

# Start profiling
dotnet-trace collect --process-id <PID> --profile cpu-sampling

# Run benchmark
wrk -t8 -c1000 -d30s http://localhost:5151/test/echo

# Stop profiling (Ctrl+C)
# Analyze with PerfView or speedscope.app
```

---

### **Memory Profiling:**

```bash
# Install dotnet-counters
dotnet tool install --global dotnet-counters

# Monitor memory
dotnet-counters monitor --process-id <PID> System.Runtime

# Watch for:
# - GC Heap Size
# - Gen 0/1/2 Collections
# - Allocation Rate
```

---

### **Network Monitoring:**

```bash
# Windows: Resource Monitor
resmon.exe

# Or use Performance Monitor
perfmon.exe

# Monitor:
# - Network Bytes/sec
# - TCP Connections
# - CPU Usage
```

---

## 📊 RESULTS TEMPLATE

**Test Results:**

```
Date:               2026-04-03
Configuration:      Minimal / Full
Hardware:           8 cores, 16 GB RAM
Network:            1 Gbps

Direct Backend:
- Throughput:       _____ req/s
- Latency (p50):    _____ ms
- Latency (p99):    _____ ms
- CPU Usage:        _____ %

YARP Gateway:
- Throughput:       _____ req/s
- Latency (p50):    _____ ms
- Latency (p99):    _____ ms
- CPU Usage:        _____ %
- Memory:           _____ MB

Overhead:           _____ %
Success Rate:       _____ %
Errors:             _____ 

Notes:
- 
- 
```

---

## 🎯 OPTIMIZATION CHECKLIST

**Before benchmarking:**

```
✅ Build in Release mode
✅ Disable logging (LogLevel.Warning)
✅ Close unnecessary applications
✅ Disable antivirus temporarily
✅ Use wired network (not WiFi)
✅ Run backend on same machine (localhost)
✅ Warm up (run 10s test first)
```

**Kestrel optimizations:**

```
✅ MaxConcurrentConnections = 10000
✅ MaxRequestBodySize = 10 MB
✅ MinRequestBodyDataRate = null
✅ MinResponseDataRate = null
✅ Http2.MaxStreamsPerConnection = 100
```

**HTTP client optimizations:**

```
✅ PooledConnectionLifetime = 2 minutes
✅ PooledConnectionIdleTimeout = 1 minute
✅ MaxConnectionsPerServer = 1000
✅ EnableMultipleHttp2Connections = true
```

---

## 🚀 QUICK START

**Run full benchmark suite:**

```bash
# 1. Start backend
cd e:\15. Other\API_Gateway\TestBackend
start dotnet run --urls http://0.0.0.0:5001

# 2. Start gateway (minimal)
cd e:\15. Other\API_Gateway\APIGateway\APIGateway
copy Program.Minimal.cs Program.cs
start dotnet run -c Release --urls http://0.0.0.0:5151

# 3. Wait 5 seconds for warmup
timeout /t 5

# 4. Run benchmark
wrk -t8 -c1000 -d30s --latency http://localhost:5151/test/echo

# 5. Save results
wrk -t8 -c1000 -d30s --latency http://localhost:5151/test/echo > results.txt

# 6. Restore original config
copy Program.Full.cs Program.cs
```

---

## 📈 EXPECTED RESULTS

### **Minimal Configuration:**

```
✅ Throughput:      120,000 - 150,000 req/s
✅ Latency (p50):   0.5 - 1ms
✅ Latency (p99):   2 - 3ms
✅ CPU Usage:       40 - 60%
✅ Memory:          500 MB - 1 GB
✅ Success Rate:    100%
```

### **Full Configuration:**

```
✅ Throughput:      50,000 - 80,000 req/s
✅ Latency (p50):   1.5ms
✅ Latency (p99):   5ms
✅ CPU Usage:       50 - 70%
✅ Memory:          800 MB - 1.5 GB
✅ Success Rate:    100%
```

---

**Status:** ✅ Ready for benchmarking  
**Next:** Run tests and compare results
