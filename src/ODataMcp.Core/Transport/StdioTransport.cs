using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ODataMcp.Core.Models;

namespace ODataMcp.Core.Transport;

/// <summary>
/// Standard I/O transport for MCP
/// </summary>
public class StdioTransport : ITransport
{
    private readonly ILogger<StdioTransport> _logger;
    private Func<McpMessage, Task<McpMessage?>>? _messageHandler;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _readTask;
    
    public StdioTransport(ILogger<StdioTransport> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(Func<McpMessage, Task<McpMessage?>> messageHandler, CancellationToken cancellationToken)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        _logger.LogInformation("Starting STDIO transport");
        
        // Start reading from stdin
        _readTask = Task.Run(() => ReadLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        
        // Wait until cancelled
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping STDIO transport");
        
        _cancellationTokenSource?.Cancel();
        
        if (_readTask != null)
        {
            try
            {
                await _readTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
        
        _cancellationTokenSource?.Dispose();
    }

    public async Task SendAsync(McpMessage message)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(message, message.GetType(), options);
            
            _logger.LogDebug("Sending message: {Message}", json);
            
            await Console.Out.WriteLineAsync(json);
            await Console.Out.FlushAsync();
            
            _logger.LogDebug("Message sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            throw;
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var reader = Console.In;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                {
                    _logger.LogInformation("EOF received, stopping read loop");
                    break;
                }
                
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                _logger.LogDebug("Received message: {Message}", line);
                
                // Parse message
                var message = JsonSerializer.Deserialize<McpRequest>(line);
                if (message != null && _messageHandler != null)
                {
                    var response = await _messageHandler(message);
                    if (response != null)
                    {
                        await SendAsync(response);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON message");
                
                // Send parse error response
                var errorResponse = new McpResponse
                {
                    Error = new McpError
                    {
                        Code = Constants.Constants.JsonRpcParseError,
                        Message = "Parse error"
                    }
                };
                await SendAsync(errorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in read loop");
            }
        }
    }
}