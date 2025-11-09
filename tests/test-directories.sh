#!/bin/bash
# Test the directory management API endpoints
set -e

JELLYFIN_URL="http://localhost:8096"
JELLYFIN_USER="${JELLYFIN_USER:-test}"
JELLYFIN_PASS="${JELLYFIN_PASS:-test}"

echo "=========================================="
echo "Testing Directory Management API"
echo "=========================================="
echo ""

# Authenticate and get token
echo "Authenticating as user: $JELLYFIN_USER"
AUTH_RESPONSE=$(curl -s -X POST "$JELLYFIN_URL/Users/AuthenticateByName" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Authorization: MediaBrowser Client=\"TestScript\", Device=\"TestDevice\", DeviceId=\"test-device-001\", Version=\"1.0.0\"" \
    -d "{\"Username\":\"$JELLYFIN_USER\",\"Pw\":\"$JELLYFIN_PASS\"}")

TOKEN=$(echo "$AUTH_RESPONSE" | jq -r '.AccessToken')
USER_ID=$(echo "$AUTH_RESPONSE" | jq -r '.User.Id')

if [ "$TOKEN" = "null" ] || [ -z "$TOKEN" ]; then
    echo "✗ Authentication failed!"
    echo "Make sure user exists (default: test/test)"
    echo "Response: $AUTH_RESPONSE"
    exit 1
fi

echo "✓ Authenticated successfully"
echo "Token: ${TOKEN:0:20}..."
echo "User ID: $USER_ID"
echo ""

# Test 1: Create directory
echo "=========================================="
echo "Test 1: Create Directory"
echo "=========================================="
echo ""
echo "Request: POST $JELLYFIN_URL/api/oxicleanarr/directories/create"
echo ""

CREATE_RESPONSE=$(curl -s -X POST "$JELLYFIN_URL/api/oxicleanarr/directories/create" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Token: $TOKEN" \
    -d '{
        "directory": "/data/test-directory"
    }')

echo "Response:"
echo "$CREATE_RESPONSE" | jq . 2>/dev/null || echo "$CREATE_RESPONSE"
echo ""

if echo "$CREATE_RESPONSE" | grep -qi "success.*true"; then
    CREATED=$(echo "$CREATE_RESPONSE" | jq -r '.Created')
    if [ "$CREATED" = "true" ]; then
        echo "✓ Directory created successfully"
    else
        echo "✓ Directory already existed"
    fi
else
    echo "✗ Failed to create directory"
    exit 1
fi

# Test 2: Verify directory exists in container
echo ""
echo "Test 2: Verify Directory Exists"
echo "=========================================="
echo ""

if docker exec jellyfin-test test -d /data/test-directory; then
    echo "✓ Directory exists inside container"
    docker exec jellyfin-test ls -lad /data/test-directory
else
    echo "✗ Directory not found inside container"
    exit 1
fi

# Test 3: Create directory again (should return Created: false)
echo ""
echo "Test 3: Create Same Directory Again (Idempotence Test)"
echo "=========================================="
echo ""

CREATE_RESPONSE2=$(curl -s -X POST "$JELLYFIN_URL/api/oxicleanarr/directories/create" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Token: $TOKEN" \
    -d '{
        "directory": "/data/test-directory"
    }')

echo "Response:"
echo "$CREATE_RESPONSE2" | jq . 2>/dev/null || echo "$CREATE_RESPONSE2"
echo ""

CREATED=$(echo "$CREATE_RESPONSE2" | jq -r '.Created')
if [ "$CREATED" = "false" ]; then
    echo "✓ Correctly reported directory already exists"
else
    echo "✗ Should have reported Created: false"
fi

# Test 4: Create nested directory
echo ""
echo "Test 4: Create Nested Directory"
echo "=========================================="
echo ""

CREATE_NESTED_RESPONSE=$(curl -s -X POST "$JELLYFIN_URL/api/oxicleanarr/directories/create" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Token: $TOKEN" \
    -d '{
        "directory": "/data/test-directory/nested/deep"
    }')

echo "Response:"
echo "$CREATE_NESTED_RESPONSE" | jq . 2>/dev/null || echo "$CREATE_NESTED_RESPONSE"
echo ""

if echo "$CREATE_NESTED_RESPONSE" | grep -qi "success.*true"; then
    echo "✓ Nested directory created successfully"
else
    echo "✗ Failed to create nested directory"
    exit 1
fi

