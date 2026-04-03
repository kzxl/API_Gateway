# 📊 Performance Estimation: 12 vCPU + 16GB RAM

**Date:** 2026-04-03  
**Hardware:** 12 vCPU, 16 GB RAM  
**Analysis:** Node.js Gateway throughput capacity

---

## 🎯 THROUGHPUT ESTIMATION

### **Node.js Gateway (PM2 Cluster Mode)**

**Configuration:**
```
CPU:                12 vCPU
RAM:                16 GB
PM2 Instances:      12 (1 per vCPU)
Instance Memory:    ~100 MB each
Total Memory Used:  ~1.2 GB (12 instances)
Available RAM:      14.8 GB (plenty of headroom)
```

**Performance per Instance:**
```
Single Instance:    10,000 - 15,000 req/s
Latency (p50):      5-10ms
Memory:             50-100 MB
CPU:                100% (1 core)
```

**Total Performance (12 Instances):**
```
Throughput:         120,000 - 180,000 req/s
Latency (p50):      5-10ms (unchanged)
Memory:             600 MB - 1.2 GB
CPU:                100% (12 cores)
Load Balancing:     PM2 round-robin
```

---

## 📈 DETAILED BREAKDOWN

### **Scenario 1: Conservative (10k req/s per instance)**

```
12 instances × 10,000 req/s = 120,000 req/s
```

**Resource Usage:**
```
CPU:        100% (12 cores)
Memory:     ~600 MB (50 MB × 12)
Network:    ~15 MB/s (with 1KB payload)
Headroom:   15.4 GB RAM available
```

---

### **Scenario 2: Optimal (15k req/s per instance)**

```
12 instances × 15,000 req/s = 180,000 req/s
```

**Resource Usage:**
```
CPU:        100% (12 cores)
Memory:     ~1.2 GB (100 MB × 12)
Network:    ~22 MB/s (with 1KB payload)
Headroom:   14.8 GB RAM available
```

---

### **Scenario 3: Peak (with optimization)**

```
With optimizations:
- HTTP/2
- Connection pooling
- Optimized buffer sizes

12 instances × 20,000 req/s = 240,000 req/s
```

**Resource Usage:**
```
CPU:        100% (12 cores)
Memory:     ~1.5 GB (125 MB × 12)
Network:    ~30 MB/s (with 1KB payload)
Headroom:   14.5 GB RAM available
```

---

## 🔥 COMPARISON WITH OTHER BACKENDS

### **12 vCPU + 16GB RAM:**

| Backend | Throughput | Memory | Latency (p50) |
|---------|------------|--------|---------------|
| **Node.js (12 instances)** | 120k-180k req/s | 1.2 GB | 5-10ms |
| **Go (single binary)** | 200k-250k req/s | 100 MB | 0.3ms |
| **.NET 8 (YARP)** | 150k-200k req/s | 1 GB | 0.8ms |

**Node.js với 12 cores:**
- Throughput tương đương .NET 8
- Memory usage thấp hơn .NET 8
- Latency cao hơn Go và .NET 8
- Dễ deploy và maintain nhất

---

## 🎯 REALISTIC EXPECTATIONS

### **Production Environment:**

**Conservative (Safe):**
```
Target:             100,000 req/s
Instances:          12
Per Instance:       ~8,300 req/s
CPU Usage:          ~80%
Memory:             ~1 GB
Headroom:           20% CPU, 15 GB RAM
```

**Optimal (Recommended):**
```
Target:             150,000 req/s
Instances:          12
Per Instance:       ~12,500 req/s
CPU Usage:          ~95%
Memory:             ~1.2 GB
Headroom:           5% CPU, 14.8 GB RAM
```

**Peak (Maximum):**
```
Target:             180,000 req/s
Instances:          12
Per Instance:       ~15,000 req/s
CPU Usage:          100%
Memory:             ~1.5 GB
Headroom:           0% CPU, 14.5 GB RAM
```

---

## 📊 NETWORK BANDWIDTH

### **Bandwidth Requirements:**

**100k req/s:**
```
Payload:            1 KB per request
Incoming:           ~100 MB/s (800 Mbps)
Outgoing:           ~100 MB/s (800 Mbps)
Total:              ~200 MB/s (1.6 Gbps)
Network:            1 Gbps ❌ (insufficient)
                    10 Gbps ✅ (sufficient)
```

**150k req/s:**
```
Payload:            1 KB per request
Incoming:           ~150 MB/s (1.2 Gbps)
Outgoing:           ~150 MB/s (1.2 Gbps)
Total:              ~300 MB/s (2.4 Gbps)
Network:            10 Gbps ✅ (sufficient)
```

**180k req/s:**
```
Payload:            1 KB per request
Incoming:           ~180 MB/s (1.44 Gbps)
Outgoing:           ~180 MB/s (1.44 Gbps)
Total:              ~360 MB/s (2.88 Gbps)
Network:            10 Gbps ✅ (sufficient)
```

**⚠️ Important:** Cần 10 Gbps network để đạt >100k req/s

---

## 🔧 OPTIMIZATION FOR 12 vCPU

### **PM2 Configuration:**

```javascript
// ecosystem.config.js
module.exports = {
  apps: [{
    name: 'gateway-node',
    script: './server-uarch.js',
    instances: 12,              // Use all 12 vCPU
    exec_mode: 'cluster',
    max_memory_restart: '500M', // Restart if > 500MB
    env: {
      NODE_ENV: 'production',
      PORT: 8887,
      UV_THREADPOOL_SIZE: 128   // Increase thread pool
    }
  }]
};
```

