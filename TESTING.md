# Testing OData MCP .NET Bridge

## Quick Test

### 1. Build and Test Locally

```bash
# Build the project
dotnet build

# Run with help
dotnet run --project src/ODataMcp -- --help

# Test with Northwind in trace mode
dotnet run --project src/ODataMcp -- --service https://services.odata.org/V2/Northwind/Northwind.svc/ --trace
```

### 2. Test MCP Protocol

```bash
# Test initialize request
echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {"protocolVersion": "2024-11-05"}}' | \
dotnet run --project src/ODataMcp -- --service https://services.odata.org/V2/Northwind/Northwind.svc/

# Test with binary
dotnet publish -c Release -r osx-x64 --self-contained false -o bin/test
chmod +x bin/test/odata-mcp
echo '{"jsonrpc": "2.0", "id": 1, "method": "initialize", "params": {}}' | ./bin/test/odata-mcp --service https://services.odata.org/V2/Northwind/Northwind.svc/
```

## Claude Desktop Configuration

### macOS Configuration

Location: `~/Library/Application Support/Claude/claude_desktop_config.json`

```json
{
    "mcpServers": {
        "odata-net-test": {
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

### Windows Configuration

Location: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
    "mcpServers": {
        "odata-net-test": {
            "command": "C:\\path\\to\\odata-mcp.exe",
            "args": [
                "--service",
                "https://services.odata.org/V2/Northwind/Northwind.svc/",
                "--tool-shrink"
            ]
        }
    }
}
```

### Linux Configuration

Location: `~/.config/Claude/claude_desktop_config.json`

## Cross-Platform Build

```bash
# Build for all platforms
make publish-all

# Individual platforms
make publish-windows   # Creates bin/publish/win-x64/odata-mcp.exe
make publish-linux     # Creates bin/publish/linux-x64/odata-mcp
make publish-macos     # Creates bin/publish/osx-x64/odata-mcp and osx-arm64/odata-mcp
```

## Test Services

### 1. Northwind V2 (Public Test Service)
```
https://services.odata.org/V2/Northwind/Northwind.svc/
```

### 2. Northwind V4 (Public Test Service)
```
https://services.odata.org/V4/Northwind/Northwind.svc/
```

### 3. TripPin V4 (Public Test Service)
```
https://services.odata.org/V4/TripPinServiceRW/
```

## Known Limitations

The current implementation has basic structure but needs the following to be fully functional:

1. **Metadata Parsing**: Currently returns empty metadata - needs XML parsing implementation
2. **Tool Generation**: No tools are generated from entities yet
3. **OData Operations**: Query building and response handling need implementation
4. **HTTP/SSE Transport**: Only stdio transport is partially implemented

## Expected Behavior

When fully implemented, you should see:

1. **Initialize Response**: 
   ```json
   {
     "jsonrpc": "2.0",
     "id": 1,
     "result": {
       "protocolVersion": "2024-11-05",
       "serverInfo": {
         "name": "odata-mcp",
         "version": "1.0.0"
       },
       "capabilities": {
         "tools": {
           "listChanged": true
         }
       }
     }
   }
   ```

2. **Tools List**: Should show generated tools like:
   - `filter_Products`
   - `get_Products`
   - `create_Products`
   - `update_Products`
   - `delete_Products`
   - `odata_service_info`

## Debugging

Enable verbose logging:
```bash
dotnet run --project src/ODataMcp -- --service <url> --verbose
```

Enable MCP trace logging:
```bash
dotnet run --project src/ODataMcp -- --service <url> --trace-mcp
```

The trace file will be created in the temp directory.