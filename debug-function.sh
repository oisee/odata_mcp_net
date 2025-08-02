#!/bin/bash

echo "🔍 Debugging function URL generation..."

# Test function call with logging to stderr
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | ASPNETCORE_ENVIRONMENT=Development ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints 2>&1 | grep -E "(Executing function|URL|400|Bad Request)"

echo ""
echo "Manual test of expected URL:"
curl -s "https://services.odata.org/V2/OData/OData.svc/GetProductsByRating?rating=3" | head -5