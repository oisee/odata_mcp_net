# The Release Build Mystery - Complete Investigation Context

## Executive Summary

**The Bug**: Release builds fail with HTTP 400 Bad Request **ONLY** when calling OData function imports (like `GetProductsByRating`) through the complete MCP/STDIO pipeline on macOS ARM64.

## Test Results

### ✅ What WORKS in Release:
1. All regular entity operations (GET, CREATE, UPDATE, DELETE)
2. Direct HTTP calls when tested in isolation
3. MCP server initialization and tool generation
4. The EXACT same function call when tested outside STDIO context
5. Everything in Debug builds

### ❌ What FAILS in Release:
- Function imports when called through: `Claude Desktop → STDIO → JSON-RPC → MCP Server → OData Function`
- Error: HTTP 400 Bad Request
- Only affects: `GetProductsByRating_for_odata` (and presumably other function imports)

## Platform Information
```
Platform: macOS 15.5 (Darwin 24.5.0)
Architecture: ARM64 (Apple Silicon M2)
.NET SDK: 8.0.408
Runtime: 8.0.15
```

## The Puzzle

The same code path works in Debug but fails in Release, BUT ONLY when:
1. ✓ It's a Release build
2. ✓ It's a function import (not regular CRUD)
3. ✓ It's going through STDIO transport
4. ✓ All three conditions must be true

## Code Under Suspicion

### 1. Function URL Building (SimpleMcpServerV2.cs:850-857)
```csharp
var functionUrl = functionImport.Name;
if (parameters.Any())
{
    // Build query string with proper URL encoding
    var paramString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
    functionUrl += $"?{paramString}";
}
```

### 2. HTTP Request Formation (SimpleODataService.cs:180-205)
```csharp
public async Task<object> ExecuteFunctionAsync(string functionUrl, CancellationToken cancellationToken = default)
{
    var url = $"{_serviceUrl.TrimEnd('/')}/{functionUrl}";
    
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    
    // V2 services typically return XML by default, but we can accept both
    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/atom+xml"));
    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    
    // Add OData V2 specific headers
    request.Headers.Add("DataServiceVersion", "2.0");
    request.Headers.Add("MaxDataServiceVersion", "2.0");
    
    // ... rest of method
}
```

## Theories to Test on Linux

### 1. Platform-Specific .NET Runtime Issue
- ARM64 macOS JIT optimizations might differ from x64 Linux
- HTTP stack implementation differences between platforms
- Async state machine generation differences

### 2. HttpClient Behavior
- Connection pooling differences in Release mode
- Header persistence across requests
- Socket reuse patterns

### 3. String/URL Encoding
- `JsonElement.ToString()` behavior (tested - same in both)
- `Uri.EscapeDataString` behavior in Release
- Character encoding in HTTP requests

### 4. STDIO/JSON-RPC Layer
- Buffering differences between Debug/Release
- Stream flushing timing
- JSON serialization optimizations

## Test Commands

### Working Debug Build:
```bash
dotnet build --configuration Debug
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | \
./src/ODataMcp/bin/Debug/net8.0/odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints
```

### Failing Release Build:
```bash
dotnet build --configuration Release
echo '{"jsonrpc": "2.0", "method": "initialize", "params": {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "test-client", "version": "1.0.0"}}, "id": 1}
{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | \
./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/" --pagination-hints
```

### Direct Function Test (Works in Both):
```bash
curl "https://services.odata.org/V2/OData/OData.svc/GetProductsByRating?rating=3" \
  -H "Accept: application/json" \
  -H "DataServiceVersion: 2.0" \
  -H "MaxDataServiceVersion: 2.0"
```

## What We've Tried

1. ✅ Added comprehensive debug logging
2. ✅ Fixed URL encoding (added `Uri.EscapeDataString`)
3. ✅ Tested `JsonElement.ToString()` behavior
4. ✅ Verified the OData service works correctly
5. ✅ Tested components in isolation (all work)
6. ❌ Still fails in Release through STDIO

## Recent Code Changes

### Last modification to ExecuteFunctionAsync:
- Changed parameter encoding from raw concatenation to proper URL encoding
- Line 855: `var paramString = string.Join("&", parameters.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));`

## Linux Testing Checklist

1. **Basic Reproduction**
   - [ ] Clone repository
   - [ ] Build Debug - test function call
   - [ ] Build Release - test function call
   - [ ] Document if bug reproduces on Linux

2. **HTTP Traffic Analysis**
   - [ ] Use tcpdump/Wireshark to capture actual HTTP requests
   - [ ] Compare Debug vs Release HTTP headers
   - [ ] Check for timing differences

3. **Detailed Logging**
   - [ ] Add file-based logging to capture all HTTP details
   - [ ] Log thread IDs to check for threading issues
   - [ ] Add timestamps to check timing

4. **Isolation Tests**
   - [ ] Test with minimal console app (no STDIO)
   - [ ] Test with different HttpClient configurations
   - [ ] Test with synchronous vs async calls

## Potential Fixes to Try

1. **Force Synchronous Execution**
   ```csharp
   // In ExecuteFunctionAsync, try:
   var response = _httpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
   ```

2. **Explicit Header Management**
   ```csharp
   // Clear and rebuild headers
   request.Headers.Clear();
   // Re-add all headers explicitly
   ```

3. **HttpClient Per Request**
   ```csharp
   // Create new HttpClient for each function call
   using var client = new HttpClient();
   ```

## Contact & Questions

If you discover anything interesting on Linux, the key questions are:
1. Does it reproduce on Linux at all?
2. If yes, is it x64 specific or also on ARM64 Linux?
3. What are the actual HTTP differences between Debug/Release?
4. Is it timing-related (race condition)?

Good luck with the investigation! This is genuinely one of the more puzzling bugs I've encountered. The fact that it ONLY happens with the complete pipeline makes it particularly interesting.

## File Locations

- Main issue: `src/ODataMcp.Core/Services/SimpleODataService.cs:180` (ExecuteFunctionAsync)
- URL building: `src/ODataMcp.Core/Mcp/SimpleMcpServerV2.cs:850`
- Test scripts: `scripts/debugging/`
- This investigation: `RELEASE_BUILD_MYSTERY.md`