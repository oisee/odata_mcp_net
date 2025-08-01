using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using ODataMcp.Core.Configuration;
using ODataMcp.Core.Services;

namespace ODataMcp.Core.Mcp;

/// <summary>
/// Simplified MCP server implementation for OData
/// </summary>
public class SimpleMcpServerV2
{
    private readonly ODataBridgeConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SimpleMcpServerV2> _logger;
    private readonly IServiceProvider _serviceProvider;
    private SimpleODataService? _odataService;
    private IEdmModel? _model;

    public SimpleMcpServerV2(ODataBridgeConfiguration config, HttpClient httpClient, ILogger<SimpleMcpServerV2> logger, IServiceProvider serviceProvider)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task<object> InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing OData MCP server for {ServiceUrl}", _config.ServiceUrl);

        try
        {
            // Initialize OData service
            var odataLogger = _serviceProvider.GetRequiredService<ILogger<SimpleODataService>>();
            _odataService = new SimpleODataService(
                _httpClient,
                _config.ServiceUrl,
                _config.Username,
                _config.Password,
                odataLogger);

            // Get metadata
            _model = await _odataService.GetMetadataAsync(cancellationToken);

            var toolCount = CountTools();
            _logger.LogInformation("Initialized OData MCP server with {ToolCount} tools", toolCount);

            return new
            {
                capabilities = new
                {
                    prompts = new { listChanged = false },
                    resources = new { listChanged = false, subscribe = false },
                    tools = new { listChanged = true }
                },
                protocolVersion = "2024-11-05",
                serverInfo = new
                {
                    name = "odata-mcp",
                    version = "1.0.0"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize OData service");
            // Also write to stderr for Claude Desktop debugging
            await Console.Error.WriteLineAsync($"Failed to initialize OData service: {ex}");
            throw;
        }
    }

    public Task<object> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<object>();

        // Add service info tool
        tools.Add(new
        {
            name = "odata_service_info",
            description = "Get information about the OData service",
            inputSchema = new
            {
                type = "object",
                properties = new { },
                additionalProperties = false
            }
        });

        // Add entity-specific tools
        if (_model?.EntityContainer != null)
        {
            foreach (var entitySet in _model.EntityContainer.EntitySets())
            {
                if (!ShouldIncludeEntity(entitySet.Name))
                    continue;

                var shortName = _config.ToolShrink ? ShortenName(entitySet.Name) : entitySet.Name;
                var entityType = entitySet.EntityType;

                // Filter tool
                tools.Add(CreateFilterTool(entitySet, shortName));

                // Get tool
                tools.Add(CreateGetTool(entitySet, entityType, shortName));

                // Count tool
                tools.Add(CreateCountTool(entitySet, shortName));

                // Search tool (if entity has string properties)
                if (HasStringProperties(entityType))
                {
                    tools.Add(CreateSearchTool(entitySet, shortName));
                }

                if (!_config.ReadOnly)
                {
                    // Create tool
                    tools.Add(CreateCreateTool(entitySet, entityType, shortName));

                    // Update tool
                    tools.Add(CreateUpdateTool(entitySet, entityType, shortName));

                    // Delete tool
                    tools.Add(CreateDeleteTool(entitySet, entityType, shortName));
                }
            }
        }

        return Task.FromResult<object>(new { tools = tools });
    }

    public async Task<object> CallToolAsync(string name, JsonElement? arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            if (name == "odata_service_info")
            {
                return await GetServiceInfoAsync();
            }

            // Parse tool name pattern
            var parts = name.Split('_', 2);
            if (parts.Length != 2)
            {
                throw new ArgumentException($"Invalid tool name format: {name}");
            }

            var operation = parts[0];
            var entityShortName = parts[1];

            // Find the actual entity name
            var entityName = FindEntityName(entityShortName);
            if (entityName == null)
            {
                throw new ArgumentException($"Entity not found: {entityShortName}");
            }

            // Execute operation
            return operation switch
            {
                "filter" => await ExecuteFilterAsync(entityName, arguments ?? JsonDocument.Parse("{}").RootElement),
                "get" => await ExecuteGetAsync(entityName, arguments ?? JsonDocument.Parse("{}").RootElement),
                "count" => await ExecuteCountAsync(entityName, arguments ?? JsonDocument.Parse("{}").RootElement),
                "search" => await ExecuteSearchAsync(entityName, arguments ?? JsonDocument.Parse("{}").RootElement),
                "create" => await ExecuteCreateAsync(entityName, arguments ?? JsonDocument.Parse("{}").RootElement),
                "update" => await ExecuteUpdateAsync(entityName, arguments ?? JsonDocument.Parse("{}").RootElement),
                "delete" => await ExecuteDeleteAsync(entityName, arguments ?? JsonDocument.Parse("{}").RootElement),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", name);
            throw; // Let the transport layer handle the error with proper JSON-RPC structure
        }
    }

