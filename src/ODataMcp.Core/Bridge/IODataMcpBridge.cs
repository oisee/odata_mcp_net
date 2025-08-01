using ODataMcp.Core.Models;
using ODataMcp.Core.Transport;

namespace ODataMcp.Core.Bridge;

/// <summary>
/// Interface for the OData MCP bridge
/// </summary>
public interface IODataMcpBridge
{
    /// <summary>
    /// Initialize the bridge
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Run the bridge with the specified transport
    /// </summary>
    Task RunAsync(ITransport transport, CancellationToken cancellationToken);
    
    /// <summary>
    /// Get trace information for debugging
    /// </summary>
    TraceInfo GetTraceInfo();
}