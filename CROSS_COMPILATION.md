# Cross-Compilation Guide for OData MCP .NET

## Overview

.NET supports full cross-compilation, meaning you can build binaries for ANY target platform from ANY host platform. This means:

- ✅ **From macOS**: Can build for Windows, Linux, and macOS
- ✅ **From Linux/WSL**: Can build for Windows, Linux, and macOS  
- ✅ **From Windows**: Can build for Windows, Linux, and macOS

## Quick Cross-Compilation Commands

### From Any Platform, Build for All Platforms

```bash
# Using make (works on macOS, Linux, WSL)
make publish-all

# Or individually:
make publish-windows    # Creates Windows .exe
make publish-linux      # Creates Linux binary
make publish-macos      # Creates both Intel and ARM64 macOS binaries
```

### Direct dotnet Commands (No make required)

```bash
# From macOS/Linux/WSL to Windows
dotnet publish src/ODataMcp -c Release -r win-x64 --self-contained

# From Windows/Linux/WSL to macOS Intel
dotnet publish src/ODataMcp -c Release -r osx-x64 --self-contained

# From Windows/macOS/WSL to Linux
dotnet publish src/ODataMcp -c Release -r linux-x64 --self-contained

# From any platform to macOS Apple Silicon
dotnet publish src/ODataMcp -c Release -r osx-arm64 --self-contained
```

## Runtime Identifiers (RIDs)

Common RIDs for dotnet publish:

- **Windows**: `win-x64`, `win-x86`, `win-arm64`
- **Linux**: `linux-x64`, `linux-arm`, `linux-arm64`
- **macOS**: `osx-x64` (Intel), `osx-arm64` (Apple Silicon)
- **Alpine Linux**: `linux-musl-x64`, `linux-musl-arm64`

## Self-Contained vs Framework-Dependent

### Self-Contained (Recommended for Distribution)
Includes .NET runtime - no installation required on target machine:

```bash
dotnet publish -r win-x64 --self-contained
# Creates ~80MB executable with embedded runtime
```

### Framework-Dependent
Smaller file size but requires .NET runtime on target machine:

```bash
dotnet publish -r win-x64 --self-contained false
# Creates ~1MB executable, needs .NET 8 runtime installed
```

## Building All Release Binaries

### Using Make (Preferred)

```bash
# Build self-contained releases for all platforms
make dist

# This creates:
# - dist/odata-mcp-win-x64.zip
# - dist/odata-mcp-linux-x64.tar.gz
# - dist/odata-mcp-osx-x64.tar.gz
# - dist/odata-mcp-osx-arm64.tar.gz
```

### Manual Build Script

```bash
#!/bin/bash
# build-all-platforms.sh

VERSION="1.0.0"
PLATFORMS=("win-x64" "linux-x64" "osx-x64" "osx-arm64")

for platform in "${PLATFORMS[@]}"; do
    echo "Building for $platform..."
    dotnet publish src/ODataMcp -c Release -r $platform --self-contained \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -o "bin/dist/$platform"
done
```

## Platform-Specific Considerations

### Windows Builds
- Executable name: `odata-mcp.exe`
- No additional permissions needed
- Works with Windows Defender

### Linux/macOS Builds
- Executable name: `odata-mcp` (no extension)
- Requires execute permission: `chmod +x odata-mcp`
- May need to bypass Gatekeeper on macOS: `xattr -d com.apple.quarantine odata-mcp`

### WSL-Specific Notes
- Can build for all platforms including Windows
- Windows binaries built in WSL work on native Windows
- Use `/mnt/c/...` paths to access Windows filesystem

## Single-File Publishing

Create a single executable file (recommended for distribution):

```bash
# Single file with runtime included
dotnet publish -r win-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

# Trimmed single file (smaller size)
dotnet publish -r linux-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true
```

## CI/CD Cross-Compilation

### GitHub Actions Example

```yaml
name: Build All Platforms

on: [push, pull_request]

jobs:
  build:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        runtime: [win-x64, linux-x64, osx-x64, osx-arm64]
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Build for ${{ matrix.runtime }}
      run: |
        dotnet publish src/ODataMcp -c Release -r ${{ matrix.runtime }} \
          --self-contained -p:PublishSingleFile=true \
          -o dist/${{ matrix.runtime }}
    
    - name: Upload artifacts
      uses: actions/upload-artifact@v3
      with:
        name: odata-mcp-${{ matrix.runtime }}
        path: dist/${{ matrix.runtime }}/*
```

## Testing Cross-Compiled Binaries

### On the Build Machine

```bash
# Test Linux binary on macOS/Windows using Docker
docker run --rm -v $(pwd)/bin/publish/linux-x64:/app ubuntu:22.04 \
    /app/odata-mcp --help

# Test Windows binary on Linux/macOS using Wine
wine bin/publish/win-x64/odata-mcp.exe --help
```

### Verification Commands

After building, verify the binaries:

```bash
# Check file type
file bin/publish/*/odata-mcp*

# Output examples:
# bin/publish/win-x64/odata-mcp.exe: PE32+ executable (console) x86-64
# bin/publish/linux-x64/odata-mcp: ELF 64-bit LSB executable, x86-64
# bin/publish/osx-x64/odata-mcp: Mach-O 64-bit executable x86_64
# bin/publish/osx-arm64/odata-mcp: Mach-O 64-bit executable arm64
```

## Troubleshooting

### "Cannot find runtime" Error
Solution: Install the target runtime pack:
```bash
dotnet workload install wasm-tools  # For WebAssembly
dotnet workload install ios          # For iOS
dotnet workload install android      # For Android
```

### Large Binary Size
Use PublishTrimmed to reduce size:
```bash
dotnet publish -r linux-x64 --self-contained \
    -p:PublishTrimmed=true \
    -p:TrimMode=link
```

### Missing Dependencies on Target
Use self-contained deployment or ensure .NET 8 runtime is installed on target.

## Summary

.NET's cross-compilation is powerful and straightforward:
1. **Any platform can build for any platform** - no VMs or cross-compilers needed
2. **Self-contained builds** include everything needed to run
3. **Single-file publishing** creates clean, distributable executables
4. **Make targets** simplify building for all platforms at once

The OData MCP bridge fully supports cross-compilation, making it easy to create releases for all platforms from your development machine, whether it's Windows, macOS, or Linux/WSL.