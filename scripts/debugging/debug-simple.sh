#!/bin/bash

echo "🔍 Simple debug test..."

# Clean up old logs
rm -f odata_debug.log url_debug.log response_debug.log

# Test regular tool
echo "Testing regular tool (filter_Products_for_odata)..."
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "filter_Products_for_odata", "arguments": {"top": 1}}, "id": 2}' | ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints > /tmp/regular_test.out 2>&1

if [ -f "odata_debug.log" ]; then
    echo "✅ Regular tool call created debug log"
    cat odata_debug.log
else
    echo "❌ Regular tool call did not create debug log"
fi

# Clean logs and test function
echo ""
echo "Testing function (GetProductsByRating_for_odata)..."
rm -f odata_debug.log url_debug.log response_debug.log

echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints > /tmp/function_test.out 2>&1

if [ -f "odata_debug.log" ]; then
    echo "✅ Function call created debug log"
    cat odata_debug.log
else
    echo "❌ Function call did not create debug log"
fi

if [ -f "url_debug.log" ]; then
    echo "✅ URL debug log found:"
    cat url_debug.log
else
    echo "❌ No URL debug log"
fi

if [ -f "response_debug.log" ]; then
    echo "✅ Response debug log found:"
    cat response_debug.log
else
    echo "❌ No response debug log"
fi

echo ""
echo "Function test output:"
tail -3 /tmp/function_test.out