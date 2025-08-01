using ODataMcp.Core.Models;

namespace ODataMcp.Core.Transport;

/// <summary>
/// Interface for MCP transport implementations
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Start the transport
    /// </summary>
    Task StartAsync(Func<McpMessage, Task<McpMessage?>> messageHandler, CancellationToken cancellationToken);
    
    /// <summary>
    /// Stop the transport
    /// </summary>
    Task StopAsync();
    
    /// <summary>
    /// Send a message
    /// </summary>
    Task SendAsync(McpMessage message);
}

/// <summary>
/// Interface for trace logging
/// </summary>
public interface ITraceLogger : IDisposable
{
    void LogIncoming(string message);
    void LogOutgoing(string message);
    string GetFilename();
}