    private Task<object> GetServiceInfoAsync()
    {
        if (_model == null) throw new InvalidOperationException("Model not initialized");

        var entitySets = new List<object>();
        if (_model.EntityContainer != null)
        {
            foreach (var es in _model.EntityContainer.EntitySets())
            {
                if (ShouldIncludeEntity(es.Name))
                {
                    entitySets.Add(new
                    {
                        name = es.Name,
                        type = es.EntityType.FullTypeName(),
                        properties = es.EntityType.Properties().Select(p => new
                        {
                            name = p.Name,
                            type = GetEdmTypeName(p.Type),
                            nullable = p.Type.IsNullable
                        }).ToList()
                    });
                }
            }
        }

        return Task.FromResult<object>(new
        {
            serviceUrl = _config.ServiceUrl,
            version = _model.GetEdmVersion()?.ToString() ?? "Unknown",
            entitySets = entitySets
        });
    }

    private async Task<object> ExecuteFilterAsync(string entityName, JsonElement args)
    {
        var queryOptions = new Dictionary<string, string>();
        
        // Support both formats: with and without $ prefix
        if ((args.TryGetProperty("filter", out var f) || args.TryGetProperty("$filter", out f)) && f.ValueKind == JsonValueKind.String)
            queryOptions["$filter"] = f.GetString()!;
        
        if ((args.TryGetProperty("orderby", out var o) || args.TryGetProperty("$orderby", out o)) && o.ValueKind == JsonValueKind.String)
            queryOptions["$orderby"] = o.GetString()!;
        
        if ((args.TryGetProperty("select", out var s) || args.TryGetProperty("$select", out s)) && s.ValueKind == JsonValueKind.String)
            queryOptions["$select"] = s.GetString()!;
        
        if ((args.TryGetProperty("expand", out var e) || args.TryGetProperty("$expand", out e)) && e.ValueKind == JsonValueKind.String)
            queryOptions["$expand"] = e.GetString()!;
        
        if ((args.TryGetProperty("top", out var t) || args.TryGetProperty("$top", out t)) && t.TryGetInt32(out var topVal))
            queryOptions["$top"] = topVal.ToString();
        
        if ((args.TryGetProperty("skip", out var sk) || args.TryGetProperty("$skip", out sk)) && sk.TryGetInt32(out var skipVal))
            queryOptions["$skip"] = skipVal.ToString();
        
        queryOptions["$count"] = "true";

        return await _odataService!.ExecuteQueryAsync(entityName, queryOptions);
    }

    private async Task<object> ExecuteGetAsync(string entityName, JsonElement args)
    {
        var entitySet = _model!.EntityContainer!.FindEntitySet(entityName);
        var keyString = ExtractKeyString(args, entitySet!.EntityType);
        return await _odataService!.GetByKeyAsync(entityName, keyString) ?? new { error = "Not found" };
    }

    private async Task<object> ExecuteCreateAsync(string entityName, JsonElement args)
    {
        var entity = JsonSerializer.Deserialize<Dictionary<string, object>>(args.GetRawText())!;
        return await _odataService!.CreateAsync(entityName, entity);
    }

    private async Task<object> ExecuteUpdateAsync(string entityName, JsonElement args)
    {
        var entitySet = _model!.EntityContainer!.FindEntitySet(entityName);
        var keyString = ExtractKeyString(args, entitySet!.EntityType);
        
        var updates = args.TryGetProperty("updates", out var u)
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(u.GetRawText())!
            : throw new ArgumentException("Missing updates");

        return await _odataService!.UpdateAsync(entityName, keyString, updates);
    }

