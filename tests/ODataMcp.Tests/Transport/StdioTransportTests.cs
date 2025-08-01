using System.IO;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ODataMcp.Core.Configuration;
using ODataMcp.Core.Mcp;
using ODataMcp.Core.Transport;
using Xunit;

namespace ODataMcp.Tests.Transport;

/// <summary>
/// Tests for STDIO transport layer and message handling
/// </summary>
public class StdioTransportTests
{
    private readonly Mock<SimpleMcpServerV2> _serverMock;
    private readonly Mock<ILogger<SimpleStdioTransport>> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public StdioTransportTests()
    {
        _serverMock = new Mock<SimpleMcpServerV2>(
            new ODataBridgeConfiguration { ServiceUrl = "https://test.com" },
            new HttpClient(),
            Mock.Of<ILogger<SimpleMcpServerV2>>(),
            Mock.Of<IServiceProvider>());
        
        _logger = new Mock<ILogger<SimpleStdioTransport>>();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    [Fact]
    public async Task Transport_Should_Handle_Initialize_Request()
    {
        // Arrange
        var transport = new SimpleStdioTransport(_serverMock.Object, _logger.Object);
        
        var initResponse = new
        {
            protocolVersion = "2024-11-05",
            serverInfo = new { name = "test-server", version = "1.0.0" }
        };
        
        _serverMock
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initResponse);

        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "initialize",
            Params = JsonDocument.Parse("{}").RootElement
        };

        // Act
        var (input, output) = CreateTestStreams(JsonSerializer.Serialize(request, _jsonOptions));
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = RunTransportWithStreams(transport, input, output, cts.Token);
        
        await Task.Delay(100); // Give time to process
        cts.Cancel();
        
        // Assert
        output.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(output);
        var response = await reader.ReadToEndAsync();
        
