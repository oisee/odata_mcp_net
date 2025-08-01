#!/bin/bash

# Test MCP initialization
echo "Testing MCP initialization..."

# Create a temporary file for the response
RESPONSE_FILE=$(mktemp)

# Send initialize request and capture response
echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}}' | \
timeout 5 dotnet run --project src/ODataMcp -- --service https://services.odata.org/V2/Northwind/Northwind.svc/ 2>/dev/null | \
head -n 1 > "$RESPONSE_FILE"

echo "Response:"
cat "$RESPONSE_FILE"
echo

# Check if we got a valid response
if grep -q '"result"' "$RESPONSE_FILE"; then
    echo "✅ Initialize successful"
else
    echo "❌ Initialize failed"
    exit 1
fi

# Test tools/list
echo -e "\nTesting tools/list..."
(echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}}'; \
 sleep 0.5; \
 echo '{"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}}') | \
timeout 5 dotnet run --project src/ODataMcp -- --service https://services.odata.org/V2/Northwind/Northwind.svc/ 2>/dev/null | \
tail -n 1 > "$RESPONSE_FILE"

echo "Response:"
cat "$RESPONSE_FILE"
echo

if grep -q '"tools"' "$RESPONSE_FILE"; then
    echo "✅ Tools list successful"
else
    echo "❌ Tools list failed"
fi

# Cleanup
rm -f "$RESPONSE_FILE"