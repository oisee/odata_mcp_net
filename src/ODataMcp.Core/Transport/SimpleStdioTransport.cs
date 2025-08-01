using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ODataMcp.Core.Mcp;

namespace ODataMcp.Core.Transport;

/// <summary>
/// Simple STDIO transport for MCP
/// </summary>
public class SimpleStdioTransport
{
    private readonly SimpleMcpServerV2 _server;
    private readonly ILogger<SimpleStdioTransport> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SimpleStdioTransport(SimpleMcpServerV2 server, ILogger<SimpleStdioTransport> logger)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stdin = Console.OpenStandardInput();
            using var reader = new StreamReader(stdin, new UTF8Encoding(false)); // No BOM
            
            await using var stdout = Console.OpenStandardOutput();
            using var writer = new StreamWriter(stdout, new UTF8Encoding(false)) { AutoFlush = true }; // No BOM

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break;

                try
                {
                    var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, _jsonOptions);
                    if (request == null)
                        continue;

                    var response = await HandleRequestAsync(request, cancellationToken);
                    if (response != null)
                    {
                        var json = JsonSerializer.Serialize(response, _jsonOptions);
                        await writer.WriteLineAsync(json);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON received");
                    var errorResponse = new JsonRpcResponse
                    {
                        Id = null,
                        Error = new JsonRpcError
                        {
                            Code = -32700,
                            Message = "Parse error"
                        }
                    };
                    await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse, _jsonOptions));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling request");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Transport cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transport error");
            throw;
        }
    }

    private async Task<JsonRpcResponse?> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            switch (request.Method)
            {
                case "initialize":
                    var initResult = await _server.InitializeAsync(cancellationToken);
                    return new JsonRpcResponse
                    {
                        Id = request.Id,
                        Result = initResult
                    };

                case "tools/list":
                    var tools = await _server.ListToolsAsync(cancellationToken);
                    return new JsonRpcResponse
                    {
                        Id = request.Id,
                        Result = tools
                    };

                case "tools/call":
                    if (request.Params == null)
                    {
                        return CreateErrorResponse(request.Id, -32602, "Invalid params");
                    }

                    var callParams = JsonSerializer.Deserialize<ToolCallParams>(request.Params.Value.GetRawText(), _jsonOptions);
                    if (callParams == null || string.IsNullOrEmpty(callParams.Name))
                    {
                        return CreateErrorResponse(request.Id, -32602, "Invalid params");
                    }

                    var result = await _server.CallToolAsync(callParams.Name, callParams.Arguments, cancellationToken);
                    
                    // Wrap result in MCP format
                    var mcpResult = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = JsonSerializer.Serialize(result, _jsonOptions)
                            }
                        }
                    };
                    
                    return new JsonRpcResponse
                    {
                        Id = request.Id,
                        Result = mcpResult
                    };

                default:
                    return CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {Method}", request.Method);
            // Also write to stderr for Claude Desktop debugging
            await Console.Error.WriteLineAsync($"Error handling {request.Method}: {ex}");
            return CreateErrorResponse(request.Id, -32603, $"Internal error: {ex.Message}");
        }
    }

    private JsonRpcResponse CreateErrorResponse(object? id, int code, string message)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        };
    }
}

// JSON-RPC types
public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; set; }
    
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; set; }
    
    [JsonPropertyName("result")]
    public object? Result { get; set; }
    
    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
    
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

public class ToolCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}