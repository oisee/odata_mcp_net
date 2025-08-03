# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OData MCP Bridge for .NET - A cross-platform implementation that creates a bridge between OData services (v2 and v4) and the Model Context Protocol (MCP), allowing AI assistants like Claude to interact with OData services.

## Build Commands

### Automated Build System (Makefile) - PREFERRED METHOD

The project includes a comprehensive Makefile for automated builds across all platforms:

```bash
# Essential commands
make build           # Build for current platform (Release mode)
make test            # Run all tests with verbose output
make clean           # Clean all build artifacts

# Cross-platform publishing
make publish-all     # Build for ALL platforms (Windows, Linux, macOS)
make publish-windows # Build Windows x64 binary
make publish-linux   # Build Linux x64 binary  
make publish-macos   # Build macOS x64 and ARM64 binaries

# Release management
make dist            # Create distribution packages (.zip/.tar.gz) for all platforms
make release         # Clean build and create full release with all packages
make install-local   # Install binary to project root for development

# Development helpers
make dev             # Build and show help
make run-northwind   # Test with Northwind OData service
make restore         # Restore NuGet packages
make help            # Show all available targets
```

**Note**: The Makefile automatically:
- Sets VERSION=1.0.0 (update in Makefile for new releases)
- Creates self-contained single-file executables
- Packages binaries with config files (appsettings.json, hints.json)
- Handles the ARM64 macOS bug by building Debug for ARM64

### Manual .NET CLI Commands

```bash
# Build for current platform
dotnet build --configuration Debug
dotnet build --configuration Release

# Run tests
dotnet test
dotnet test --logger "console;verbosity=normal"

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Build and run the application
dotnet run --project src/ODataMcp -- --help
dotnet run --project src/ODataMcp -- --service https://services.odata.org/V2/OData/OData.svc/

# Manual publish for specific platforms
dotnet publish src/ODataMcp -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
dotnet publish src/ODataMcp -c Release -r win-x86 --self-contained -p:PublishSingleFile=true
dotnet publish src/ODataMcp -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
dotnet publish src/ODataMcp -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
```

## Important Note on ARM64 macOS

There is a known .NET runtime bug on ARM64 macOS with Release builds. Use x64 builds on Apple Silicon (they run via Rosetta 2) or use Debug builds for ARM64.

## Release Workflow

To create a new release with binaries for all platforms:

```bash
# 1. Update version in Makefile (line 9)
VERSION = 0.5.2  # or whatever version

# 2. Build all platforms and create distribution packages
make release

# 3. Create git tag
git tag -a v0.5.2 -m "Release v0.5.2 - Your release message"
git push origin v0.5.2

# 4. Create GitHub release with gh CLI
gh release create v0.5.2 \
  --title "Release v0.5.2 - Title" \
  --notes "Release notes here" \
  bin/dist/*.{zip,tar.gz}

# 5. Update README.md with new version links
# Update the download links in the Installation section
```

The `make release` command will:
1. Clean all previous builds
2. Build for all platforms (Windows x64, Linux x64, macOS x64, macOS ARM64 Debug)
3. Create compressed archives in `bin/dist/`
4. Install local binary for testing

## Architecture Overview

### Core Components

1. **Entry Point** (`src/ODataMcp/Program.cs`):
   - Handles CLI argument parsing using CommandLine library
   - Sets up dependency injection with Microsoft.Extensions.DependencyInjection
   - Manages configuration from CLI args, environment variables, and .env files
   - Creates and runs the MCP server with STDIO transport

2. **MCP Server** (`src/ODataMcp.Core/Mcp/SimpleMcpServerV2.cs`):
   - Implements the MCP protocol with JSON-RPC handling
   - Dynamically generates tools from OData metadata
   - Routes tool calls to appropriate OData operations
   - Handles both $ and non-$ parameter prefixes (--claude-code-friendly flag)

3. **OData Service** (`src/ODataMcp.Core/Services/SimpleODataService.cs`):
   - Manages HTTP communication with OData services
   - Handles authentication (Basic, Cookie, CSRF tokens)
   - Executes CRUD operations and function imports
   - Supports both OData v2 and v4 protocols

4. **Metadata Parser** (`src/ODataMcp.Core/Services/SimpleMetadataParser.cs`):
   - Parses OData metadata XML ($metadata endpoint)
   - Extracts entity sets, properties, and function imports
   - Handles version differences between OData v2 and v4

5. **Transport Layer** (`src/ODataMcp.Core/Transport/SimpleStdioTransport.cs`):
   - Manages STDIO communication for MCP protocol
   - Handles JSON-RPC message framing
   - Provides async read/write operations

### Key Design Patterns

- **Dependency Injection**: All major components use constructor injection
- **Async/Await**: All I/O operations are async throughout
- **Configuration Pattern**: Centralized configuration through `ODataBridgeConfiguration`
- **Tool Generation**: Dynamic tool creation based on OData metadata
- **CSRF Token Handling**: Automatic token management for SAP services

### Tool Generation Logic

The bridge dynamically creates MCP tools for each OData entity set:
- `filter_{entity}`: Query with OData options ($filter, $select, $expand, etc.)
- `get_{entity}`: Retrieve single entity by key
- `count_{entity}`: Get count with optional filter
- `create_{entity}`: Create new entity (when not read-only)
- `update_{entity}`: Update existing entity (when not read-only)
- `delete_{entity}`: Delete entity (when not read-only)
- Function imports (e.g., `GetProductsByRating`)
- `odata_service_info`: Service metadata inspection

### Request Flow

1. JSON-RPC request arrives via STDIO
2. `SimpleMcpServerV2` parses and routes the request
3. For tool calls, parameters are validated and transformed
4. `SimpleODataService` builds and executes the HTTP request
5. Response is transformed to MCP format and returned

## Testing

Tests use xUnit with FluentAssertions and Moq. Key test areas:
- Unit tests for metadata parsing
- Integration tests for MCP server operations
- Transport layer tests
- CSRF token handling tests

Run tests with `dotnet test` or specific tests with filtering.

## Configuration Options

Key command-line flags:
- `--service`: OData service URL (required)
- `--user`/`--password`: Basic authentication
- `--tool-shrink`: Shorten tool names
- `--claude-code-friendly`: Remove $ prefixes from parameters
- `--read-only`: Disable write operations
- `--entities`: Filter which entities to expose
- `--verbose`: Enable debug logging
- `--pagination-hints`: Add pagination guidance to tools

Environment variables:
- `ODATA_SERVICE_URL`: Service URL
- `ODATA_USERNAME`/`ODATA_PASSWORD`: Authentication credentials