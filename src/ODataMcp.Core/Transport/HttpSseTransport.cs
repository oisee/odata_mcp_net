using Microsoft.Extensions.Logging;
using ODataMcp.Core.Models;

namespace ODataMcp.Core.Transport;

/// <summary>
/// HTTP/SSE transport for MCP (placeholder implementation)
/// </summary>
public class HttpSseTransport : ITransport
{
    private readonly ILogger<HttpSseTransport> _logger;
    private string _address = "localhost:8080";
    
    public HttpSseTransport(ILogger<HttpSseTransport> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    public void Configure(string address)
    {
        _address = address;
    }

    public Task StartAsync(Func<McpMessage, Task<McpMessage?>> messageHandler, CancellationToken cancellationToken)
    {
        _logger.LogInformation("HTTP/SSE transport not implemented yet");
        throw new NotImplementedException("HTTP/SSE transport is not implemented in this version");
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public Task SendAsync(McpMessage message)
    {
        return Task.CompletedTask;
    }
}