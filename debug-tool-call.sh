#!/bin/bash

echo "🔍 Debugging tool call routing..."

# Add debug to CallToolAsync 
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints 2>&1 | grep -E "(DEBUG|CallToolAsync|function|Function)" || echo "No debug messages found"