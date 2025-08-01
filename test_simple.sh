#!/bin/bash

echo "Testing simple MCP echo..."

# Build first
echo "Building project..."
dotnet build src/ODataMcp/ODataMcp.csproj -c Release --nologo -v quiet

# Run the binary directly instead of dotnet run
BINARY="src/ODataMcp/bin/Release/net8.0/odata-mcp"

echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}}' | \
"$BINARY" --service https://services.odata.org/V2/Northwind/Northwind.svc/ 2>&1 | grep -E '^\{.*\}$' || echo "No JSON response found"