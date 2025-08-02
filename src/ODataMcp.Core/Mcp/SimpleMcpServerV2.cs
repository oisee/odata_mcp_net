using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using ODataMcp.Core.Configuration;
using ODataMcp.Core.Services;
using ODataMcp.Core.Debug;

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
        var suffix = GetToolSuffix();
        
        _logger.LogInformation("ListToolsAsync called, model is null: {IsNull}, container is null: {ContainerNull}", 
            _model == null, _model?.EntityContainer == null);

        // Add service info tool
        tools.Add(new
        {
            name = $"odata_service_info{suffix}",
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
                tools.Add(CreateFilterTool(entitySet, shortName, suffix));

                // Get tool
                tools.Add(CreateGetTool(entitySet, entityType, shortName, suffix));

                // Count tool
                tools.Add(CreateCountTool(entitySet, shortName, suffix));

                // Search tool (only if entity has string properties)
                if (HasStringProperties(entityType))
                {
                    tools.Add(CreateSearchTool(entitySet, shortName, suffix));
                }

                if (!_config.ReadOnly)
                {
                    // Create tool
                    tools.Add(CreateCreateTool(entitySet, entityType, shortName, suffix));

                    // Update tool
                    tools.Add(CreateUpdateTool(entitySet, entityType, shortName, suffix));

                    // Delete tool
                    tools.Add(CreateDeleteTool(entitySet, entityType, shortName, suffix));
                }
            }
            
            // Add function imports (V2) or operations (V4)
            _logger.LogInformation("Looking for function imports in container");
            foreach (var element in _model.EntityContainer.Elements)
            {
                _logger.LogInformation("Container element: {Kind} - {Name}", 
                    element.ContainerElementKind, 
                    element is IEdmNamedElement named ? named.Name : "unnamed");
                    
                if (element.ContainerElementKind == EdmContainerElementKind.FunctionImport && 
                    element is IEdmFunctionImport functionImport)
                {
                    _logger.LogInformation("Adding function import tool: {Name}", functionImport.Name);
                    tools.Add(CreateFunctionTool(functionImport, suffix));
                }
            }
        }

        return Task.FromResult<object>(new { tools = tools });
    }

    public async Task<object> CallToolAsync(string name, JsonElement? arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("CallToolAsync called with name: {Name}", name);
            
            var suffix = GetToolSuffix();
            _logger.LogDebug("Tool suffix: {Suffix}", suffix);
            
            if (name == $"odata_service_info{suffix}")
            {
                return await GetServiceInfoAsync();
            }

            // Remove suffix from tool name
            if (name.EndsWith(suffix))
            {
                name = name[..^suffix.Length];
                _logger.LogDebug("Name after suffix removal: {Name}", name);
            }
            
            // Check if it's a function import
            _logger.LogDebug("Checking for function import: {Name}", name);
            if (_model?.EntityContainer != null)
            {
                var functionImport = _model.EntityContainer.Elements
                    .Where(e => e.ContainerElementKind == EdmContainerElementKind.FunctionImport)
                    .OfType<IEdmFunctionImport>()
                    .FirstOrDefault(f => name == f.Name);
                    
                _logger.LogDebug("Function import found: {Found}", functionImport != null);
                    
                if (functionImport != null)
                {
                    _logger.LogDebug("Executing function: {FunctionName}", functionImport.Name);
                    return await ExecuteFunctionAsync(functionImport, arguments);
                }
            }
            
            // Parse tool name pattern for entity operations
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
        
        // Note: $count is V4 feature, not supported in V2

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
                EdmPrimitiveTypeKind.Int16 => element.GetInt16().ToString(),
                EdmPrimitiveTypeKind.Int32 => element.GetInt32().ToString(),
                EdmPrimitiveTypeKind.Int64 => element.GetInt64().ToString(),
                EdmPrimitiveTypeKind.Double => element.GetDouble().ToString("G17"),
                EdmPrimitiveTypeKind.Decimal => element.GetDecimal().ToString(),
                EdmPrimitiveTypeKind.Boolean => element.GetBoolean().ToString().ToLower(),
                EdmPrimitiveTypeKind.DateTimeOffset => $"datetime'{element.GetDateTime():yyyy-MM-ddTHH:mm:ss}'",
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
        
        // Count function imports (V2) or operations (V4)
        var functionCount = 0;
        if (_model.EntityContainer.Elements.Any(e => e.ContainerElementKind == EdmContainerElementKind.FunctionImport))
        {
            functionCount = _model.EntityContainer.Elements
                .Where(e => e.ContainerElementKind == EdmContainerElementKind.FunctionImport)
                .Count();
        }
        
        return 1 + (entityCount * baseTools) + searchTools + modifyTools + functionCount;
    }

    private string GetToolSuffix()
    {
        // Generate a suffix from the service URL to distinguish tools from different services
        try
        {
            var uri = new Uri(_config.ServiceUrl);
            var host = uri.Host;
            var path = uri.AbsolutePath.Trim('/');
            
            // Extract a meaningful suffix from the URL
            if (path.Length > 0)
            {
                var parts = path.Split('/');
                var lastPart = parts.Last();
                
                // Remove common suffixes like .svc
                if (lastPart.EndsWith(".svc", StringComparison.OrdinalIgnoreCase))
                    lastPart = lastPart[..^4];
                
                // Use the last meaningful part of the path
                if (!string.IsNullOrEmpty(lastPart))
                    return $"_for_{ShortenName(lastPart)}";
            }
            
            // Fallback to host-based suffix
            var hostParts = host.Split('.');
            if (hostParts.Length > 2)
                return $"_for_{hostParts[^2]}"; // Use subdomain
            
            return $"_for_{hostParts[0]}";
        }
        catch
        {
            // If URL parsing fails, use a default suffix
            return "_for_OData";
        }
    }

    private object CreateFilterTool(IEdmEntitySet entitySet, string shortName, string suffix)
    {
        return new
        {
            name = $"filter_{shortName}{suffix}",
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

    private object CreateGetTool(IEdmEntitySet entitySet, IEdmEntityType entityType, string shortName, string suffix)
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
            name = $"get_{shortName}{suffix}",
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

    private object CreateCreateTool(IEdmEntitySet entitySet, IEdmEntityType entityType, string shortName, string suffix)
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
            name = $"create_{shortName}{suffix}",
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

    private object CreateUpdateTool(IEdmEntitySet entitySet, IEdmEntityType entityType, string shortName, string suffix)
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
            name = $"update_{shortName}{suffix}",
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

    private object CreateDeleteTool(IEdmEntitySet entitySet, IEdmEntityType entityType, string shortName, string suffix)
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
            name = $"delete_{shortName}{suffix}",
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

    private object CreateCountTool(IEdmEntitySet entitySet, string shortName, string suffix)
    {
        return new
        {
            name = $"count_{shortName}{suffix}",
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

    private object CreateSearchTool(IEdmEntitySet entitySet, string shortName, string suffix)
    {
        return new
        {
            name = $"search_{shortName}{suffix}",
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
        // Disable search functions for now - OData V2 service doesn't support contains() function
        // and we need to check metadata for actual searchability annotations
        return false;
        
        // Original logic (commented out until we can properly detect searchable entities):
        // return entityType.Properties().Any(p => 
        //     p.Type.Definition.TypeKind == EdmTypeKind.Primitive && 
        //     p.Type.AsPrimitive().PrimitiveKind() == EdmPrimitiveTypeKind.String);
    }

    private object CreateFunctionTool(IEdmFunctionImport functionImport, string suffix)
    {
        var parameters = new Dictionary<string, object>();
        
        foreach (var param in functionImport.Function.Parameters)
        {
            var paramDef = new Dictionary<string, object>
            {
                ["type"] = GetJsonSchemaType(param.Type),
                ["description"] = $"Parameter: {param.Name}"
            };
            
            parameters[param.Name] = paramDef;
        }
        
        var required = functionImport.Function.Parameters
            .Where(p => !p.Type.IsNullable)
            .Select(p => p.Name)
            .ToArray();
        
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = parameters,
            ["additionalProperties"] = false
        };
        
        if (required.Any())
        {
            schema["required"] = required;
        }
        
        return new
        {
            name = $"{functionImport.Name}{suffix}",
            description = $"Execute function {functionImport.Name}",
            inputSchema = schema
        };
    }

    private async Task<object> ExecuteFunctionAsync(IEdmFunctionImport functionImport, JsonElement? arguments)
    {
        var parameters = new Dictionary<string, string>();
        
        if (arguments.HasValue)
        {
            foreach (var param in functionImport.Function.Parameters)
            {
                if (arguments.Value.TryGetProperty(param.Name, out var value))
                {
                    parameters[param.Name] = value.ToString();
                }
            }
        }
        
        // Build function URL - V2 uses query string parameters, not parentheses
        var functionUrl = functionImport.Name;
        if (parameters.Any())
        {
            // For V2, parameters should not be URL encoded - they're simple values
            var paramString = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}"));
            functionUrl += $"?{paramString}";
        }
        
        _logger.LogDebug("ExecuteFunctionAsync building URL: functionName={FunctionName}, parameters={Parameters}", 
            functionImport.Name, string.Join(",", parameters.Select(p => $"{p.Key}={p.Value}")));
        _logger.LogDebug("Generated function URL: {FunctionUrl}", functionUrl);
        
        _logger.LogInformation("Executing function: {FunctionUrl}", functionUrl);
        
        using (var timer = new ODataMcpDebugger.PerformanceTimer($"ExecuteFunction: {functionUrl}"))
        {
            return await _odataService!.ExecuteFunctionAsync(functionUrl);
        }
    }
}