        response.Should().Contain("\"protocolVersion\":\"2024-11-05\"");
        response.Should().Contain("\"name\":\"test-server\"");
        response.Should().Contain("\"id\":1");
        response.Should().NotContain("\"error\"");
    }

    [Fact]
    public async Task Transport_Should_Handle_Tools_List_Request()
    {
        // Arrange
        var transport = new SimpleStdioTransport(_serverMock.Object, _logger.Object);
        
        var toolsResponse = new
        {
            tools = new[]
            {
                new { name = "test_tool", description = "Test tool", inputSchema = new { type = "object" } }
            }
        };
        
        _serverMock
            .Setup(x => x.ListToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolsResponse);

        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 2,
            Method = "tools/list"
        };

        // Act
        var (input, output) = CreateTestStreams(JsonSerializer.Serialize(request, _jsonOptions));
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = RunTransportWithStreams(transport, input, output, cts.Token);
        
        await Task.Delay(100);
        cts.Cancel();
        
        // Assert
        output.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(output);
        var response = await reader.ReadToEndAsync();
        
        response.Should().Contain("\"tools\"");
        response.Should().Contain("\"test_tool\"");
        response.Should().Contain("\"id\":2");
    }

    [Fact]
    public async Task Transport_Should_Handle_Tool_Call_Request()
    {
        // Arrange
        var transport = new SimpleStdioTransport(_serverMock.Object, _logger.Object);
        
        var toolResult = new { data = "test result" };
        
        _serverMock
            .Setup(x => x.CallToolAsync(
                It.IsAny<string>(),
                It.IsAny<JsonElement?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolResult);

        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 3,
            Method = "tools/call",
            Params = JsonDocument.Parse(@"{""name"":""test_tool"",""arguments"":{}}").RootElement
        };

        // Act
        var (input, output) = CreateTestStreams(JsonSerializer.Serialize(request, _jsonOptions));
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = RunTransportWithStreams(transport, input, output, cts.Token);
        
        await Task.Delay(100);
        cts.Cancel();
        
        // Assert
        output.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(output);
        var response = await reader.ReadToEndAsync();
        
        response.Should().Contain("\"content\"");
        response.Should().Contain("\"type\":\"text\"");
        response.Should().Contain("test result");
        response.Should().Contain("\"id\":3");
    }

    [Fact]
    public async Task Transport_Should_Handle_Invalid_JSON()
    {
        // Arrange
        var transport = new SimpleStdioTransport(_serverMock.Object, _logger.Object);
        var invalidJson = "{invalid json}";

        // Act
        var (input, output) = CreateTestStreams(invalidJson);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = RunTransportWithStreams(transport, input, output, cts.Token);
        
        await Task.Delay(100);
        cts.Cancel();
        
        // Assert
        output.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(output);
        var response = await reader.ReadToEndAsync();
        
        response.Should().Contain("\"error\"");
        response.Should().Contain("\"code\":-32700");
        response.Should().Contain("Parse error");
    }

    [Fact]
    public async Task Transport_Should_Handle_Method_Not_Found()
    {
        // Arrange
        var transport = new SimpleStdioTransport(_serverMock.Object, _logger.Object);
        
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 4,
            Method = "unknown/method"
        };

        // Act
        var (input, output) = CreateTestStreams(JsonSerializer.Serialize(request, _jsonOptions));
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = RunTransportWithStreams(transport, input, output, cts.Token);
        
        await Task.Delay(100);
        cts.Cancel();
        
        // Assert
        output.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(output);
        var response = await reader.ReadToEndAsync();
        
        response.Should().Contain("\"error\"");
        response.Should().Contain("\"code\":-32601");
        response.Should().Contain("Method not found");
        response.Should().Contain("\"id\":4");
    }

    [Fact]
    public async Task Transport_Should_Not_Respond_To_Notifications()
    {
        // Arrange
        var transport = new SimpleStdioTransport(_serverMock.Object, _logger.Object);
        
        var notification = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = null, // No ID means it's a notification
            Method = "notification/test"
        };

        // Act
        var (input, output) = CreateTestStreams(JsonSerializer.Serialize(notification, _jsonOptions));
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = RunTransportWithStreams(transport, input, output, cts.Token);
        
        await Task.Delay(100);
        cts.Cancel();
        
        // Assert
        output.Length.Should().Be(0, "Notifications should not receive a response");
    }

    [Fact]
    public async Task Transport_Should_Handle_Server_Exceptions()
    {
        // Arrange
        var transport = new SimpleStdioTransport(_serverMock.Object, _logger.Object);
        
        _serverMock
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 5,
            Method = "initialize"
        };

        // Act
        var (input, output) = CreateTestStreams(JsonSerializer.Serialize(request, _jsonOptions));
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = RunTransportWithStreams(transport, input, output, cts.Token);
        
        await Task.Delay(100);
        cts.Cancel();
        
        // Assert
        output.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(output);
        var response = await reader.ReadToEndAsync();
        
        response.Should().Contain("\"error\"");
        response.Should().Contain("\"code\":-32603");
        response.Should().Contain("Internal error");
        response.Should().Contain("Test error");
        response.Should().Contain("\"id\":5");
    }

    [Fact]
    public async Task Transport_Should_Not_Include_BOM_In_Response()
    {
        // Arrange
        var transport = new SimpleStdioTransport(_serverMock.Object, _logger.Object);
        
        _serverMock
            .Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { protocolVersion = "2024-11-05" });

        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 6,
            Method = "initialize"
        };

        // Act
        var (input, output) = CreateTestStreams(JsonSerializer.Serialize(request, _jsonOptions));
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task = RunTransportWithStreams(transport, input, output, cts.Token);
        
        await Task.Delay(100);
        cts.Cancel();
        
        // Assert
        output.Seek(0, SeekOrigin.Begin);
        var bytes = new byte[3];
        output.Read(bytes, 0, 3);
        
        // Check for UTF-8 BOM (EF BB BF)
        var hasBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        hasBom.Should().BeFalse("Response should not contain UTF-8 BOM");
        
        // First character should be '{'
        output.Seek(0, SeekOrigin.Begin);
        var firstByte = output.ReadByte();
        firstByte.Should().Be((int)'{', "Response should start with JSON object");
    }

    private (Stream input, Stream output) CreateTestStreams(string inputContent)
    {
        var inputBytes = Encoding.UTF8.GetBytes(inputContent + "\n");
        var input = new MemoryStream(inputBytes);
        var output = new MemoryStream();
        return (input, output);
    }

    private async Task RunTransportWithStreams(SimpleStdioTransport transport, Stream input, Stream output, CancellationToken cancellationToken)
    {
        // Redirect console streams
        var originalIn = Console.In;
        var originalOut = Console.Out;
        
        try
        {
            Console.SetIn(new StreamReader(input));
            Console.SetOut(new StreamWriter(output) { AutoFlush = true });
            
            await transport.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when we cancel the token
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }
}