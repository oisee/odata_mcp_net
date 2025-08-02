# ARM64 .NET Runtime Bug Discovered! 🐛

## The Bug
Release builds on ARM64 macOS fail with HTTP 400 when calling OData function imports through STDIO, while x64 builds work perfectly via Rosetta 2.

## Test Results

| Platform | Build | Result |
|----------|-------|--------|
| ARM64 (M2) | Debug | ✅ Works |
| ARM64 (M2) | Release | ❌ HTTP 400 |
| x64 (Rosetta) | Release | ✅ Works |

## Reproduction
```bash
# Fails on ARM64
dotnet build --configuration Release
./odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/"

# Works on x64 via Rosetta
dotnet publish -c Release -r osx-x64 --self-contained
./publish-x64/odata-mcp --service "https://services.odata.org/V2/OData/OData.svc/"
```

## Workaround
Use x64 builds on Apple Silicon Macs - they run fine through Rosetta 2!

## Details
- Only affects function imports, not regular CRUD operations
- Only in Release configuration with optimizations
- Only through STDIO transport (works in isolation)
- Likely a JIT optimization bug specific to ARM64

This should be reported to: https://github.com/dotnet/runtime/issues