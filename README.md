# OData MCP Bridge for .NET

A .NET implementation of the OData to Model Context Protocol (MCP) bridge, allowing AI assistants like Claude to interact with OData services through a standardized interface.

This is a .NET 8 port of the [Python](https://github.com/yyyo/odata-mcp) and [Go](https://github.com/yyyo/odata-mcp-go) OData-MCP bridge implementations, providing robust OData support with dynamic tool generation.

## Features

- **Universal OData Support**: Works with both OData v2 and v4 services
- **Cross-Platform**: Runs on Windows, Linux, and macOS (Intel & Apple Silicon)  
- **Dynamic Tool Generation**: Automatically creates tools per entity set:
  - `filter_{entity}`: Query with OData options ($filter, $select, $expand, etc.)
  - `get_{entity}`: Retrieve single entity by key
  - `count_{entity}`: Get count with optional filter
  - `create_{entity}`: Create new entity (when not read-only)
  - `update_{entity}`: Update existing entity (when not read-only)
  - `delete_{entity}`: Delete entity (when not read-only)
  - Function imports (e.g., `GetProductsByRating`)
- **Proper Type Handling**: Correctly formats integers, strings, dates, and other OData types
- **Multiple Authentication Methods**: Basic auth, CSRF tokens, and anonymous access
- **SAP OData Extensions**: Automatic CSRF token handling for SAP services
- **Advanced Query Support**: Full OData query options support
- **Flexible Configuration**: Entity filtering, read-only modes, tool name customization
- **Smart Tool Suffixes**: Automatic namespace disambiguation for multiple MCP servers
- **Comprehensive Documentation**: See [IMPLEMENTATION_GUIDE.md](IMPLEMENTATION_GUIDE.md) for architecture details

## Quick Start

1. **Download a pre-built binary** from [releases](https://github.com/oisee/odata_mcp_net/releases) or build from source
2. **Add to Claude Desktop config** (see [Configuration](#claude-desktop-configuration) below)
3. **Restart Claude Desktop** to load the MCP server
4. **Start using OData tools** in your conversations!

## Prerequisites

- For running: No prerequisites (self-contained binaries include .NET runtime)
- For building: .NET 8.0 SDK or later

## Installation

### Option 1: Pre-built Binaries (Recommended)

Download the appropriate binary for your platform:

- **Windows**: `odata-mcp-win-x64.zip`
- **macOS Intel**: `odata-mcp-osx-x64.tar.gz`
- **macOS Apple Silicon**: `odata-mcp-osx-arm64.tar.gz`
- **Linux**: `odata-mcp-linux-x64.tar.gz`

Extract and place the executable in your preferred location:
- **Windows**: Extract the .zip and use `odata-mcp.exe`
- **macOS/Linux**: Extract the .tar.gz and make executable: `chmod +x odata-mcp`

> **Note**: Currently, use Debug builds for Claude Desktop integration. See [Known Issues](#known-issues).

### Option 2: Build from Source

```bash
git clone https://github.com/oisee/odata_mcp_net.git
cd odata_mcp_net

# Install .NET 8 SDK if not already installed
# macOS: brew install dotnet-sdk
# Linux: Follow https://docs.microsoft.com/dotnet/core/install/linux
# Windows: Download from https://dotnet.microsoft.com/download

# Build Debug version (recommended for now)
dotnet build --configuration Debug

# Or build for all platforms
make publish-all

# Windows users without make can use:
# dotnet publish src/ODataMcp -c Debug -r win-x64 --self-contained
```

## Usage

### Basic Usage

```bash
# Using positional argument
./odata-mcp https://services.odata.org/V2/OData/OData.svc/

# Using --service flag
./odata-mcp --service https://services.odata.org/V2/OData/OData.svc/

# Using environment variable
export ODATA_SERVICE_URL=https://services.odata.org/V2/OData/OData.svc/
./odata-mcp
```

### Claude Desktop Configuration

Add to your Claude Desktop configuration file:

**Configuration file locations:**
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

#### Basic Configuration (Debug Build)

```json
{
    "mcpServers": {
        "odata-demo": {
            "command": "/path/to/odata_mcp_net/src/ODataMcp/bin/Debug/net8.0/odata-mcp",
            "args": [
                "--service",
                "https://services.odata.org/V2/OData/OData.svc/",
                "--pagination-hints"
            ]
        }
    }
}
```

#### Advanced Configurations

**Authenticated Service:**
```json
{
    "mcpServers": {
        "my-company-odata": {
            "command": "/path/to/odata-mcp",
            "args": [
                "--service",
                "https://api.company.com/odata/",
                "--tool-shrink",
                "--entities",
                "Products,Orders,Customers"
            ],
            "env": {
                "ODATA_USERNAME": "myusername",
                "ODATA_PASSWORD": "mypassword"
            }
        }
    }
}
```

**SAP Service with CSRF Token:**
```json
{
    "mcpServers": {
        "sap-odata": {
            "command": "/path/to/odata-mcp",
            "args": [
                "--service",
                "https://sap-system.com/sap/opu/odata/sap/MY_SERVICE/",
                "--claude-code-friendly",
                "--tool-shrink"
            ],
            "env": {
                "ODATA_USERNAME": "sapuser",
                "ODATA_PASSWORD": "sappass"
            }
        }
    }
}
```

**Read-Only Production Service:**
```json
{
    "mcpServers": {
        "production-readonly": {
            "command": "/path/to/odata-mcp",
            "args": [
                "--service",
                "https://production.company.com/odata/",
                "--read-only",
                "--tool-shrink",
                "--max-items", "50"
            ]
        }
    }
}
```

### Authentication Methods

#### Basic Authentication
```bash
# Command line
./odata-mcp --user admin --password secret https://my-service.com/odata/

# Environment variables (preferred for security)
export ODATA_USERNAME=admin
export ODATA_PASSWORD=secret
./odata-mcp https://my-service.com/odata/
```

#### CSRF Token Support (SAP Services)
The bridge automatically handles CSRF tokens for SAP OData services:
- Fetches token with HEAD request before modifying operations
- Caches tokens for 30 minutes
- No additional configuration needed

## Command Line Options

```bash
Core Options:
  --service, -s URL         OData service URL (required)
  --user, -u USERNAME       Username for basic authentication
  --password, -p PASSWORD   Password for basic authentication
  
Tool Generation:
  --entities FILTER         Entity filter (comma-separated, wildcards supported)
                           Examples: "Products,Orders" or "Product*,Order*"
  --tool-shrink            Shorten tool names (e.g., filter_products vs filter_Products)
  --claude-code-friendly   Remove $ prefixes from OData parameters
  --pagination-hints       Add pagination guidance to tool descriptions
  
Access Control:
  --read-only              Disable create, update, and delete operations
  --max-items N            Maximum items per response (default: 100)
  
Debugging:
  --verbose, -v            Enable verbose logging
  --trace                  Show configuration and exit
  --help                   Display help and usage information
```

## Generated Tools

For each entity set, the bridge generates the following tools:

| Tool | Description | Example |
|------|-------------|---------|
| `filter_{entity}` | Query with OData filters | `filter_Products` with `$filter=Price gt 10` |
| `get_{entity}` | Get single entity by key | `get_Products` with `ID: 1` |
| `count_{entity}` | Count entities with optional filter | `count_Products` with filter |
| `create_{entity}` | Create new entity | `create_Products` with entity data |
| `update_{entity}` | Update existing entity | `update_Products` with ID and changes |
| `delete_{entity}` | Delete entity | `delete_Products` with ID |

Additionally:
- **Function Imports**: OData functions like `GetProductsByRating`
- **Service Info**: `odata_service_info` tool for metadata inspection

## Example Use Cases

### 1. Public Test Service (OData V2)
```json
{
    "mcpServers": {
        "odata-v2-demo": {
            "command": "/path/to/odata-mcp",
            "args": [
                "--service", "https://services.odata.org/V2/OData/OData.svc/",
                "--tool-shrink",
                "--pagination-hints"
            ]
        }
    }
}
```

### 2. OData V4 Service
```json
{
    "mcpServers": {
        "odata-v4-demo": {
            "command": "/path/to/odata-mcp",
            "args": [
                "--service", "https://services.odata.org/V4/OData/OData.svc/",
                "--tool-shrink",
                "--claude-code-friendly"
            ]
        }
    }
}
```

### 3. Development Environment
```bash
# For local development with verbose output
dotnet run --project src/ODataMcp -- \
    --service https://localhost:5001/odata/ \
    --verbose \
    --tool-shrink \
    --claude-code-friendly
```

## Building from Source

### Development
```bash
# Clone repository
git clone https://github.com/oisee/odata_mcp_net.git
cd odata_mcp_net

# Build and test
dotnet build
dotnet test

# Run directly
dotnet run --project src/ODataMcp -- --help
```

### Creating Release Binaries
```bash
# Single platform (use Debug for now)
dotnet publish src/ODataMcp -c Debug -r win-x64 --self-contained
dotnet publish src/ODataMcp -c Debug -r linux-x64 --self-contained
dotnet publish src/ODataMcp -c Debug -r osx-x64 --self-contained
dotnet publish src/ODataMcp -c Debug -r osx-arm64 --self-contained

# Using Make
make publish-all
make dist
```

## Troubleshooting

### Common Issues

**"No tools generated"**
- Verify the service URL is accessible and returns valid metadata
- Check authentication credentials if required
- Use `--verbose` to see detailed parsing information

**"Function call returns 400 Bad Request"**
- Ensure you're using the Debug build (see [Known Issues](#known-issues))
- Check that the OData service supports the operation
- Verify parameter types match the metadata

**"Parse error on metadata"**
- Some services require authentication even for metadata
- Try adding `--verbose` to see the actual error
- Ensure the URL ends with a trailing slash if it's a service root

**"CSRF token required"**
- The bridge automatically handles CSRF tokens for SAP services
- Ensure your credentials have permission to fetch tokens
- Check if the service requires special headers

### Testing Your Setup

1. **Test with public service first:**
   ```bash
   ./odata-mcp --service https://services.odata.org/V2/OData/OData.svc/ --trace
   ```

2. **Verify tools are generated:**
   ```bash
   echo '{"jsonrpc": "2.0", "id": 1, "method": "tools/list", "params": {}}' | \
   ./odata-mcp --service https://services.odata.org/V2/OData/OData.svc/ | \
   jq '.result.tools[].name'
   ```

3. **Check Claude Desktop integration:**
   - Restart Claude Desktop after config changes
   - Look for your MCP server in Claude's model context
   - Try a simple query like "Show me product with ID 1"

## Known Issues

### Debug vs Release Build
Currently, there's a known issue where Release builds may fail with certain OData operations (specifically function imports). **Please use Debug builds for Claude Desktop integration** until this is resolved.

```bash
# Build Debug version
dotnet build --configuration Debug

# Path for Claude Desktop config
/path/to/odata_mcp_net/src/ODataMcp/bin/Debug/net8.0/odata-mcp
```

### OData V2 Limitations
- Search functionality is not available for OData V2 services
- Some V2 services may have limited function import support

## Documentation

- **[IMPLEMENTATION_GUIDE.md](IMPLEMENTATION_GUIDE.md)** - Technical architecture and design details
- **[REIMPLEMENTATION_GUIDE.md](REIMPLEMENTATION_GUIDE.md)** - Journey of porting from Go/Python to .NET
- **[TESTING.md](TESTING.md)** - Comprehensive testing guide
- **[LIBRARY_INTEGRATION.md](LIBRARY_INTEGRATION.md)** - Details on OData library usage

## Recent Improvements

- ✅ Fixed integer ID formatting (no more quoted integers)
- ✅ Achieved tool parity with Go implementation
- ✅ Improved OData V2 metadata parsing
- ✅ Added function import support
- ✅ Smart tool name suffixes for multi-server environments
- ✅ Removed non-functional search tools pending proper metadata validation

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

### Development Setup
1. Fork the repository
2. Create a feature branch
3. Make your changes with tests
4. Submit a pull request

## Acknowledgments

- Original [Python implementation](https://github.com/yyyo/odata-mcp) by yyyo
- [Go implementation](https://github.com/yyyo/odata-mcp-go) reference
- Microsoft for the OData libraries and .NET platform
- Anthropic for the Model Context Protocol specification

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.