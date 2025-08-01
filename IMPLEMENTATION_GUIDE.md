# OData MCP Bridge (.NET) Implementation Guide

## 1. Overview

The OData MCP Bridge for .NET is a cross-platform implementation that creates a bridge between OData services (v2 and v4) and the Model Context Protocol (MCP). It leverages Microsoft.OData.Client for robust OData support and provides a simplified MCP implementation for reliable AI assistant integration.

## 2. Core Architecture

### 2.1 Component Structure

```
odata_mcp_net/
├── src/
│   ├── ODataMcp/                    # CLI application
│   │   ├── Program.cs               # Main entry point
│   │   └── CommandLineOptions.cs    # CLI option definitions
│   └── ODataMcp.Core/               # Core library
│       ├── Configuration/           # Configuration models
│       │   └── ODataBridgeConfiguration.cs
│       ├── Mcp/                     # MCP implementation
│       │   └── SimpleMcpServerV2.cs # MCP server with tool generation
│       ├── Services/                # OData services
│       │   ├── SimpleODataService.cs    # OData client operations
│       │   ├── SimpleMetadataParser.cs  # Metadata parsing
│       │   └── CsrfTokenHandler.cs      # CSRF token management
│       └── Transport/               # Transport layer
│           └── SimpleStdioTransport.cs  # STDIO transport
├── Makefile                         # Cross-platform build automation
└── test scripts                     # Testing utilities
```

### 2.2 Key Components

1. **Main Entry Point** (`src/ODataMcp/Program.cs`):
   - Command-line parsing using System.CommandLine
   - Configuration from flags, environment variables, and .env files
   - Dependency injection setup
   - Graceful shutdown handling

2. **MCP Server** (`SimpleMcpServerV2.cs`):
   - Core MCP protocol implementation
   - Dynamic tool generation from OData metadata
   - Request routing and response handling
   - Support for both $ and non-$ parameter prefixes

3. **OData Service** (`SimpleODataService.cs`):
   - HTTP client for OData operations
   - Supports both v2 and v4 protocols
   - Authentication handling (Basic, Cookie)
   - CRUD operations implementation

4. **Metadata Parser** (`SimpleMetadataParser.cs`):
   - Flexible OData metadata parsing
   - Handles version differences automatically
   - Multiple parsing strategies for compatibility

5. **CSRF Token Handler** (`CsrfTokenHandler.cs`):
   - Automatic token fetching for SAP services
   - Token caching and refresh
   - Request decoration with tokens

## 3. Data Flow

### 3.1 Initialization Flow

```
1. CLI startup → Parse command-line arguments
2. Create configuration → Merge with environment variables
3. Initialize OData service → Fetch and parse metadata
4. Generate MCP tools → Create tools for each entity set
5. Start STDIO transport → Ready for JSON-RPC requests
```

### 3.2 Request Processing Flow

```
1. JSON-RPC request received → Parse via STDIO transport
2. Route to handler → Match method (initialize/tools/list/tools/call)
3. Execute tool → Build OData query/operation
4. Call OData service → Execute HTTP request
5. Transform response → Convert to MCP format
6. Return JSON-RPC response → Send via STDIO
```

## 4. Key Features Implementation

### 4.1 Dynamic Tool Generation

Tools are automatically generated based on OData metadata:

- **Entity Set Operations**:
  - `filter_{EntitySet}`: Query with OData options ($filter, $select, etc.)
  - `get_{EntitySet}`: Retrieve single entity by key
  - `count_{EntitySet}`: Get count with optional filter
  - `search_{EntitySet}`: Full-text search across string fields
  - `create_{EntitySet}`: Create new entity (unless read-only)
  - `update_{EntitySet}`: Update existing entity (unless read-only)
  - `delete_{EntitySet}`: Delete entity (unless read-only)

- **Special Tools**:
  - `odata_service_info`: Get service metadata and entity information

### 4.2 Authentication Mechanisms

1. **Basic Authentication**:
   ```csharp
   // Via command line
   --user myuser --password mypass
   
   // Via environment variables
   ODATA_USERNAME=myuser
   ODATA_PASSWORD=mypass
   ```

2. **Cookie Authentication**:
   - Command-line options present but not fully implemented
   - Extensible architecture for future implementation

3. **CSRF Token Handling**:
   - Automatic token fetching with HEAD request
   - Token caching with 30-minute lifetime
   - Automatic refresh on expiration
   - Applied to modifying operations

### 4.3 Command-Line Options

```
Core Options:
  --service, -s             OData service URL (required)
  --user, -u                Username for basic auth
  --password, -p            Password for basic auth
  --entities, -e            Entity filter (comma-separated, wildcards)
  --tool-shrink             Shorten tool names
  --read-only               Disable create/update/delete
  --claude-code-friendly    Remove $ prefixes from parameters
  --max-items               Maximum items per response (default: 100)
  --verbose, -v             Enable verbose logging
  --trace                   Show configuration and exit
```

### 4.4 Entity Filtering

Control which entities generate tools:
```bash
# Include specific entities
--entities "Products,Orders,Customers"

# Use wildcards
--entities "Product*,Order*"

# All entities (default)
--entities "*"
```

### 4.5 Tool Name Management

