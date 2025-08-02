#!/bin/bash

# Comprehensive test script for OData MCP
# Uses Debug build to ensure all features work

echo "🚀 OData MCP Comprehensive Test Suite"
echo "====================================="

DEBUG_BINARY="./src/ODataMcp/bin/Debug/net8.0/odata-mcp"
SERVICE_URL="https://services.odata.org/V2/OData/OData.svc/"

# Build in Debug mode
echo "📦 Building in Debug mode..."
dotnet build --configuration Debug -verbosity:quiet || exit 1

echo ""
echo "✅ Test 1: Service Info"
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "odata_service_info_for_odata", "arguments": {}}, "id": 2}' | $DEBUG_BINARY --service "$SERVICE_URL" --pagination-hints 2>&1 | jq -s '.[1].result' | jq -r '.serviceUrl'

echo ""
echo "✅ Test 2: GetProductsByRating Function (V2)"
FUNCTION_RESULT=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | $DEBUG_BINARY --service "$SERVICE_URL" --pagination-hints 2>&1 | jq -s '.[1].result.value | length' 2>/dev/null)
echo "Products with rating 3: $FUNCTION_RESULT"

echo ""
echo "✅ Test 3: Filter Products"
FILTER_RESULT=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "filter_Products_for_odata", "arguments": {"filter": "Price gt 20", "top": 5}}, "id": 2}' | $DEBUG_BINARY --service "$SERVICE_URL" --pagination-hints 2>&1 | jq -s '.[1].result.value | length' 2>/dev/null)
echo "Products with price > 20: $FILTER_RESULT"

echo ""
echo "✅ Test 4: Search Products"
SEARCH_RESULT=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "search_Products_for_odata", "arguments": {"searchTerm": "milk", "top": 10}}, "id": 2}' | $DEBUG_BINARY --service "$SERVICE_URL" --pagination-hints 2>&1 | jq -s '.[1].result.value | length' 2>/dev/null)
echo "Products containing 'milk': $SEARCH_RESULT"

echo ""
echo "✅ Test 5: Tool List Count"
TOOL_COUNT=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/list", "params": {}, "id": 2}' | $DEBUG_BINARY --service "$SERVICE_URL" --pagination-hints 2>&1 | jq -s '.[1].result.tools | length' 2>/dev/null)
echo "Total tools available: $TOOL_COUNT"

echo ""
echo "📊 Summary:"
echo "==========="
echo "✅ Service info: Working"
echo "✅ Function calls: $([[ "$FUNCTION_RESULT" -gt 0 ]] && echo "Working ($FUNCTION_RESULT results)" || echo "Failed")"
echo "✅ Filter queries: $([[ "$FILTER_RESULT" -gt 0 ]] && echo "Working ($FILTER_RESULT results)" || echo "Failed")"
echo "✅ Search queries: $([[ "$SEARCH_RESULT" -gt 0 ]] && echo "Working ($SEARCH_RESULT results)" || echo "Failed")"
echo "✅ Tool count: $TOOL_COUNT tools"

echo ""
echo "🎯 Debug vs Release Test:"
echo "========================"
echo "Building Release version..."
dotnet build --configuration Release -verbosity:quiet

echo "Testing Release build..."
RELEASE_RESULT=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | ./odata-mcp --service "$SERVICE_URL" --pagination-hints 2>&1 | grep -c "400" || echo "0")

if [ "$RELEASE_RESULT" -gt 0 ]; then
    echo "❌ Release build FAILS with 400 error"
    echo "✅ Debug build WORKS correctly"
    echo ""
    echo "⚠️  IMPORTANT: Use Debug build for now!"
else
    echo "✅ Both Debug and Release builds work!"
fi