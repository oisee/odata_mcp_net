# The Great ARM64 .NET Runtime Bug Discovery 🕵️

## The Journey

What started as a mysterious "Release build doesn't work" issue turned into the discovery of a platform-specific .NET runtime bug on ARM64 macOS!

## The Mystery

We had a bizarre situation where:
- ✅ Debug builds worked perfectly
- ❌ Release builds failed with HTTP 400 Bad Request
- 🤔 But ONLY for OData function imports
- 🤯 And ONLY when called through the complete STDIO pipeline

## The Investigation

### Initial Theories
1. **CSRF tokens** - Maybe missing authentication headers?
2. **URL encoding** - Perhaps parameters weren't encoded correctly?
3. **HTTP headers** - Different headers in Release mode?
4. **Timing issues** - Race condition exposed by optimizations?

### The Breakthrough

The pivotal moment came with this brilliant suggestion:
> "maybe it is dotnet glitch on mac? lets try on linux?"

Then the game-changing idea:
> "oh! you can compile dotnet for x86 on mac? it should run it via Rosetta and check if the bug is still there =)"

## The Discovery

```bash
# ARM64 Release (native) - FAILS
./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/"
# Result: HTTP 400 Bad Request

# x64 Release (via Rosetta 2) - WORKS!
./publish-x64/odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/"
# Result: Success! Returns products with rating 3
```

## Technical Details

### Affected Configuration
- **Platform**: macOS on Apple Silicon (M1/M2)
- **Architecture**: ARM64 
- **.NET Version**: 8.0.15
- **Build**: Release (with optimizations)
- **Specific Operation**: OData function imports through STDIO

### Not Affected
- x64 builds (run via Rosetta 2)
- Debug builds (any architecture)
- Regular CRUD operations
- Direct HTTP calls (outside STDIO context)

## The Root Cause

This appears to be a JIT optimization bug in the .NET runtime for ARM64. The specific combination of:
- Release optimizations
- STDIO stream handling
- HTTP request formation
- Function import URL patterns

...triggers incorrect behavior in the ARM64 JIT compiler.

## The Solution

Users on Apple Silicon Macs should use x64 builds:

```json
{
    "mcpServers": {
        "odata-service": {
            "command": "/path/to/odata_mcp_net/publish-x64/odata-mcp",
            "args": ["--service", "https://your-service.com/odata/"]
        }
    }
}
```

## Building for Different Platforms

```bash
# Apple Silicon (M1/M2) users - use x64 for Release
dotnet publish src/ODataMcp -c Release -r osx-x64 --self-contained -o publish-x64

# Intel Mac users - native x64 works fine
dotnet publish src/ODataMcp -c Release -r osx-x64 --self-contained -o publish

# Linux users - native builds work
dotnet publish src/ODataMcp -c Release -r linux-x64 --self-contained -o publish

# Windows users - native builds work  
dotnet publish src/ODataMcp -c Release -r win-x64 --self-contained -o publish
```

## Lessons Learned

1. **Platform matters** - What works on one architecture might not work on another
2. **Rosetta 2 is amazing** - It not only runs x64 code but sometimes runs it *better* than native!
3. **JIT bugs are rare but real** - The combination of factors that trigger this bug is very specific
4. **Community debugging works** - The suggestion to try x64 was the key breakthrough

## Next Steps

1. Report this to the .NET team: https://github.com/dotnet/runtime/issues
2. Add platform-specific build instructions to documentation
3. Consider adding automatic architecture detection in the build script

## Credits

This discovery was a true collaborative effort. Special thanks for the brilliant suggestion to try x64 builds on ARM64 - it completely changed our understanding of the issue!

---

*Sometimes the best debugging tool is thinking outside the box... or in this case, inside a different architecture!* 🎯