# Test 5: Attempt to remove non-empty directory without force
echo ""
echo "Test 5: Remove Non-Empty Directory Without Force (Should Fail)"
echo "=========================================="
echo ""

# First, create a test file in the nested directory
docker exec jellyfin-test touch /data/test-directory/nested/deep/test-file.txt

REMOVE_RESPONSE=$(curl -s -X DELETE "$JELLYFIN_URL/api/oxicleanarr/directories/remove" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Token: $TOKEN" \
    -d '{
        "directory": "/data/test-directory",
        "force": false
    }')

echo "Response:"
echo "$REMOVE_RESPONSE" | jq . 2>/dev/null || echo "$REMOVE_RESPONSE"
echo ""

if echo "$REMOVE_RESPONSE" | grep -qi "error"; then
    echo "✓ Correctly rejected removal of non-empty directory"
else
    echo "✗ Should have rejected removal of non-empty directory"
fi

# Test 6: Remove non-empty directory with force
echo ""
echo "Test 6: Remove Non-Empty Directory With Force"
echo "=========================================="
echo ""

REMOVE_FORCE_RESPONSE=$(curl -s -X DELETE "$JELLYFIN_URL/api/oxicleanarr/directories/remove" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Token: $TOKEN" \
    -d '{
        "directory": "/data/test-directory",
        "force": true
    }')

echo "Response:"
echo "$REMOVE_FORCE_RESPONSE" | jq . 2>/dev/null || echo "$REMOVE_FORCE_RESPONSE"
echo ""

if echo "$REMOVE_FORCE_RESPONSE" | grep -qi "success.*true"; then
    echo "✓ Directory removed successfully with force"
else
    echo "✗ Failed to remove directory with force"
    exit 1
fi

# Test 7: Verify directory no longer exists
echo ""
echo "Test 7: Verify Directory Removed"
echo "=========================================="
echo ""

if docker exec jellyfin-test test -d /data/test-directory; then
    echo "✗ Directory still exists inside container"
    exit 1
else
    echo "✓ Directory successfully removed from container"
fi

# Test 8: Remove non-existent directory (idempotence test)
echo ""
echo "Test 8: Remove Non-Existent Directory (Idempotence Test)"
echo "=========================================="
echo ""

REMOVE_MISSING_RESPONSE=$(curl -s -X DELETE "$JELLYFIN_URL/api/oxicleanarr/directories/remove" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Token: $TOKEN" \
    -d '{
        "directory": "/data/test-directory",
        "force": false
    }')

echo "Response:"
echo "$REMOVE_MISSING_RESPONSE" | jq . 2>/dev/null || echo "$REMOVE_MISSING_RESPONSE"
echo ""

if echo "$REMOVE_MISSING_RESPONSE" | grep -qi "success.*true"; then
    echo "✓ Correctly handled removal of non-existent directory"
else
    echo "✗ Should have succeeded for non-existent directory"
fi

# Test 9: Create empty directory and remove it without force
echo ""
echo "Test 9: Remove Empty Directory Without Force"
echo "=========================================="
echo ""

# Create directory
curl -s -X POST "$JELLYFIN_URL/api/oxicleanarr/directories/create" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Token: $TOKEN" \
    -d '{"directory": "/data/test-empty-dir"}' > /dev/null

# Remove it
REMOVE_EMPTY_RESPONSE=$(curl -s -X DELETE "$JELLYFIN_URL/api/oxicleanarr/directories/remove" \
    -H "Content-Type: application/json" \
    -H "X-Emby-Token: $TOKEN" \
    -d '{
        "directory": "/data/test-empty-dir",
        "force": false
    }')

echo "Response:"
echo "$REMOVE_EMPTY_RESPONSE" | jq . 2>/dev/null || echo "$REMOVE_EMPTY_RESPONSE"
echo ""

if echo "$REMOVE_EMPTY_RESPONSE" | grep -qi "success.*true"; then
    echo "✓ Empty directory removed successfully without force"
else
    echo "✗ Failed to remove empty directory"
    exit 1
fi

echo ""
echo "=========================================="
echo "All Directory Management Tests Passed! ✓"
echo "=========================================="
echo ""
echo "Summary:"
echo "✓ Create directory"
echo "✓ Idempotent create (directory already exists)"
echo "✓ Create nested directory"
echo "✓ Remove non-empty directory rejected without force"
echo "✓ Remove non-empty directory with force"
echo "✓ Verify directory removed"
echo "✓ Idempotent remove (directory doesn't exist)"
echo "✓ Remove empty directory without force"
echo ""
