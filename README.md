# OData MCP Bridge for .NET

A .NET implementation of the OData to Model Context Protocol (MCP) bridge, allowing AI assistants like Claude to interact with OData services through a standardized interface.

This is a .NET 8 port of the [Python](https://github.com/yyyo/odata-mcp) and [Go](https://github.com/yyyo/odata-mcp-go) OData-MCP bridge implementations, providing robust OData support with dynamic tool generation.

## Features

- **Universal OData Support**: Works with both OData v2 and v4 services
- **Cross-Platform**: Runs on Windows, Linux, and macOS (Intel & Apple Silicon)  
- **Dynamic Tool Generation**: Automatically creates 7+ tools per entity set:
  - `filter_{entity}`: Query with OData options
  - `get_{entity}`: Retrieve single entity by key
  - `create_{entity}`: Create new entity
  - `update_{entity}`: Update existing entity
  - `delete_{entity}`: Delete entity
  - `count_{entity}`: Get count with optional filter
  - `search_{entity}`: Full-text search across string fields
- **Multiple Authentication Methods**: Basic auth, CSRF tokens, and anonymous access
- **SAP OData Extensions**: Automatic CSRF token handling for SAP services
- **Advanced Query Support**: OData query options ($filter, $select, $expand, $orderby, etc.)
- **Flexible Configuration**: Entity filtering, read-only modes, tool name customization
- **Claude-Code-Friendly**: Optional mode for better Claude Code integration
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

### Option 2: Build from Source

```bash
git clone https://github.com/oisee/odata_mcp_net.git
cd odata_mcp_net

# Install .NET 8 SDK if not already installed
# macOS: brew install dotnet-sdk
# Linux: Follow https://docs.microsoft.com/dotnet/core/install/linux
# Windows: Download from https://dotnet.microsoft.com/download

# Build for current platform
make build

# Or build for all platforms
make publish-all

# Windows users without make can use:
# dotnet publish src/ODataMcp -c Release -r win-x64 --self-contained
```

## Usage

### Basic Usage

```bash
# Using positional argument
./odata-mcp https://services.odata.org/V2/Northwind/Northwind.svc/

# Using --service flag
./odata-mcp --service https://services.odata.org/V2/Northwind/Northwind.svc/

# Using environment variable
export ODATA_SERVICE_URL=https://services.odata.org/V2/Northwind/Northwind.svc/
./odata-mcp
```

### Claude Desktop Configuration

Add to your Claude Desktop configuration file:

**Configuration file locations:**
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

#### Basic Configuration

```json
{
    "mcpServers": {
        "northwind": {
            "command": "C:\\path\\to\\odata-mcp.exe",  // Windows
            // "command": "/path/to/odata-mcp",        // macOS/Linux
            "args": [
                "--service",
                "https://services.odata.org/V2/Northwind/Northwind.svc/",
                "--tool-shrink"
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
  
Access Control:
  --read-only              Disable create, update, and delete operations
  --max-items N            Maximum items per response (default: 100)
  
Debugging:
  --verbose, -v            Enable verbose logging
  --trace                  Show configuration and exit
  --help                   Display help and usage information
```

## Example Use Cases

### 1. Public Test Service (Northwind)
```json
{
    "mcpServers": {
        "northwind-demo": {
            "command": "/path/to/odata-mcp",
            "args": [
                "--service", "https://services.odata.org/V2/Northwind/Northwind.svc/",
                "--tool-shrink",
                "--claude-code-friendly"
            ]
        }
    }
}
```

### 2. Corporate API with Limited Entities
```json
{
    "mcpServers": {
        "company-api": {
            "command": "/path/to/odata-mcp",
            "args": [
                "--service", "https://api.company.com/odata/v4/",
                "--entities", "Customer*,Product*,Order*",
                "--tool-shrink",
                "--max-items", "25"
            ],
            "env": {
                "ODATA_USERNAME": "api_user",
                "ODATA_PASSWORD": "api_key_here"
            }
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
# Single platform
make publish-windows    # Creates bin/publish/win-x64/
make publish-linux      # Creates bin/publish/linux-x64/
make publish-macos      # Creates bin/publish/osx-x64/ and osx-arm64/

# All platforms
make publish-all

# Create distribution archives
make dist              # Creates .zip and .tar.gz files
```

## Troubleshooting

### Common Issues

**"No tools generated"**
- Verify the service URL is accessible and returns valid metadata
- Check authentication credentials if required
- Use `--verbose` to see detailed parsing information

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
   # macOS/Linux
   ./odata-mcp --service https://services.odata.org/V2/Northwind/Northwind.svc/ --trace
   
   # Windows
   odata-mcp.exe --service https://services.odata.org/V2/Northwind/Northwind.svc/ --trace
   ```

2. **Verify tools are generated:**
   ```bash
   # macOS/Linux
   echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}}' | \
   ./odata-mcp --service https://services.odata.org/V2/Northwind/Northwind.svc/
   
   # Windows PowerShell
   echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}}' | `
   .\odata-mcp.exe --service https://services.odata.org/V2/Northwind/Northwind.svc/
   ```

3. **Check Claude Desktop integration:**
   - Restart Claude Desktop after config changes
   - Look for your MCP server in Claude's model context
   - Try a simple query like "Show me all products"

## Documentation

- **[IMPLEMENTATION_GUIDE.md](IMPLEMENTATION_GUIDE.md)** - Technical architecture and design details
- **[REIMPLEMENTATION_GUIDE.md](REIMPLEMENTATION_GUIDE.md)** - Journey of porting from Go/Python to .NET
- **[TESTING.md](TESTING.md)** - Comprehensive testing guide
- **[LIBRARY_INTEGRATION.md](LIBRARY_INTEGRATION.md)** - Details on OData library usage

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