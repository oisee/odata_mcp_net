# OData MCP .NET Test Suite

This test suite provides comprehensive testing for the OData MCP server implementation, covering JSON-RPC protocol compliance, MCP handshake validation, and OData metadata parsing.

## Test Categories

### 1. JSON-RPC Protocol Tests (`JsonRpcProtocolTests.cs`)
- Validates JSON-RPC 2.0 protocol compliance
- Tests request/response serialization
- Verifies error response formatting
- Ensures proper handling of notifications (no ID)
- Validates standard error codes (-32700, -32600, etc.)

### 2. MCP Handshake Tests (`McpHandshakeTests.cs`)
- Tests the MCP initialization protocol
- Validates protocol version negotiation
- Verifies server capabilities response
- Tests authentication handling
- Ensures proper error handling for invalid services

### 3. STDIO Transport Tests (`StdioTransportTests.cs`)
- Tests the STDIO transport layer
- Validates message handling for all MCP methods
- Ensures no UTF-8 BOM in responses
- Tests error handling and invalid JSON
- Verifies notification handling (no response)

### 4. MCP Tools Tests (`McpToolsTests.cs`)
- Tests dynamic tool generation from OData metadata
- Validates tool schemas and input validation
- Tests entity filtering and tool shrinking
- Verifies read-only mode behavior
- Ensures search tools only exist for entities with string properties

### 5. OData Metadata Parsing Tests (`ODataMetadataParsingTests.cs`)
- Tests parsing of OData v2 and v4 metadata
- Validates complex types and associations
- Tests error handling for invalid metadata
- Verifies authentication header inclusion
- Tests fallback model creation

### 6. Northwind V2 Specific Tests (`NorthwindV2MetadataTests.cs`)
- Documents known issues with V2 metadata parsing
- Tests fallback model creation
- Validates entity set extraction from problematic metadata

### 7. Integration Tests (`McpServerIntegrationTests.cs`)
- End-to-end testing with real process execution
- Tests full MCP handshake flow
- Validates tool execution
- Tests error handling for invalid tools

## Running the Tests

### Run all tests:
```bash
dotnet test
```

### Run specific test categories:
```bash
# Unit tests only
dotnet test --filter "Category!=Integration"

# Integration tests only (requires built executable)
dotnet test --filter "Category=Integration"

# Specific test class
dotnet test --filter "FullyQualifiedName~JsonRpcProtocolTests"
```

### Run with coverage:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Known Issues

1. **Northwind V2 Metadata Parsing**: The Microsoft.OData.Edm.Csdl.CsdlReader has issues parsing OData v2 metadata from services like Northwind V2. The error "Value cannot be null. (Parameter 'key')" occurs due to incompatibilities with the older metadata format.

2. **Integration Tests**: These are marked with `[Fact(Skip = "...")]` as they require the executable to be built first. Remove the Skip attribute to run them after building the project.

## Test Dependencies

- **xUnit**: Test framework
- **FluentAssertions**: Fluent assertion library
- **Moq**: Mocking framework
- **Microsoft.NET.Test.Sdk**: .NET test SDK

## Best Practices

1. All tests should be independent and not rely on external services
2. Use mocking for HTTP calls and external dependencies
3. Integration tests should be clearly marked and skippable
4. Each test should focus on a single aspect of functionality
5. Use descriptive test names that explain what is being tested

## Adding New Tests

When adding new tests:
1. Place them in the appropriate category folder
2. Use consistent naming: `Should_ExpectedBehavior_When_Condition`
3. Include arrange, act, assert comments for clarity
4. Mock external dependencies
5. Add XML documentation for test classes

## Debugging Failed Tests

1. Check the test output for detailed error messages
2. Use the `--logger "console;verbosity=detailed"` flag for more information
3. Set breakpoints in both test and production code
4. Check for timing issues in async tests
5. Verify mock setups match actual usage