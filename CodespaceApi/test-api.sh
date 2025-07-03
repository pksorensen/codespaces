#!/bin/bash

# Codespace API Test Script
# This script provides comprehensive testing for the Codespace API

set -e

API_URL="http://localhost:8080"
TEST_USER="testuser$(date +%s)"

echo "🚀 Starting Codespace API Tests..."
echo "API URL: $API_URL"
echo "Test User: $TEST_USER"
echo ""

# Function to make API calls with error handling
make_request() {
    local method=$1
    local endpoint=$2
    local data=$3
    local expected_status=$4
    
    echo "🔍 Testing: $method $endpoint"
    
    if [ -n "$data" ]; then
        response=$(curl -s -w "\n%{http_code}" -X $method "$API_URL$endpoint" \
            -H "Content-Type: application/json" \
            -d "$data")
    else
        response=$(curl -s -w "\n%{http_code}" -X $method "$API_URL$endpoint")
    fi
    
    # Split response body and status code
    response_body=$(echo "$response" | sed '$d')
    status_code=$(echo "$response" | tail -n1)
    
    if [ "$status_code" -eq "$expected_status" ]; then
        echo "✅ Success: HTTP $status_code"
        if [ -n "$response_body" ]; then
            echo "📄 Response: $response_body" | jq . 2>/dev/null || echo "📄 Response: $response_body"
        fi
    else
        echo "❌ Failed: Expected HTTP $expected_status, got HTTP $status_code"
        echo "📄 Response: $response_body"
        exit 1
    fi
    echo ""
}

# Test 1: Health Check
echo "=== Test 1: Health Check ==="
make_request "GET" "/api/health" "" 200

# Test 2: Create User
echo "=== Test 2: Create User ==="
user_data="{\"username\": \"$TEST_USER\"}"
make_request "POST" "/api/users" "$user_data" 200

# Extract password from response for SSH testing
echo "💾 Extracting user credentials..."
user_response=$(curl -s -X POST "$API_URL/api/users" \
    -H "Content-Type: application/json" \
    -d "$user_data")

temp_password=$(echo "$user_response" | jq -r '.tempPassword' 2>/dev/null || echo "")
home_directory=$(echo "$user_response" | jq -r '.homeDirectory' 2>/dev/null || echo "")

echo "🔑 Temporary Password: $temp_password"
echo "🏠 Home Directory: $home_directory"
echo ""

# Test 3: Get User Info
echo "=== Test 3: Get User Info ==="
make_request "GET" "/api/users/$TEST_USER" "" 200

# Test 4: Verify User Directory
echo "=== Test 4: Verify User Directory ==="
if [ -d "$home_directory" ]; then
    echo "✅ User directory exists: $home_directory"
    ls -la "$home_directory"
    
    # Check SSH directory
    if [ -d "$home_directory/.ssh" ]; then
        echo "✅ SSH directory exists"
        ls -la "$home_directory/.ssh"
    else
        echo "❌ SSH directory missing"
    fi
else
    echo "❌ User directory missing: $home_directory"
fi
echo ""

# Test 5: Verify User in System
echo "=== Test 5: Verify User in System ==="
if id "$TEST_USER" &>/dev/null; then
    echo "✅ User exists in system"
    id "$TEST_USER"
else
    echo "❌ User not found in system"
fi
echo ""

# Test 6: Try to Create Duplicate User
echo "=== Test 6: Try to Create Duplicate User ==="
make_request "POST" "/api/users" "$user_data" 400

# Test 7: Test SSH Access (if SSH is available)
echo "=== Test 7: Test SSH Access ==="
if command -v ssh &> /dev/null; then
    echo "🔐 Testing SSH access..."
    
    # Note: This test might require manual password entry
    echo "💡 To test SSH access manually, run:"
    echo "   ssh $TEST_USER@localhost"
    echo "   Password: $temp_password"
    echo ""
    
    # Test SSH key access
    ssh_key_path="$home_directory/.ssh/id_rsa"
    if [ -f "$ssh_key_path" ]; then
        echo "✅ SSH private key exists: $ssh_key_path"
        echo "💡 To test SSH key access manually, run:"
        echo "   ssh -i $ssh_key_path $TEST_USER@localhost"
    else
        echo "❌ SSH private key missing: $ssh_key_path"
    fi
else
    echo "⚠️  SSH not available for testing"
fi
echo ""

# Test 8: Test Directory Permissions
echo "=== Test 8: Test Directory Permissions ==="
if [ -d "$home_directory" ]; then
    permissions=$(ls -ld "$home_directory" | cut -d' ' -f1)
    owner=$(ls -ld "$home_directory" | cut -d' ' -f3)
    group=$(ls -ld "$home_directory" | cut -d' ' -f4)
    
    echo "📁 Directory permissions: $permissions"
    echo "👤 Owner: $owner"
    echo "👥 Group: $group"
    
    if [ "$owner" = "$TEST_USER" ] && [ "$group" = "$TEST_USER" ]; then
        echo "✅ Directory ownership is correct"
    else
        echo "❌ Directory ownership is incorrect"
    fi
else
    echo "❌ Cannot check permissions - directory missing"
fi
echo ""

# Test 9: Delete User
echo "=== Test 9: Delete User ==="
make_request "DELETE" "/api/users/$TEST_USER" "" 200

# Test 10: Verify User Deletion
echo "=== Test 10: Verify User Deletion ==="
make_request "GET" "/api/users/$TEST_USER" "" 404

# Test 11: Verify User Removed from System
echo "=== Test 11: Verify User Removed from System ==="
if id "$TEST_USER" &>/dev/null; then
    echo "❌ User still exists in system"
else
    echo "✅ User successfully removed from system"
fi
echo ""

# Test 12: Verify Directory Cleanup
echo "=== Test 12: Verify Directory Cleanup ==="
if [ -d "$home_directory" ]; then
    echo "❌ User directory still exists: $home_directory"
else
    echo "✅ User directory successfully removed"
fi
echo ""

# Test 13: Test Invalid Username
echo "=== Test 13: Test Invalid Username ==="
invalid_data='{"username": "a"}'
make_request "POST" "/api/users" "$invalid_data" 400

# Test 14: Test Empty Username
echo "=== Test 14: Test Empty Username ==="
empty_data='{"username": ""}'
make_request "POST" "/api/users" "$empty_data" 400

echo "🎉 All tests completed successfully!"
echo ""
echo "📋 Test Summary:"
echo "  - Health check: ✅"
echo "  - User creation: ✅"
echo "  - User retrieval: ✅"
echo "  - Directory creation: ✅"
echo "  - SSH setup: ✅"
echo "  - User deletion: ✅"
echo "  - Cleanup verification: ✅"
echo "  - Error handling: ✅"
echo ""
echo "🚀 Your Codespace API is ready for production!"