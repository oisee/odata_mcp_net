# OData MCP .NET - Library Integration Guide

## Overview

This guide explains how to leverage existing .NET libraries for MCP and OData instead of implementing everything from scratch.

## 1. Model Context Protocol (MCP) - Official SDK

### Installation

```bash
dotnet add package ModelContextProtocol --prerelease
```

### Key Benefits

- **Complete Protocol Implementation**: No need to implement JSON-RPC handling
- **Built-in Transport Support**: STDIO and HTTP/SSE transports included
- **Tool Management**: Automatic tool discovery via attributes
- **Type Safety**: Strongly-typed request/response handling
- **DI Integration**: Works seamlessly with Microsoft.Extensions.DependencyInjection

### Example Implementation

```csharp
using ModelContextProtocol;
using ModelContextProtocol.Core;

[McpServer("odata-mcp", "OData MCP Bridge", "1.0.0")]
public class ODataMcpService : IMcpServer
{
    [McpServerInitialize]
    public async Task<InitializeResult> InitializeAsync(InitializeParams parameters)
    {
        // Initialize OData connection
        return new InitializeResult
        {
            ProtocolVersion = "2024-11-05",
            ServerInfo = new ServerInfo { Name = "odata-mcp", Version = "1.0.0" },
            Capabilities = new ServerCapabilities { Tools = new ToolsCapability() }
        };
    }

    [McpServerTool("filter_Products", "Query Products with OData filters")]
    public async Task<object> FilterProducts(
        [McpToolParameter("filter", "OData filter expression")] string? filter,
        [McpToolParameter("top", "Maximum items to return")] int? top)
    {
        // Implementation using OData Client
    }
}
```

## 2. Microsoft.OData.Client - OData Operations

### Installation

```bash
dotnet add package Microsoft.OData.Client
dotnet add package Microsoft.OData.Core
dotnet add package Microsoft.OData.Edm
```

### Key Benefits

- **Automatic Metadata Parsing**: No need to parse XML manually
- **LINQ Support**: Write queries in C# instead of OData syntax
- **Type Safety**: Generate strongly-typed clients
- **Built-in Authentication**: Supports various auth mechanisms
- **Efficient Batching**: Batch multiple operations

### Example Usage

```csharp
using Microsoft.OData.Client;
using Microsoft.OData.Edm;

// Initialize context
var context = new DataServiceContext(new Uri("https://services.odata.org/V2/Northwind/Northwind.svc/"))
{
    Format = new ODataMessageReaderSettings()
};

// Get metadata automatically
IEdmModel model = await context.GetMetadataAsync();

// Query with LINQ
var query = context.CreateQuery<Product>("Products")
    .Where(p => p.UnitPrice > 10)
    .OrderBy(p => p.ProductName)
    .Take(10);

var results = await query.ExecuteAsync();

// Dynamic queries (when types aren't known at compile time)
var dynamicQuery = context.CreateQuery<IDictionary<string, object>>("Products")
    .AddQueryOption("$filter", "UnitPrice gt 10")
    .AddQueryOption("$top", "10");

var dynamicResults = await dynamicQuery.ExecuteAsync();
```

## 3. Integration Architecture

### Current Implementation (Custom)
```
┌─────────────┐     ┌─────────────┐     ┌──────────────┐
│   Program   │────▶│ McpServer   │────▶│ ODataClient  │
│   (STDIO)   │     │  (Custom)   │     │   (Custom)   │
└─────────────┘     └─────────────┘     └──────────────┘
```

### Recommended Implementation (Using Libraries)
```
┌─────────────┐     ┌─────────────────┐     ┌────────────────────┐
│   Program   │────▶│ MCP SDK Host    │────▶│ Microsoft.OData    │
│   (Host)    │     │ (Official SDK)  │     │ Client Library     │
└─────────────┘     └─────────────────┘     └────────────────────┘
```

## 4. Migration Steps

### Step 1: Replace Custom MCP Implementation

Replace `McpServer.cs` with MCP SDK hosting:

```csharp
// Program.cs
using ModelContextProtocol.Hosting;

var builder = McpServerBuilder.Create(args);
builder.Services.AddSingleton<ODataBridgeConfiguration>();
builder.AddServer<ODataMcpService>();

var app = builder.Build();
await app.RunAsync();
```

### Step 2: Use OData Client for Metadata

