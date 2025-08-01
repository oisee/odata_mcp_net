# OData MCP Bridge .NET v1.0.0

## 🎉 First Release!

We're excited to announce the first release of OData MCP Bridge for .NET! This release brings full OData to Model Context Protocol bridging capabilities to the .NET ecosystem.

### ✨ Features

- **Universal OData Support**: Works with both OData v2 and v4 services
- **Cross-Platform**: Native binaries for Windows, Linux, and macOS (Intel & Apple Silicon)
- **Dynamic Tool Generation**: Automatically creates 7+ MCP tools per OData entity:
  - `filter_{entity}` - Query with OData options
  - `get_{entity}` - Retrieve by key
  - `create_{entity}` - Create new entities
  - `update_{entity}` - Update existing entities
  - `delete_{entity}` - Delete entities
  - `count_{entity}` - Get counts with filters
  - `search_{entity}` - Full-text search
- **Authentication**: Basic auth and automatic CSRF token handling
- **SAP Ready**: Built-in support for SAP OData services
- **Claude-Code-Friendly**: Optional mode for better integration

### 📦 Downloads

All binaries are self-contained and include the .NET 8 runtime - no installation required!

| Platform | Architecture | Download | Size |
|----------|--------------|----------|------|
| Windows | x64 | [odata-mcp-1.0.0-win-x64.zip](../../releases/download/v1.0.0/odata-mcp-1.0.0-win-x64.zip) | ~80MB |
| Linux | x64 | [odata-mcp-1.0.0-linux-x64.tar.gz](../../releases/download/v1.0.0/odata-mcp-1.0.0-linux-x64.tar.gz) | ~85MB |
| macOS | Intel x64 | [odata-mcp-1.0.0-osx-x64.tar.gz](../../releases/download/v1.0.0/odata-mcp-1.0.0-osx-x64.tar.gz) | ~82MB |
| macOS | Apple Silicon | [odata-mcp-1.0.0-osx-arm64.tar.gz](../../releases/download/v1.0.0/odata-mcp-1.0.0-osx-arm64.tar.gz) | ~82MB |

### 🚀 Quick Start

1. Download the appropriate binary for your platform
2. Extract the archive
3. Add to your Claude Desktop configuration:

```json
{
    "mcpServers": {
        "odata": {
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

### 📖 Documentation

- [README](README.md) - Getting started guide
- [IMPLEMENTATION_GUIDE](IMPLEMENTATION_GUIDE.md) - Technical architecture
- [REIMPLEMENTATION_GUIDE](REIMPLEMENTATION_GUIDE.md) - Journey from Go/Python to .NET
- [CROSS_COMPILATION](CROSS_COMPILATION.md) - Building for all platforms

### 🙏 Acknowledgments

This project is a .NET reimplementation of:
- [odata-mcp](https://github.com/yyyo/odata-mcp) (Python) by yyyo
- [odata-mcp-go](https://github.com/yyyo/odata-mcp-go) (Go) by yyyo

### 📝 Changelog

**v1.0.0** - Initial Release
- Full MCP protocol implementation
- OData v2 and v4 support
- Dynamic tool generation (157+ tools for Northwind)
- Cross-platform self-contained binaries
- Basic authentication and CSRF token support
- Entity filtering and read-only modes
- Claude-code-friendly parameter handling

### 🐛 Known Issues

- Cookie authentication not fully implemented
- Function imports not yet exposed as tools

### 🔮 Next Steps

- Add cookie authentication support
- Implement function imports as MCP tools
- Add batch operation support
- Create automated CI/CD pipeline

---

**Full Changelog**: First release! 🎊