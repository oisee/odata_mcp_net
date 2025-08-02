#!/bin/bash

# Test Release build with forced logging

echo "🔍 Testing Release build with logging to file"

# Create a wrapper that captures stderr
cat > test-wrapper.sh << 'EOF'
#!/bin/bash
exec 2>release_stderr.log
./odata-mcp "$@"
EOF
chmod +x test-wrapper.sh

# Build and test
dotnet build --configuration Release -verbosity:quiet

echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | ./test-wrapper.sh --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints > release_output.json

echo "Output saved. Checking stderr log..."
if [ -f release_stderr.log ]; then
    echo "=== STDERR LOG ==="
    cat release_stderr.log
else
    echo "No stderr log found"
fi

echo ""
echo "=== JSON OUTPUT ==="
tail -1 release_output.json | jq '.'

# Cleanup
rm -f test-wrapper.sh release_stderr.log release_output.json