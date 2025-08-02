#!/bin/bash

# OData MCP Testing and Diagnostics Script
set -e

SERVICE_URL="${1:-https://services.odata.org/V2/OData/OData.svc/}"
BINARY="./odata-mcp"

echo "🚀 OData MCP Diagnostics Script"
echo "Service URL: $SERVICE_URL"
echo "Binary: $BINARY"
echo "=================================="

# Build first
echo "📦 Building project..."
dotnet build --configuration Release -verbosity:quiet
echo "✅ Build complete"

# Test 1: Initialize and get tool count
echo ""
echo "🔧 Test 1: Tool count"
TOOL_COUNT=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/list", "params": {}, "id": 2}' | $BINARY --service "$SERVICE_URL" --pagination-hints 2>/dev/null | jq -s '.[1].result.tools | length' 2>/dev/null || echo "0")
echo "Tool count: $TOOL_COUNT"

# Test 2: List all tools
echo ""
echo "🛠️  Test 2: Tool list"
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/list", "params": {}, "id": 2}' | $BINARY --service "$SERVICE_URL" --pagination-hints 2>/dev/null | jq -s '.[1].result.tools | map(.name) | sort' 2>/dev/null > /tmp/current_tools.json
echo "Tools saved to /tmp/current_tools.json"
head -10 /tmp/current_tools.json

# Test 3: Check for GetProductsByRating function
echo ""
echo "🔍 Test 3: Function check"
FUNCTION_EXISTS=$(cat /tmp/current_tools.json | jq -r '.[] | select(. | contains("GetProducts"))' 2>/dev/null || echo "")
if [ -n "$FUNCTION_EXISTS" ]; then
    echo "✅ Function found: $FUNCTION_EXISTS"
else
    echo "❌ GetProductsByRating function not found"
fi

# Test 4: Test function execution
echo ""
echo "🎯 Test 4: Function execution"
echo "Testing GetProductsByRating with rating=3..."
FUNCTION_TEST=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | $BINARY --service "$SERVICE_URL" --pagination-hints 2>&1)

if echo "$FUNCTION_TEST" | grep -q '"error"'; then
    echo "❌ Function execution failed:"
    echo "$FUNCTION_TEST" | tail -5
else
    RESULT_COUNT=$(echo "$FUNCTION_TEST" | jq -s '.[1].result.value | length' 2>/dev/null || echo "parse_error")
    if [ "$RESULT_COUNT" = "parse_error" ]; then
        echo "❌ JSON parse error in response"
    else
        echo "✅ Function executed successfully, returned $RESULT_COUNT items"
    fi
fi

# Test 5: Compare with Go implementation (if available)
echo ""
echo "📊 Test 5: Comparison with Go"
if [ -f "../odata_mcp_go/odata-mcp" ]; then
    echo "Comparing tool counts with Go implementation..."
    GO_TOOL_COUNT=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/list", "params": {}, "id": 2}' | ../odata_mcp_go/odata-mcp --service "$SERVICE_URL" --pagination-hints 2>/dev/null | jq -s '.[1].result.tools | length' 2>/dev/null || echo "0")
    echo ".NET tools: $TOOL_COUNT"
    echo "Go tools: $GO_TOOL_COUNT"
    
    if [ "$TOOL_COUNT" -eq "$GO_TOOL_COUNT" ]; then
        echo "✅ Tool counts match!"
    else
        echo "⚠️  Tool counts differ"
    fi
else
    echo "Go implementation not found at ../odata_mcp_go/odata-mcp"
fi

# Test 6: Claude Desktop integration check
echo ""
echo "🏠 Test 6: Claude Desktop logs"
LOG_FILE="$HOME/Library/Logs/Claude/mcp-server-north-net.log"
if [ -f "$LOG_FILE" ]; then
    echo "Recent Claude Desktop activity:"
    tail -5 "$LOG_FILE" | grep -E "(tool|error|disconnect)" || echo "No recent tool activity"
else
    echo "Claude Desktop log not found"
fi

echo ""
echo "=================================="
echo "🎉 Diagnostics complete!"
echo "Results summary:"
echo "- Tools: $TOOL_COUNT"
echo "- Function: $([ -n "$FUNCTION_EXISTS" ] && echo "✅ Found" || echo "❌ Missing")"
echo "- Execution: $(echo "$FUNCTION_TEST" | grep -q '"error"' && echo "❌ Failed" || echo "✅ Working")"