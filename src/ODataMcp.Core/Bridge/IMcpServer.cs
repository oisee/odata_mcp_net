using System.Text.Json;
using ODataMcp.Core.Models;

namespace ODataMcp.Core.Bridge;

/// <summary>
/// Interface for the MCP server
/// </summary>
public interface IMcpServer
{
    /// <summary>
    /// Register a tool with the server
    /// </summary>
    void RegisterTool(McpTool tool, Func<JsonElement?, Task<object>> handler);
    
    /// <summary>
    /// Handle an incoming MCP message
    /// </summary>
    Task<McpMessage?> HandleMessageAsync(McpMessage message);
    
    /// <summary>
    /// Get all registered tools
    /// </summary>
    IReadOnlyList<McpTool> GetTools();
}