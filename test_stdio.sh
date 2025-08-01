#!/bin/bash

echo "=== Testing .NET OData MCP STDIO Implementation ==="
echo

# First build it
echo "Building..."
dotnet build src/ODataMcp/ODataMcp.csproj -c Release --nologo -v quiet || exit 1

BINARY="src/ODataMcp/bin/Release/net8.0/odata-mcp"

# Test 1: Initialize
echo "Test 1: Initialize request"
echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}}' | \
    "$BINARY" --service https://services.odata.org/V2/Northwind/Northwind.svc/ 2>/dev/null | head -1
echo

# Test 2: Initialize then tools/list
echo "Test 2: Initialize + tools/list"
(echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05"}}'; \
 echo '{"jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {}}') | \
    "$BINARY" --service https://services.odata.org/V2/Northwind/Northwind.svc/ --tool-shrink 2>/dev/null | tail -1
echo

# Test 3: Check trace mode output
echo "Test 3: Trace mode (should show configuration)"
"$BINARY" --service https://services.odata.org/V2/Northwind/Northwind.svc/ --trace 2>&1 | grep -E "(ServiceUrl|EntitySetCount|GeneratedTools)" | head -5
echo

# Compare with Go implementation if available
if [ -f "../odata_mcp_go/odata-mcp" ]; then
    echo "=== Comparing with Go implementation ==="
    echo "Go version initialize:"
    echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05"}}' | \
        ../odata_mcp_go/odata-mcp --service https://services.odata.org/V2/Northwind/Northwind.svc/ 2>/dev/null | head -1
fi