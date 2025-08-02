#!/bin/bash

echo "🔍 Capturing HTTP request details"
echo "================================="

# Use a proxy to capture the exact request
# First, let's use curl to see what headers work
echo "Working request with curl:"
curl -s -i -H "Accept: application/atom+xml" -H "Accept: application/json" "https://services.odata.org/V2/OData/OData.svc/GetProductsByRating?rating=3" | head -10

echo ""
echo "Let's add more detailed HTTP logging to our code..."

# Add HTTP request logging
cat > src/ODataMcp.Core/Debug/HttpLogger.cs << 'EOF'
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ODataMcp.Core.Debug
{
    public class LoggingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Log request
            System.Console.Error.WriteLine($"[HTTP] {request.Method} {request.RequestUri}");
            foreach (var header in request.Headers)
            {
                System.Console.Error.WriteLine($"[HTTP] Header: {header.Key}: {string.Join(", ", header.Value)}");
            }
            
            // Send request
            var response = await base.SendAsync(request, cancellationToken);
            
            // Log response
            System.Console.Error.WriteLine($"[HTTP] Response: {(int)response.StatusCode} {response.ReasonPhrase}");
            
            return response;
        }
    }
}
EOF

echo "File created. This will help us see the exact HTTP requests being made."