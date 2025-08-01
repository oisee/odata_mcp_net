using System.Text.Json;
using System.Text.Json.Serialization;

namespace ODataMcp.Core.Models;

/// <summary>
/// MCP message base class
/// </summary>
public class McpMessage
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

/// <summary>
/// MCP request message
/// </summary>
public class McpRequest : McpMessage
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;
    
    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

/// <summary>
/// MCP response message
/// </summary>
public class McpResponse : McpMessage
{
    [JsonPropertyName("result")]
    public object? Result { get; set; }
    
    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

/// <summary>
/// MCP error
/// </summary>
public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}

/// <summary>
/// MCP tool definition
/// </summary>
public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("inputSchema")]
    public JsonElement InputSchema { get; set; }
}

/// <summary>
/// MCP tool call parameters
/// </summary>
public class McpToolCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}

/// <summary>
/// MCP initialize parameters
/// </summary>
public class McpInitializeParams
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = string.Empty;
    
    [JsonPropertyName("capabilities")]
    public McpCapabilities Capabilities { get; set; } = new();
    
    [JsonPropertyName("clientInfo")]
    public McpClientInfo? ClientInfo { get; set; }
}

/// <summary>
/// MCP initialize result
/// </summary>
public class McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; set; } = string.Empty;
    
    [JsonPropertyName("capabilities")]
    public McpCapabilities Capabilities { get; set; } = new();
    
    [JsonPropertyName("serverInfo")]
    public McpServerInfo ServerInfo { get; set; } = new();
}

/// <summary>
/// MCP capabilities
/// </summary>
public class McpCapabilities
{
    [JsonPropertyName("tools")]
    public Dictionary<string, object>? Tools { get; set; }
    
    [JsonPropertyName("resources")]
    public Dictionary<string, object>? Resources { get; set; }
    
    [JsonPropertyName("prompts")]
    public Dictionary<string, object>? Prompts { get; set; }
}

/// <summary>
/// MCP client info
/// </summary>
public class McpClientInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

/// <summary>
/// MCP server info
/// </summary>
public class McpServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// MCP tools list result
/// </summary>
public class McpToolsList
{
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; set; } = new();
}

/// <summary>
/// Tool info for internal tracking
/// </summary>
public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string EntitySetName { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public Func<JsonElement?, Task<object>> Handler { get; set; } = _ => Task.FromResult<object>(new { });
}

/// <summary>
/// Trace information for debugging
/// </summary>
public class TraceInfo
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string? ODataVersion { get; set; }
    public int EntitySetCount { get; set; }
    public int FunctionImportCount { get; set; }
    public List<string> GeneratedTools { get; set; } = new();
    public Dictionary<string, object> Configuration { get; set; } = new();
}