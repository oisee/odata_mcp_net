# OData MCP Bridge for .NET

A .NET implementation of the OData to Model Context Protocol (MCP) bridge, allowing AI assistants like Claude to interact with OData services through a standardized interface.

This is a .NET 8 port of the Python/Go OData-MCP bridge implementations, leveraging Microsoft.OData.Client for robust OData support.

## Features

- **Universal OData Support**: Works with both OData v2 and v4 services
- **Cross-Platform**: Runs on Windows, Linux, and macOS (Intel & Apple Silicon)
- **Dynamic Tool Generation**: Automatically creates MCP tools based on OData metadata
- **Multiple Authentication Methods**: Basic auth, cookie auth, and anonymous access
- **SAP OData Extensions**: Full support for SAP-specific OData features including CSRF tokens
- **Comprehensive CRUD Operations**: Generated tools for create, read, update, delete operations
- **Advanced Query Support**: OData query options ($filter, $select, $expand, $orderby, etc.)
- **Function Import Support**: Call OData function imports as MCP tools
- **Flexible Tool Naming**: Configurable tool naming with prefix/postfix options
- **Entity Filtering**: Selective tool generation with wildcard support
- **Read-Only Modes**: Restrict operations with `--read-only` or `--read-only-but-functions`
- **MCP Protocol Debugging**: Built-in trace logging with `--trace-mcp` for troubleshooting
- **Service-Specific Hints**: Flexible hint system with pattern matching for known service issues
- **Full MCP Compliance**: Complete protocol implementation for all MCP clients
- **Multiple Transports**: Support for stdio (default) and HTTP/SSE transports

## Prerequisites

- .NET 8.0 SDK or later
- Or: .NET 8.0 Runtime (for pre-built binaries)

## Installation

### From Source

```bash
git clone https://github.com/yourusername/odata_mcp_net.git
cd odata_mcp_net

# Build for current platform
make build

# Or build for all platforms
make publish-all
```

### Pre-built Binaries

Download the appropriate binary for your platform from the releases page.

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

- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Linux**: `~/.config/Claude/claude_desktop_config.json`

```json
{
    "mcpServers": {
        "northwind": {
            "command": "/path/to/odata-mcp",
            "args": [
                "--service",
                "https://services.odata.org/V2/Northwind/Northwind.svc/",
                "--tool-shrink"
            ]
        }
    }
}
```

### Authentication

```bash
# Basic authentication
./odata-mcp --user admin --password secret https://my-service.com/odata/

# Cookie file authentication
./odata-mcp --cookie-file cookies.txt https://my-service.com/odata/

# Environment variables
export ODATA_USERNAME=admin
export ODATA_PASSWORD=secret
./odata-mcp https://my-service.com/odata/
```

## Building

### Development Build

```bash
# Restore packages and build
dotnet build

# Run tests
dotnet test

# Run with help
dotnet run --project src/ODataMcp -- --help
```

### Publishing for Distribution

```bash
# Windows
make publish-windows

# Linux
make publish-linux

# macOS (Intel and Apple Silicon)
make publish-macos

# All platforms
make publish-all
```

## Command Line Options

Run `./odata-mcp --help` for a full list of options.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.