    private async Task<object> ExecuteDeleteAsync(string entityName, JsonElement args)
    {
        var entitySet = _model!.EntityContainer!.FindEntitySet(entityName);
        var keyString = ExtractKeyString(args, entitySet!.EntityType);
        var success = await _odataService!.DeleteAsync(entityName, keyString);
        return new { success = success };
    }

    private async Task<object> ExecuteCountAsync(string entityName, JsonElement args)
    {
        var queryOptions = new Dictionary<string, string>();
        
        if (args.TryGetProperty("filter", out var f) && f.ValueKind == JsonValueKind.String)
            queryOptions["$filter"] = f.GetString()!;
        
        queryOptions["$count"] = "true";
        queryOptions["$top"] = "0"; // We only want the count, not the data
        
        var result = await _odataService!.ExecuteQueryAsync(entityName, queryOptions);
        
        // Extract count from OData response
        if (result is JsonElement jsonResult && jsonResult.TryGetProperty("@odata.count", out var countProp))
        {
            return new { count = countProp.GetInt64() };
        }
        
        return new { count = 0 };
    }

    private async Task<object> ExecuteSearchAsync(string entityName, JsonElement args)
    {
        if (!args.TryGetProperty("searchTerm", out var searchTerm) || searchTerm.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("Missing required parameter: searchTerm");
        }

        var term = searchTerm.GetString()!;
        var entitySet = _model!.EntityContainer!.FindEntitySet(entityName);
        var entityType = entitySet!.EntityType;

        // Build a filter that searches all string properties
        var stringProperties = entityType.Properties()
            .Where(p => p.Type.Definition.TypeKind == EdmTypeKind.Primitive && 
                       p.Type.AsPrimitive().PrimitiveKind() == EdmPrimitiveTypeKind.String)
            .ToList();

        if (!stringProperties.Any())
        {
            return new { value = new object[0] };
        }

        // Create OR filter for all string properties
        var filters = stringProperties.Select(p => $"contains(tolower({p.Name}), tolower('{term}'))");
        var combinedFilter = string.Join(" or ", filters);

        var queryOptions = new Dictionary<string, string>
        {
            ["$filter"] = combinedFilter
        };

        if (args.TryGetProperty("top", out var t) && t.TryGetInt32(out var topVal))
            queryOptions["$top"] = topVal.ToString();
        
        if (args.TryGetProperty("skip", out var sk) && sk.TryGetInt32(out var skipVal))
            queryOptions["$skip"] = skipVal.ToString();

        return await _odataService!.ExecuteQueryAsync(entityName, queryOptions);
    }

    private string ExtractKeyString(JsonElement args, IEdmEntityType entityType)
    {
        var keyProperties = entityType.Key().ToList();

        if (keyProperties.Count == 1)
        {
            var keyProp = keyProperties[0];
            if (args.TryGetProperty(keyProp.Name, out var keyValue))
            {
                return FormatKeyValue(keyValue, keyProp.Type);
            }
            throw new ArgumentException($"Missing key property: {keyProp.Name}");
        }

        // For composite keys
        var keyParts = new List<string>();
        foreach (var keyProp in keyProperties)
        {
            if (args.TryGetProperty(keyProp.Name, out var keyValue))
            {
                keyParts.Add($"{keyProp.Name}={FormatKeyValue(keyValue, keyProp.Type)}");
            }
            else
            {
                throw new ArgumentException($"Missing key property: {keyProp.Name}");
            }
        }
        return string.Join(",", keyParts);
    }

    private string FormatKeyValue(JsonElement element, IEdmTypeReference typeRef)
    {
        if (typeRef.IsPrimitive())
        {
            var primitiveType = typeRef.AsPrimitive();
            return primitiveType.PrimitiveKind() switch
            {
                EdmPrimitiveTypeKind.String => $"'{element.GetString()}'",
                EdmPrimitiveTypeKind.Guid => $"guid'{element.GetString()}'",
                _ => element.GetString()!
            };
        }
        return element.GetString()!;
    }

