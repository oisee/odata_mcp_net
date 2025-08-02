#!/bin/bash

echo "🔍 Investigating Debug vs Release build differences"
echo "================================================="

# First, let's check if the issue is with our debug statements
echo ""
echo "Test 1: Remove all Console.Error.WriteLine debug statements"
echo "------------------------------------------------------------"

# Create a backup
cp src/ODataMcp.Core/Mcp/SimpleMcpServerV2.cs src/ODataMcp.Core/Mcp/SimpleMcpServerV2.cs.backup
cp src/ODataMcp.Core/Services/SimpleODataService.cs src/ODataMcp.Core/Services/SimpleODataService.cs.backup

# Remove debug statements
sed -i '' '/Console\.Error\.WriteLine.*ODATA_DEBUG/d' src/ODataMcp.Core/Mcp/SimpleMcpServerV2.cs
sed -i '' '/Console\.Error\.WriteLine.*ODATA_DEBUG/d' src/ODataMcp.Core/Services/SimpleODataService.cs

echo "Building Release without debug statements..."
dotnet build --configuration Release -verbosity:quiet

echo "Testing Release build..."
RESULT=$(echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | ./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints 2>&1)

if echo "$RESULT" | grep -q "400"; then
    echo "❌ Still fails with 400 - not related to debug statements"
else
    echo "✅ Works now! Issue was with debug statements in Release mode"
fi

# Restore backups
mv src/ODataMcp.Core/Mcp/SimpleMcpServerV2.cs.backup src/ODataMcp.Core/Mcp/SimpleMcpServerV2.cs
mv src/ODataMcp.Core/Services/SimpleODataService.cs.backup src/ODataMcp.Core/Services/SimpleODataService.cs

echo ""
echo "Test 2: Check for async/await optimization issues"
echo "-------------------------------------------------"

# Let's create a minimal test to isolate the issue
cat > TestMinimal.cs << 'EOF'
using System;
using System.Net.Http;
using System.Threading.Tasks;

class TestMinimal
{
    static async Task Main()
    {
        var client = new HttpClient();
        var url = "https://services.odata.org/V2/OData/OData.svc/GetProductsByRating?rating=3";
        
        Console.WriteLine($"Testing URL: {url}");
        
        var response = await client.GetAsync(url);
        Console.WriteLine($"Status: {response.StatusCode}");
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Success! Content length: {content.Length}");
        }
    }
}
EOF

echo "Testing direct HTTP call..."
dotnet run TestMinimal.cs

rm TestMinimal.cs