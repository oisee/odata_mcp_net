using System.Text.Json;
using Microsoft.Extensions.Logging;
using ODataMcp.Core.Models;

namespace ODataMcp.Core.Bridge;

/// <summary>
/// MCP server implementation
/// </summary>
public class McpServer : IMcpServer
{
    private readonly ILogger<McpServer> _logger;
    private readonly Dictionary<string, McpTool> _tools = new();
    private readonly Dictionary<string, Func<JsonElement?, Task<object>>> _handlers = new();
    
    public McpServer(ILogger<McpServer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RegisterTool(McpTool tool, Func<JsonElement?, Task<object>> handler)
    {
        _tools[tool.Name] = tool;
        _handlers[tool.Name] = handler;
        _logger.LogDebug("Registered tool: {ToolName}", tool.Name);
    }

    public async Task<McpMessage?> HandleMessageAsync(McpMessage message)
    {
        if (message is not McpRequest request)
            return null;
            
        _logger.LogDebug("Handling MCP request: {Method}", request.Method);
        
        try
        {
            var response = request.Method switch
            {
                "initialize" => await HandleInitializeAsync(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolCallAsync(request),
                _ => CreateErrorResponse(request.Id, Constants.Constants.JsonRpcMethodNotFound, $"Method not found: {request.Method}")
            };
            
            _logger.LogDebug("Handled request {Method} with response type {ResponseType}", 
                request.Method, response?.GetType().Name ?? "null");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request");
            return CreateErrorResponse(request.Id, Constants.Constants.JsonRpcInternalError, "Internal error");
        }
    }

    public IReadOnlyList<McpTool> GetTools()
    {
        return _tools.Values.ToList();
    }

    private Task<McpResponse> HandleInitializeAsync(McpRequest request)
    {
        var result = new McpInitializeResult
        {
            ProtocolVersion = Constants.Constants.McpProtocolVersion,
            ServerInfo = new McpServerInfo
            {
                Name = Constants.Constants.McpServerName,
                Version = Constants.Constants.McpServerVersion
            },
            Capabilities = new McpCapabilities
            {
                Tools = new Dictionary<string, object> { ["listChanged"] = true }
            }
        };
        
        return Task.FromResult(new McpResponse
        {
            Id = request.Id,
            Result = result
        });
    }

    private McpResponse HandleToolsList(McpRequest request)
    {
        var result = new McpToolsList
        {
            Tools = _tools.Values.ToList()
        };
        
        return new McpResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        if (request.Params == null)
        {
            return CreateErrorResponse(request.Id, Constants.Constants.JsonRpcInvalidParams, "Missing parameters");
        }
        
        var toolCall = JsonSerializer.Deserialize<McpToolCall>(request.Params.Value.GetRawText());
        if (toolCall == null || string.IsNullOrEmpty(toolCall.Name))
        {
            return CreateErrorResponse(request.Id, Constants.Constants.JsonRpcInvalidParams, "Invalid tool call");
        }
        
        if (!_handlers.TryGetValue(toolCall.Name, out var handler))
        {
            return CreateErrorResponse(request.Id, Constants.Constants.JsonRpcMethodNotFound, $"Tool not found: {toolCall.Name}");
        }
        
        try
        {
            var result = await handler(toolCall.Arguments);
            return new McpResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);
            return CreateErrorResponse(request.Id, Constants.Constants.JsonRpcInternalError, $"Tool execution error: {ex.Message}");
        }
    }

    private McpResponse CreateErrorResponse(object? id, int code, string message)
    {
        return new McpResponse
        {
            Id = id,
            Error = new McpError
            {
                Code = code,
                Message = message
            }
        };
    }
}