    private string? FindEntityName(string shortName)
    {
        if (_model?.EntityContainer == null)
            return null;

        foreach (var entitySet in _model.EntityContainer.EntitySets())
        {
            if (entitySet.Name.Equals(shortName, StringComparison.OrdinalIgnoreCase))
                return entitySet.Name;

            if (_config.ToolShrink)
            {
                var shortened = ShortenName(entitySet.Name);
                if (shortened.Equals(shortName, StringComparison.OrdinalIgnoreCase))
                    return entitySet.Name;
            }
        }

        return null;
    }

    private int CountTools()
    {
        if (_model?.EntityContainer == null)
            return 1; // Just service info

        var entityCount = _model.EntityContainer.EntitySets()
            .Count(es => ShouldIncludeEntity(es.Name));

        // Each entity gets: filter, get, count, search (optional), and optionally create, update, delete
        var baseTools = 3; // filter, get, count
        var searchTools = _model.EntityContainer.EntitySets()
            .Count(es => ShouldIncludeEntity(es.Name) && HasStringProperties(es.EntityType));
        var modifyTools = _config.ReadOnly ? 0 : entityCount * 3; // create, update, delete
        return 1 + (entityCount * baseTools) + searchTools + modifyTools;
    }

    private object CreateFilterTool(IEdmEntitySet entitySet, string shortName)
    {
        return new
        {
            name = $"filter_{shortName}",
            description = $"Query {entitySet.Name} with OData filters",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    filter = new { type = "string", description = "OData filter expression (e.g., 'Price gt 10')" },
                    select = new { type = "string", description = "Comma-separated list of properties to return" },
                    orderby = new { type = "string", description = "Order by expression (e.g., 'Name desc')" },
                    expand = new { type = "string", description = "Navigation properties to expand" },
                    top = new { type = "integer", description = "Maximum number of items to return", minimum = 1, maximum = _config.MaxItems },
                    skip = new { type = "integer", description = "Number of items to skip", minimum = 0 }
                },
                additionalProperties = false
            }
        };
    }

    private object CreateGetTool(IEdmEntitySet entitySet, IEdmEntityType entityType, string shortName)
    {
        var keyProperties = entityType.Key().ToList();
        var properties = new Dictionary<string, object>();

        foreach (var key in keyProperties)
        {
            properties[key.Name] = new
            {
                type = GetJsonSchemaType(key.Type),
                description = $"Key property: {key.Name}"
            };
        }

        return new
        {
            name = $"get_{shortName}",
            description = $"Get a specific {entitySet.Name} by key",
            inputSchema = new
            {
                type = "object",
                properties = properties,
                required = keyProperties.Select(k => k.Name).ToArray(),
                additionalProperties = false
            }
        };
    }

    private object CreateCreateTool(IEdmEntitySet entitySet, IEdmEntityType entityType, string shortName)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var property in entityType.Properties())
        {
            if (property is IEdmStructuralProperty structProp)
            {
                var propSchema = new Dictionary<string, object>
                {
                    ["type"] = GetJsonSchemaType(structProp.Type),
                    ["description"] = $"Property: {property.Name}"
                };

                if (!structProp.Type.IsNullable)
                {
                    required.Add(property.Name);
                }

                properties[property.Name] = propSchema;
            }
        }

        return new
        {
            name = $"create_{shortName}",
            description = $"Create a new {entitySet.Name}",
            inputSchema = new
            {
                type = "object",
                properties = properties,
                required = required.ToArray(),
                additionalProperties = false
            }
        };
    }

    private object CreateUpdateTool(IEdmEntitySet entitySet, IEdmEntityType entityType, string shortName)
    {
        var properties = new Dictionary<string, object>();
        var keyProperties = entityType.Key().ToList();

        foreach (var key in keyProperties)
        {
            properties[key.Name] = new
            {
                type = GetJsonSchemaType(key.Type),
                description = $"Key property: {key.Name}"
            };
        }

        properties["updates"] = new
        {
            type = "object",
            description = "Properties to update",
            additionalProperties = true
        };

        return new
        {
            name = $"update_{shortName}",
            description = $"Update an existing {entitySet.Name}",
            inputSchema = new
            {
                type = "object",
                properties = properties,
                required = keyProperties.Select(k => k.Name).Concat(new[] { "updates" }).ToArray(),
                additionalProperties = false
            }
        };
    }

    private object CreateDeleteTool(IEdmEntitySet entitySet, IEdmEntityType entityType, string shortName)
    {
        var keyProperties = entityType.Key().ToList();
        var properties = new Dictionary<string, object>();

        foreach (var key in keyProperties)
        {
            properties[key.Name] = new
            {
                type = GetJsonSchemaType(key.Type),
                description = $"Key property: {key.Name}"
            };
        }

        return new
        {
            name = $"delete_{shortName}",
            description = $"Delete a {entitySet.Name}",
            inputSchema = new
            {
                type = "object",
                properties = properties,
                required = keyProperties.Select(k => k.Name).ToArray(),
                additionalProperties = false
            }
        };
    }

    private bool ShouldIncludeEntity(string entityName)
    {
        if (_config.Entities == null || _config.Entities.Count == 0)
            return true;

        return _config.Entities.Any(pattern =>
            pattern == "*" ||
            pattern == entityName ||
            (pattern.EndsWith("*") && entityName.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)));
    }

    private string ShortenName(string entityName)
    {
        var result = new System.Text.StringBuilder();
        bool lastWasLower = false;

        foreach (char c in entityName)
        {
            if (char.IsUpper(c))
            {
                if (lastWasLower && result.Length > 0)
                    result.Append('_');
                result.Append(char.ToLower(c));
                lastWasLower = false;
            }
            else
            {
                result.Append(c);
                lastWasLower = true;
            }
        }

        return result.ToString();
    }

    private string GetJsonSchemaType(IEdmTypeReference typeRef)
    {
        if (typeRef.IsPrimitive())
        {
            var primitiveType = typeRef.AsPrimitive();
            return primitiveType.PrimitiveKind() switch
            {
                EdmPrimitiveTypeKind.String => "string",
                EdmPrimitiveTypeKind.Int16 => "integer",
                EdmPrimitiveTypeKind.Int32 => "integer",
                EdmPrimitiveTypeKind.Int64 => "integer",
                EdmPrimitiveTypeKind.Double => "number",
                EdmPrimitiveTypeKind.Decimal => "number",
                EdmPrimitiveTypeKind.Boolean => "boolean",
                EdmPrimitiveTypeKind.DateTimeOffset => "string",
                EdmPrimitiveTypeKind.Guid => "string",
                _ => "string"
            };
        }
        return "string";
    }

    private string GetEdmTypeName(IEdmTypeReference typeRef)
    {
        if (typeRef.IsPrimitive())
        {
            return typeRef.AsPrimitive().PrimitiveKind().ToString();
        }
        return typeRef.Definition.FullTypeName();
    }

    private object CreateCountTool(IEdmEntitySet entitySet, string shortName)
    {
        return new
        {
            name = $"count_{shortName}",
            description = $"Get count of {entitySet.Name} with optional filter",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    filter = new { type = "string", description = "OData filter expression to count matching items" }
                },
                additionalProperties = false
            }
        };
    }

    private object CreateSearchTool(IEdmEntitySet entitySet, string shortName)
    {
        return new
        {
            name = $"search_{shortName}",
            description = $"Search {entitySet.Name} across all text fields",
            inputSchema = new
            {
                type = "object",
                properties = new
                {
                    searchTerm = new { type = "string", description = "Text to search for (case-insensitive)" },
                    top = new { type = "integer", description = "Maximum number of items to return", minimum = 1, maximum = _config.MaxItems },
                    skip = new { type = "integer", description = "Number of items to skip", minimum = 0 }
                },
                required = new[] { "searchTerm" },
                additionalProperties = false
            }
        };
    }

    private bool HasStringProperties(IEdmEntityType entityType)
    {
        return entityType.Properties().Any(p => 
            p.Type.Definition.TypeKind == EdmTypeKind.Primitive && 
            p.Type.AsPrimitive().PrimitiveKind() == EdmPrimitiveTypeKind.String);
    }
}