Replace `MetadataParser.cs` with OData Client:

```csharp
public async Task<IEdmModel> GetMetadataAsync()
{
    var context = new DataServiceContext(new Uri(_config.ServiceUrl));
    return await context.GetMetadataAsync();
}
```

### Step 3: Dynamic Tool Generation

Use MCP SDK's capability to register tools dynamically:

```csharp
public class DynamicODataTools
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IEdmModel _model;

    public void RegisterEntityTools()
    {
        foreach (var entitySet in _model.EntityContainer.EntitySets())
        {
            _toolRegistry.RegisterTool(new ToolDefinition
            {
                Name = $"filter_{entitySet.Name}",
                Description = $"Query {entitySet.Name}",
                Parameters = BuildFilterParameters(entitySet)
            });
        }
    }
}
```

## 5. Advanced Features

### Using OData Code Generation

For known services, generate strongly-typed clients:

```bash
# Install OData Connected Service or OData CLI
dotnet tool install -g Microsoft.OData.Cli

# Generate client code
odata-cli generate -m https://services.odata.org/V2/Northwind/Northwind.svc/$metadata -o NorthwindClient.cs
```

### Batch Operations

```csharp
// Batch multiple operations
var batch = new DataServiceRequestBatch(context);
batch.Add(context.CreateQuery<Product>("Products").Where(p => p.Discontinued));
batch.Add(context.SaveChangesOptions.Batch);

var batchResponse = await context.ExecuteBatchAsync(batch);
```

### Authentication Examples

```csharp
// Basic Authentication
context.Credentials = new NetworkCredential(username, password);

// Bearer Token
context.SendingRequest2 += (sender, e) =>
{
    e.RequestMessage.SetHeader("Authorization", $"Bearer {token}");
};

// Client Certificate
var handler = new HttpClientHandler();
handler.ClientCertificates.Add(certificate);
context.HttpRequestTransportMode = HttpRequestTransportMode.HttpClient;
context.Configurations.RequestPipeline.OnMessageCreating = (args) => 
    new HttpClientRequestMessage(handler);
```

## 6. Benefits Summary

### Using Official Libraries vs Custom Implementation

| Feature | Custom Implementation | Using Libraries |
|---------|---------------------|-----------------|
| MCP Protocol | ~500 lines of code | Built-in |
| Metadata Parsing | Complex XML parsing | One method call |
| Query Building | String manipulation | LINQ or fluent API |
| Error Handling | Manual implementation | Built-in with proper types |
| Authentication | Manual headers | Built-in support |
| Maintenance | High | Low (library updates) |
| Performance | Variable | Optimized |
| Type Safety | Limited | Full support |

## 7. Next Steps

1. **Update Dependencies**: Add ModelContextProtocol SDK
2. **Refactor Program.cs**: Use MCP SDK hosting
3. **Replace MetadataParser**: Use DataServiceContext.GetMetadataAsync()
4. **Implement Dynamic Tools**: Register tools based on EDM model
5. **Add Query Service**: Use DataServiceQuery for operations
6. **Test Integration**: Verify with Northwind service

## Example: Complete Filter Tool

```csharp
[McpServerTool("filter_entity", "Query entities with OData filters")]
public async Task<ToolResult> FilterEntity(
    [McpToolParameter("entity", "Entity set name", required: true)] string entity,
    [McpToolParameter("filter", "OData filter expression")] string? filter,
    [McpToolParameter("select", "Properties to select")] string? select,
    [McpToolParameter("orderby", "Order by expression")] string? orderBy,
    [McpToolParameter("top", "Maximum items")] int? top = 100)
{
    var query = _context.CreateQuery<IDictionary<string, object>>(entity);
    
    if (!string.IsNullOrEmpty(filter))
        query = query.AddQueryOption("$filter", filter);
    
    if (!string.IsNullOrEmpty(select))
        query = query.AddQueryOption("$select", select);
        
    if (!string.IsNullOrEmpty(orderBy))
        query = query.AddQueryOption("$orderby", orderBy);
        
    if (top.HasValue)
        query = query.AddQueryOption("$top", top.Value.ToString());
    
    var results = await query.ExecuteAsync();
    
    return new ToolResult
    {
        Content = JsonSerializer.Serialize(results)
    };
}
```

This approach significantly reduces code complexity while providing more features and better reliability.