#!/bin/bash

# Test what URL is being requested by intercepting HTTP traffic

echo "🔍 Testing GetProductsByRating URL generation"

# First, let's manually test the expected URL
echo "✅ Expected URL test:"
curl -s -i "https://services.odata.org/V2/OData/OData.svc/GetProductsByRating?rating=3" | head -5

echo ""
echo "🔄 Now testing with our code..."

# Create a simple test that will show us HTTP errors
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints --verbose-errors 2>&1 | grep -A20 "FATAL ERROR" || echo "No detailed error found"

echo ""
echo "🧪 Let's also test if regular queries work:"
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "filter_Products_for_odata", "arguments": {"top": 1}}, "id": 2}' | ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints 2>&1 | jq -s '.[1].result.value | length' 2>/dev/null || echo "Regular query failed"