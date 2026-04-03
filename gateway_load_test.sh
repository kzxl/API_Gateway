#!/bin/bash

# Gateway Load Test Script - Test through Gateway Proxy
# This script tests the gateway's ability to proxy requests to a backend

API_BASE="http://localhost:5151"
BACKEND_BASE="http://localhost:5001"  # Backend service

echo "=========================================="
echo "API Gateway Proxy Load Test"
echo "=========================================="
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Check if backend is running
echo -e "${YELLOW}Checking if backend is available...${NC}"
if curl -s "$BACKEND_BASE/test/health" > /dev/null 2>&1; then
    echo -e "${GREEN}✓ Backend is running at $BACKEND_BASE${NC}"
else
    echo -e "${RED}✗ Backend is not running${NC}"
    echo "Starting mock backend on port 5001..."
    echo "Please run: dotnet run --urls http://localhost:5001"
    exit 1
fi
echo ""

# Test 1: Direct Backend Call (Baseline)
echo -e "${BLUE}[BASELINE] Direct Backend Call (No Gateway)${NC}"
echo "Requests: 10,000 | Concurrency: 100"
ab -n 10000 -c 100 -q "$BACKEND_BASE/test/echo" 2>&1 | grep -E "Requests per second|Time per request|Failed requests"
echo ""

# Test 2: Through Gateway (No Auth)
echo -e "${BLUE}[TEST 1] Through Gateway (No Auth)${NC}"
echo "Requests: 10,000 | Concurrency: 100"
ab -n 10000 -c 100 -q "$API_BASE/test/echo" 2>&1 | grep -E "Requests per second|Time per request|Failed requests"
echo ""

# Get access token for authenticated tests
echo -e "${YELLOW}Getting access token...${NC}"
LOGIN_RESPONSE=$(curl -s -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}')

ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.accessToken')

if [ "$ACCESS_TOKEN" = "null" ] || [ -z "$ACCESS_TOKEN" ]; then
    echo -e "${RED}Failed to get access token${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Access token obtained${NC}"
echo ""

# Test 3: Through Gateway (With Auth)
echo -e "${BLUE}[TEST 2] Through Gateway (With Auth)${NC}"
echo "Requests: 10,000 | Concurrency: 100"
ab -n 10000 -c 100 -q \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  "$API_BASE/test/echo" 2>&1 | grep -E "Requests per second|Time per request|Failed requests"
echo ""

# Test 4: High Concurrency (No Auth)
echo -e "${BLUE}[TEST 3] High Concurrency (No Auth)${NC}"
echo "Requests: 50,000 | Concurrency: 500"
ab -n 50000 -c 500 -q "$API_BASE/test/echo" 2>&1 | grep -E "Requests per second|Time per request|Failed requests"
echo ""

# Test 5: High Concurrency (With Auth)
echo -e "${BLUE}[TEST 4] High Concurrency (With Auth)${NC}"
echo "Requests: 50,000 | Concurrency: 500"
ab -n 50000 -c 500 -q \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  "$API_BASE/test/echo" 2>&1 | grep -E "Requests per second|Time per request|Failed requests"
echo ""

# Test 6: Extreme Concurrency (No Auth)
echo -e "${BLUE}[TEST 5] Extreme Concurrency (No Auth)${NC}"
echo "Requests: 100,000 | Concurrency: 1000"
ab -n 100000 -c 1000 -q "$API_BASE/test/echo" 2>&1 | grep -E "Requests per second|Time per request|Failed requests"
echo ""

# Test 7: With Backend Latency (100ms)
echo -e "${BLUE}[TEST 6] With Backend Latency (100ms delay)${NC}"
echo "Requests: 5,000 | Concurrency: 100"
ab -n 5000 -c 100 -q "$API_BASE/test/delay?delay=100" 2>&1 | grep -E "Requests per second|Time per request|Failed requests"
echo ""

# Test 8: CPU-Intensive Backend
echo -e "${BLUE}[TEST 7] CPU-Intensive Backend${NC}"
echo "Requests: 5,000 | Concurrency: 50"
ab -n 5000 -c 50 -q "$API_BASE/test/cpu?iterations=10000" 2>&1 | grep -E "Requests per second|Time per request|Failed requests"
echo ""

# Test 9: Sustained Load (60 seconds)
echo -e "${BLUE}[TEST 8] Sustained Load Test (60 seconds)${NC}"
echo "Duration: 60s | Concurrency: 200"
ab -t 60 -c 200 -q "$API_BASE/test/echo" 2>&1 | grep -E "Requests per second|Time per request|Failed requests|Complete requests"
echo ""

# Get final statistics
echo -e "${BLUE}[STATS] Gateway Statistics${NC}"
curl -s "$API_BASE/test/stats" | jq '.'
echo ""

echo "=========================================="
echo "Load Test Complete"
echo "=========================================="
echo ""
echo -e "${YELLOW}Performance Analysis:${NC}"
echo "1. Compare 'Direct Backend' vs 'Through Gateway' to measure gateway overhead"
echo "2. Compare 'No Auth' vs 'With Auth' to measure authentication overhead"
echo "3. Monitor CPU, memory, and network during tests"
echo ""
echo -e "${YELLOW}Expected Results:${NC}"
echo "- Gateway overhead: 5-10% latency increase"
echo "- Auth overhead: 10-20% latency increase"
echo "- Target throughput: 20,000-35,000 req/s (no auth)"
echo "- Target throughput: 15,000-25,000 req/s (with auth)"
