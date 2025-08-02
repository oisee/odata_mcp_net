# Debug vs Release Build Investigation

## Issue Summary

Release builds fail with HTTP 400 Bad Request errors when executing OData function imports (e.g., `GetProductsByRating`), while Debug builds work perfectly.

## Symptoms

### Debug Build ✅
```bash
dotnet build --configuration Debug
echo '{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | \
./src/ODataMcp/bin/Debug/net8.0/odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/"
# Result: SUCCESS - Returns products with rating 3
```

### Release Build ❌
```bash
dotnet build --configuration Release
echo '{"jsonrpc": "2.0", "method": "tools/call", "params": {"name": "GetProductsByRating_for_odata", "arguments": {"rating": 3}}, "id": 2}' | \
./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/"
# Result: ERROR - HTTP 400 Bad Request
```

## Investigation Results

### What We've Tested

1. **Direct HTTP Requests**: Both Debug and Release builds can make successful HTTP requests when tested in isolation
2. **MCP Server Logic**: The MCP server initialization and tool generation works in both builds
3. **OData Service Calls**: Direct OData service calls work in both builds when tested outside the STDIO transport
4. **Debug Infrastructure**: The ODataMcpDebugger is available in both builds (not conditionally compiled)

### Key Observations

1. **The issue only manifests in the full application context** - not in isolated tests
2. **Regular entity operations work** - only function imports fail
3. **The exact same code path works in Debug but fails in Release**
4. **No obvious conditional compilation or #if DEBUG directives**

### Working Theory

The issue appears to be specific to:
- The STDIO transport layer
- JSON-RPC message processing
- Possibly related to assembly loading or JIT compilation differences

## Potential Root Causes

### 1. JIT Optimization Differences
Release builds enable optimizations that might:
- Inline methods differently
- Optimize away certain operations
- Change timing of initialization

### 2. Assembly Loading/Reflection
- Release builds might load assemblies differently
- Type resolution could be affected by optimization
- Metadata reading might be impacted

### 3. Async/Await Behavior
- Release optimizations can change async state machine generation
- Timing differences in async operations
- Possible race conditions exposed by optimization

### 4. String Handling/Encoding
- URL encoding might behave differently
- String concatenation optimizations
- Character encoding in HTTP requests

## Next Investigation Steps

1. **Add Conditional Logging**
   ```csharp
   #if DEBUG
   Console.WriteLine("DEBUG BUILD");
   #else
   Console.WriteLine("RELEASE BUILD");
   #endif
   ```

2. **HTTP Request Inspection**
   - Use Fiddler or similar to capture actual HTTP requests
   - Compare headers, URL encoding, request body

3. **Assembly Analysis**
   - Use ILSpy to compare Debug vs Release IL code
   - Check for optimization differences in critical paths

4. **Timing Analysis**
   - Add performance counters
   - Check if it's a timing/race condition issue

5. **Minimal Reproduction**
   - Create smallest possible repro case
   - Isolate to specific component

## Workaround

For now, use Debug builds for production:
```json
{
    "mcpServers": {
        "odata-service": {
            "command": "/path/to/src/ODataMcp/bin/Debug/net8.0/odata-mcp",
            "args": ["--service", "https://your-service.com/odata/"]
        }
    }
}
```

## References

- [.NET Compilation Differences](https://docs.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [JIT Optimizations](https://docs.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)
- [Async Debugging](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/)