- **Normal mode**: `filter_Products`, `get_Orders`
- **Tool shrink mode**: `filter_products`, `get_orders` (lowercase, underscore-separated)
- Configurable via `--tool-shrink` flag

## 5. Protocol Specifics

### 5.1 OData Version Handling

The bridge automatically detects and handles version differences:

**OData v2**:
- XML-based metadata format
- Uses `$inlinecount` for counts
- Different namespace handling

**OData v4**:
- Enhanced metadata format
- Uses `$count` parameter
- Additional data types support
- `@odata.context` and `@odata.count` annotations

### 5.2 MCP Protocol Compliance

Implementation follows MCP specification:
- JSON-RPC 2.0 message format
- Proper error codes (-32700, -32600, etc.)
- Tool registration with JSON Schema
- Content wrapped in text blocks

## 6. Error Handling

### 6.1 Error Types

1. **Configuration Errors**: Missing service URL, invalid options
2. **Network Errors**: Connection failures, timeouts
3. **Authentication Errors**: 401/403 responses
4. **OData Errors**: Invalid queries, constraint violations
5. **MCP Protocol Errors**: Malformed requests, missing parameters

### 6.2 Error Response Format

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32602,
    "message": "Invalid params",
    "data": "Missing required parameter: filter"
  }
}
```

## 7. Security Considerations

### 7.1 Authentication Security
- Credentials stored in memory only during runtime
- Environment variable support for secure deployment
- No credentials logged in any output
- HTTPS recommended for all services

### 7.2 Operation Security
- Read-only mode available (`--read-only`)
- Entity filtering to limit exposure
- Response size limits (`--max-items`)
- No arbitrary code execution

### 7.3 Transport Security
- STDIO transport inherits process security
- No network exposure by default
- Future HTTP transport will be localhost-only

## 8. Performance Optimizations

### 8.1 Client Optimizations
- HTTP connection reuse via HttpClient
- Metadata cached after initial fetch
- Minimal memory allocations
- Efficient JSON serialization

### 8.2 Tool Management
- Tools generated once at initialization
- Lazy evaluation where possible
- Filtered tool creation based on configuration

## 9. Testing Strategy

### 9.1 Test Coverage
- Manual integration tests with public services
- Protocol compliance verification
- Multi-version OData compatibility
- Error scenario testing

### 9.2 Test Services
```bash
# Northwind v2
./odata-mcp --service https://services.odata.org/V2/Northwind/Northwind.svc/

# Northwind v4
./odata-mcp --service https://services.odata.org/V4/Northwind/Northwind.svc/

# TripPin v4
./odata-mcp --service https://services.odata.org/V4/TripPinServiceRW/
```

## 10. Extension Points

### 10.1 Adding New Transports
1. Implement transport interface
2. Add to dependency injection
3. Update command-line options
4. Handle in Program.cs

### 10.2 Custom Authentication
1. Extend SimpleODataService
2. Add configuration options
3. Implement request decoration

### 10.3 Additional Tool Types
1. Add tool creation method
2. Implement execution handler
3. Update tool counting logic

## 11. Debugging and Troubleshooting

### 11.1 Debug Options
- `--verbose`: Detailed logging output
- `--trace`: Display configuration without running
- Response includes full OData JSON

### 11.2 Common Issues

1. **Parse Errors on Initialize**:
   - Check service URL accessibility
   - Verify metadata endpoint returns valid XML

2. **Authentication Failures**:
   - Confirm credentials are correct
   - Check for required authentication headers

3. **No Tools Generated**:
   - Verify entity filter if using `--entities`
   - Check metadata parsing succeeded

4. **CSRF Token Issues (SAP)**:
   - Ensure service supports token fetching
   - Check token endpoint accessibility

## 12. Best Practices

### 12.1 Configuration
- Use environment variables for sensitive data
- Enable read-only mode for production
- Set appropriate response limits
- Filter entities to reduce tool count

### 12.2 Production Usage
- Always use HTTPS services
- Monitor memory usage for large services
- Implement proper error handling in clients
- Use specific entity filters

### 12.3 Development
- Test with multiple OData versions
- Validate against public test services
- Use trace mode for debugging
- Follow .NET coding conventions

## 13. Implementation Differences from Go Version

### Features Implemented
- ✅ Core MCP protocol
- ✅ Dynamic tool generation
- ✅ CRUD operations
- ✅ Search and count tools
- ✅ Basic authentication
- ✅ CSRF token handling
- ✅ Entity filtering
- ✅ Tool name shortening
- ✅ Read-only mode
- ✅ Claude-code-friendly mode

### Features Not Yet Implemented
- ❌ Function imports as tools
- ❌ Cookie file authentication
- ❌ Operation filtering (--enable/--disable)
- ❌ Service hints system
- ❌ HTTP/SSE transport
- ❌ Response metadata inclusion
- ❌ MCP trace logging

## 14. Future Enhancements

Planned improvements:
1. Function import support
2. Complete cookie authentication
3. Operation type filtering
4. Service hints system
5. HTTP/SSE transport option
6. Batch operation support
7. Enhanced error messages
8. Performance monitoring

This implementation provides a solid foundation for OData-to-MCP bridging with room for growth based on user needs and feedback.