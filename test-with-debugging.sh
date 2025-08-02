#!/bin/bash

# Comprehensive debugging test script
# Inspired by MinZ's AI-driven testing revolution

echo "🚀 OData MCP Comprehensive Debug Test"
echo "====================================="

# Clean debug directory
rm -rf debug_output
mkdir -p debug_output

# Build with our new debugging
echo "📦 Building with debug instrumentation..."
dotnet build --configuration Release -verbosity:quiet || exit 1

echo ""
echo "🔍 Test 1: Regular tool call (should work)"
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "filter_Products_for_odata", "arguments": {"top": 1}}, "id": 2}' | ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints > debug_output/regular_tool.out 2>&1

echo ""
echo "🎯 Test 2: Function call (GetProductsByRating)"
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints > debug_output/function_call.out 2>&1

echo ""
echo "📊 Debug Report:"
echo "==============="

# Check if debug logs were created
if [ -d "debug_output" ] && [ "$(ls -A debug_output/*.log 2>/dev/null | wc -l)" -gt 0 ]; then
    echo "✅ Debug logs created:"
    ls -la debug_output/*.log | awk '{print "   " $9 " (" $5 " bytes)"}'
    
    echo ""
    echo "📋 Master log tail:"
    if [ -f "debug_output/master.log" ]; then
        tail -20 debug_output/master.log
    fi
    
    echo ""
    echo "🔍 Function-specific events:"
    if [ -f "debug_output/function_check.log" ] || [ -f "debug_output/function_url.log" ]; then
        echo "Function checks:"
        cat debug_output/function_*.log 2>/dev/null | jq -r '.message' | head -10
    fi
    
    echo ""
    echo "🌐 HTTP events:"
    if [ -f "debug_output/http_request.log" ]; then
        echo "HTTP requests:"
        cat debug_output/http_*.log 2>/dev/null | jq -r '.message' | head -10
    fi
else
    echo "❌ No debug logs found!"
    echo "Checking for any output..."
    ls -la debug_output/
fi

echo ""
echo "💡 Function call result:"
if [ -f "debug_output/function_call.out" ]; then
    tail -5 debug_output/function_call.out
fi

echo ""
echo "📁 All debug files are in: $(pwd)/debug_output/"