### **Node.js Optimizations:**

```bash
# Increase memory limit
node --max-old-space-size=8192 server-uarch.js

# Or in PM2
pm2 start ecosystem.config.js --node-args="--max-old-space-size=8192"
```

### **OS Optimizations (Linux):**

```bash
# Increase file descriptors
ulimit -n 65536

# Increase network buffers
sysctl -w net.core.rmem_max=16777216
sysctl -w net.core.wmem_max=16777216
sysctl -w net.ipv4.tcp_rmem="4096 87380 16777216"
sysctl -w net.ipv4.tcp_wmem="4096 65536 16777216"

# Increase connection tracking
sysctl -w net.netfilter.nf_conntrack_max=1000000
```

---

## 📈 SCALING SCENARIOS

### **Scenario A: 50k req/s (Light Load)**

```
PM2 Instances:      6 (50% of vCPU)
Per Instance:       ~8,300 req/s
CPU Usage:          ~50%
Memory:             ~600 MB
Headroom:           50% CPU, 15.4 GB RAM
Cost:               Optimal (not over-provisioned)
```

### **Scenario B: 100k req/s (Medium Load)**

```
PM2 Instances:      10 (83% of vCPU)
Per Instance:       ~10,000 req/s
CPU Usage:          ~83%
Memory:             ~1 GB
Headroom:           17% CPU, 15 GB RAM
Cost:               Good balance
```

### **Scenario C: 150k req/s (High Load)**

```
PM2 Instances:      12 (100% of vCPU)
Per Instance:       ~12,500 req/s
CPU Usage:          ~95%
Memory:             ~1.2 GB
Headroom:           5% CPU, 14.8 GB RAM
Cost:               Fully utilized
```

### **Scenario D: 180k req/s (Peak Load)**

```
PM2 Instances:      12 (100% of vCPU)
Per Instance:       ~15,000 req/s
CPU Usage:          100%
Memory:             ~1.5 GB
Headroom:           0% CPU, 14.5 GB RAM
Cost:               Maximum capacity
```

---

## 🎯 RECOMMENDATION

### **For 12 vCPU + 16GB RAM:**

**Target Throughput:** **150,000 req/s** (Recommended)

**Configuration:**
```bash
pm2 start ecosystem.config.js

# ecosystem.config.js
{
  instances: 12,
  exec_mode: 'cluster',
  max_memory_restart: '1G'
}
```

**Expected Performance:**
```
Throughput:         150,000 req/s
Latency (p50):      5-10ms
CPU Usage:          95%
Memory Usage:       1.2 GB
Network:            2.4 Gbps (need 10 Gbps)
Headroom:           5% CPU, 14.8 GB RAM
```

**Why 150k instead of 180k?**
```
✅ Leaves 5% CPU headroom for spikes
✅ More stable under load
✅ Better error handling capacity
✅ Room for background tasks
✅ Safer for production
```

---

## 🔥 COMPARISON: 8 vCPU vs 12 vCPU

| Metric | 8 vCPU | 12 vCPU | Improvement |
|--------|--------|---------|-------------|
| **Instances** | 8 | 12 | +50% |
| **Throughput** | 120k req/s | 180k req/s | +50% |
| **Memory** | 800 MB | 1.2 GB | +50% |
| **Cost** | Lower | Higher | +50% |
| **Efficiency** | Same | Same | - |

**Linear Scaling:** Node.js scales linearly with CPU cores

---

## 💰 COST ANALYSIS

### **Cloud Provider Pricing (Estimated):**

**12 vCPU + 16GB RAM:**
```
AWS EC2 (c5.3xlarge):       ~$200/month
Azure (F12s v2):            ~$180/month
GCP (n2-highcpu-12):        ~$190/month
```

**Cost per 1M requests:**
```
150k req/s × 86400s = 12.96B req/day
12.96B req/day × 30 days = 388.8B req/month

Cost: $200/month
Cost per 1M req: $0.0005 (very cheap!)
```

---

## ✅ FINAL ANSWER

### **12 vCPU + 16GB RAM:**

**Conservative (Safe):**
```
Throughput:         100,000 req/s
CPU Usage:          80%
Memory:             1 GB
Headroom:           20% CPU
```

**Recommended (Optimal):**
```
Throughput:         150,000 req/s ⭐
CPU Usage:          95%
Memory:             1.2 GB
Headroom:           5% CPU
```

**Maximum (Peak):**
```
Throughput:         180,000 req/s
CPU Usage:          100%
Memory:             1.5 GB
Headroom:           0% CPU
```

---

## 🎉 SUMMARY

**Với 12 vCPU + 16GB RAM:**

```
✅ Throughput:      150,000 - 180,000 req/s
✅ Memory:          1.2 - 1.5 GB (plenty of RAM left)
✅ Latency:         5-10ms
✅ Instances:       12 (PM2 cluster)
✅ Network:         Need 10 Gbps (2.4-2.9 Gbps used)
✅ Headroom:        5% CPU, 14.8 GB RAM
✅ Cost:            ~$200/month (cloud)
✅ Scalability:     Linear with CPU cores
```

**Recommendation:** Target **150,000 req/s** for production stability

---

**Status:** ✅ Analysis complete  
**Hardware:** 12 vCPU + 16GB RAM  
**Capacity:** 150,000 - 180,000 req/s  
**Bottleneck:** Network bandwidth (need 10 Gbps)
