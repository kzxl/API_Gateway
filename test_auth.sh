#!/bin/bash

# API Gateway Authentication Testing Script
# Usage: ./test_auth.sh

API_BASE="http://localhost:5151"
API_KEY="gw-admin-key-change-me"

echo "=========================================="
echo "API Gateway Authentication Test Suite"
echo "=========================================="
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test 1: Login
echo -e "${YELLOW}[TEST 1] Login with admin credentials${NC}"
LOGIN_RESPONSE=$(curl -s -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}')

echo "$LOGIN_RESPONSE" | jq '.'

ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.accessToken')
REFRESH_TOKEN=$(echo "$LOGIN_RESPONSE" | jq -r '.refreshToken')

if [ "$ACCESS_TOKEN" != "null" ] && [ -n "$ACCESS_TOKEN" ]; then
    echo -e "${GREEN}✓ Login successful${NC}"
else
    echo -e "${RED}✗ Login failed${NC}"
    exit 1
fi
echo ""

# Test 2: Validate Token
echo -e "${YELLOW}[TEST 2] Validate access token${NC}"
VALIDATE_RESPONSE=$(curl -s -X POST "$API_BASE/auth/validate" \
  -H "Content-Type: application/json" \
  -d "{\"token\":\"$ACCESS_TOKEN\"}")

echo "$VALIDATE_RESPONSE" | jq '.'

IS_VALID=$(echo "$VALIDATE_RESPONSE" | jq -r '.valid')
if [ "$IS_VALID" = "true" ]; then
    echo -e "${GREEN}✓ Token is valid${NC}"
else
    echo -e "${RED}✗ Token validation failed${NC}"
fi
echo ""

# Test 3: Access Protected Endpoint (Routes)
echo -e "${YELLOW}[TEST 3] Access protected endpoint (GET /admin/routes)${NC}"
ROUTES_RESPONSE=$(curl -s -X GET "$API_BASE/admin/routes" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Api-Key: $API_KEY")

echo "$ROUTES_RESPONSE" | jq '.' | head -20

if echo "$ROUTES_RESPONSE" | jq -e 'type == "array"' > /dev/null 2>&1; then
    echo -e "${GREEN}✓ Protected endpoint accessible${NC}"
else
    echo -e "${RED}✗ Failed to access protected endpoint${NC}"
fi
echo ""

# Test 4: Access Without Token (Should Fail)
echo -e "${YELLOW}[TEST 4] Access protected endpoint without token (should fail)${NC}"
NO_TOKEN_RESPONSE=$(curl -s -w "\nHTTP_CODE:%{http_code}" -X GET "$API_BASE/admin/routes" \
  -H "X-Api-Key: $API_KEY")

HTTP_CODE=$(echo "$NO_TOKEN_RESPONSE" | grep "HTTP_CODE" | cut -d: -f2)
if [ "$HTTP_CODE" = "401" ]; then
    echo -e "${GREEN}✓ Correctly rejected unauthorized request (401)${NC}"
else
    echo -e "${RED}✗ Should have returned 401, got $HTTP_CODE${NC}"
fi
echo ""

# Test 5: Refresh Token
echo -e "${YELLOW}[TEST 5] Refresh access token${NC}"
REFRESH_RESPONSE=$(curl -s -X POST "$API_BASE/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}")

echo "$REFRESH_RESPONSE" | jq '.'

NEW_ACCESS_TOKEN=$(echo "$REFRESH_RESPONSE" | jq -r '.accessToken')
NEW_REFRESH_TOKEN=$(echo "$REFRESH_RESPONSE" | jq -r '.refreshToken')

if [ "$NEW_ACCESS_TOKEN" != "null" ] && [ -n "$NEW_ACCESS_TOKEN" ]; then
    echo -e "${GREEN}✓ Token refresh successful${NC}"
    ACCESS_TOKEN="$NEW_ACCESS_TOKEN"
    REFRESH_TOKEN="$NEW_REFRESH_TOKEN"
else
    echo -e "${RED}✗ Token refresh failed${NC}"
fi
echo ""

# Test 6: Logout
echo -e "${YELLOW}[TEST 6] Logout (revoke tokens)${NC}"
LOGOUT_RESPONSE=$(curl -s -X POST "$API_BASE/auth/logout" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}")

echo "$LOGOUT_RESPONSE" | jq '.'

if echo "$LOGOUT_RESPONSE" | jq -e '.message' > /dev/null 2>&1; then
    echo -e "${GREEN}✓ Logout successful${NC}"
else
    echo -e "${RED}✗ Logout failed${NC}"
fi
echo ""

# Test 7: Try Using Revoked Token (Should Fail)
echo -e "${YELLOW}[TEST 7] Try using revoked token (should fail)${NC}"
sleep 1  # Wait for blacklist to propagate
REVOKED_RESPONSE=$(curl -s -w "\nHTTP_CODE:%{http_code}" -X GET "$API_BASE/admin/routes" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "X-Api-Key: $API_KEY")

HTTP_CODE=$(echo "$REVOKED_RESPONSE" | grep "HTTP_CODE" | cut -d: -f2)
if [ "$HTTP_CODE" = "401" ]; then
    echo -e "${GREEN}✓ Correctly rejected revoked token (401)${NC}"
else
    echo -e "${RED}✗ Should have returned 401, got $HTTP_CODE${NC}"
fi
echo ""

# Test 8: Try Using Revoked Refresh Token (Should Fail)
echo -e "${YELLOW}[TEST 8] Try using revoked refresh token (should fail)${NC}"
REVOKED_REFRESH_RESPONSE=$(curl -s -w "\nHTTP_CODE:%{http_code}" -X POST "$API_BASE/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}")

HTTP_CODE=$(echo "$REVOKED_REFRESH_RESPONSE" | grep "HTTP_CODE" | cut -d: -f2)
if [ "$HTTP_CODE" = "401" ]; then
    echo -e "${GREEN}✓ Correctly rejected revoked refresh token (401)${NC}"
else
    echo -e "${RED}✗ Should have returned 401, got $HTTP_CODE${NC}"
fi
echo ""

echo "=========================================="
echo "Test Suite Complete"
echo "=========================================="
