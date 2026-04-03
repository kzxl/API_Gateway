#!/bin/bash

# API Gateway Load Testing Script
# Requires: Apache Bench (ab), jq

API_BASE="http://localhost:5151"
API_KEY="gw-admin-key-change-me"

echo "=========================================="
echo "API Gateway Load Testing Suite"
echo "=========================================="
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Check if ab is installed
if ! command -v ab &> /dev/null; then
    echo -e "${RED}Error: Apache Bench (ab) is not installed${NC}"
    echo "Install: sudo apt-get install apache2-utils"
    exit 1
fi

# Get access token
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

# Test 1: Admin Routes Endpoint (With Auth)
echo -e "${BLUE}[TEST 1] Admin Routes Endpoint (With Auth)${NC}"
echo "Requests: 10,000 | Concurrency: 100"
ab -n 10000 -c 100 -q \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Api-Key: $API_KEY" \
  "$API_BASE/admin/routes" 2>&1 | grep -E "Requests per second|Time per request|Transfer rate|Failed requests"
echo ""

# Test 2: Admin Routes Endpoint (Higher Concurrency)
echo -e "${BLUE}[TEST 2] Admin Routes Endpoint (High Concurrency)${NC}"
echo "Requests: 50,000 | Concurrency: 500"
ab -n 50000 -c 500 -q \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Api-Key: $API_KEY" \
  "$API_BASE/admin/routes" 2>&1 | grep -E "Requests per second|Time per request|Transfer rate|Failed requests"
echo ""

# Test 3: Health Endpoint (No Auth)
echo -e "${BLUE}[TEST 3] Health Endpoint (No Auth)${NC}"
echo "Requests: 100,000 | Concurrency: 1000"
ab -n 100000 -c 1000 -q \
  "$API_BASE/health" 2>&1 | grep -E "Requests per second|Time per request|Transfer rate|Failed requests"
echo ""

# Test 4: Auth Refresh Endpoint
echo -e "${BLUE}[TEST 4] Auth Refresh Endpoint${NC}"
echo "Requests: 10,000 | Concurrency: 100"

REFRESH_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.refreshToken')
echo "{\"refreshToken\":\"$REFRESH_TOKEN\"}" > /tmp/refresh.json

ab -n 10000 -c 100 -q \
  -p /tmp/refresh.json -T application/json \
  "$API_BASE/auth/refresh" 2>&1 | grep -E "Requests per second|Time per request|Transfer rate|Failed requests"
echo ""

# Test 5: Auth Login Endpoint
echo -e "${BLUE}[TEST 5] Auth Login Endpoint${NC}"
echo "Requests: 5,000 | Concurrency: 50"

echo '{"username":"admin","password":"admin123"}' > /tmp/login.json

ab -n 5000 -c 50 -q \
  -p /tmp/login.json -T application/json \
  "$API_BASE/auth/login" 2>&1 | grep -E "Requests per second|Time per request|Transfer rate|Failed requests"
echo ""

# Test 6: Sustained Load Test (1 minute)
echo -e "${BLUE}[TEST 6] Sustained Load Test (60 seconds)${NC}"
echo "Duration: 60s | Concurrency: 200"
ab -t 60 -c 200 -q \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Api-Key: $API_KEY" \
  "$API_BASE/admin/routes" 2>&1 | grep -E "Requests per second|Time per request|Transfer rate|Failed requests|Complete requests"
echo ""

# Cleanup
rm -f /tmp/refresh.json /tmp/login.json

echo "=========================================="
echo "Load Testing Complete"
echo "=========================================="
echo ""
echo -e "${YELLOW}Performance Summary:${NC}"
echo "- Admin endpoints should achieve 12,000-20,000 req/s"
echo "- Health endpoint should achieve 25,000-35,000 req/s"
echo "- Auth refresh should achieve 10,000-20,000 req/s"
echo "- Auth login should achieve 500-1,500 req/s (BCrypt limited)"
echo ""
echo -e "${YELLOW}Note:${NC} Results depend on hardware, OS, and system load"
