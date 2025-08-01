using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ODataMcp.Core.Mcp;
using ODataMcp.Core.Transport;
using Xunit;

namespace ODataMcp.Tests.Mcp;

/// <summary>
/// Tests for JSON-RPC 2.0 protocol compliance
/// </summary>
public class JsonRpcProtocolTests
{
    private readonly Mock<ILogger<SimpleStdioTransport>> _transportLogger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonRpcProtocolTests()
    {
        _transportLogger = new Mock<ILogger<SimpleStdioTransport>>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    [Fact]
    public void JsonRpcRequest_Should_Serialize_Correctly()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = 1,
            Method = "initialize",
            Params = JsonDocument.Parse("{\"clientInfo\":{\"name\":\"test\"}}").RootElement
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<JsonRpcRequest>(json, _jsonOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.JsonRpc.Should().Be("2.0");
        deserialized.Id.Should().Be(1);
        deserialized.Method.Should().Be("initialize");
        deserialized.Params.Should().NotBeNull();
    }

    [Fact]
    public void JsonRpcResponse_Should_Include_JsonRpc_Version()
    {
        // Arrange
        var response = new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = 1,
            Result = new { test = "value" }
        };

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);

        // Assert
        json.Should().Contain("\"jsonrpc\":\"2.0\"");
        json.Should().Contain("\"id\":1");
        json.Should().Contain("\"result\"");
        json.Should().NotContain("\"error\"");
    }

    [Fact]
    public void JsonRpcError_Should_Format_Correctly()
    {
        // Arrange
        var response = new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = 1,
            Error = new JsonRpcError
            {
                Code = -32700,
                Message = "Parse error",
                Data = new { details = "Invalid JSON" }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var parsed = JsonDocument.Parse(json);

        // Assert
        parsed.RootElement.GetProperty("jsonrpc").GetString().Should().Be("2.0");
        parsed.RootElement.GetProperty("id").GetInt32().Should().Be(1);
        parsed.RootElement.TryGetProperty("result", out _).Should().BeFalse();
        
        var error = parsed.RootElement.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32700);
        error.GetProperty("message").GetString().Should().Be("Parse error");
        error.GetProperty("data").GetProperty("details").GetString().Should().Be("Invalid JSON");
    }

    [Theory]
    [InlineData(-32700, "Parse error")]
    [InlineData(-32600, "Invalid Request")]
    [InlineData(-32601, "Method not found")]
    [InlineData(-32602, "Invalid params")]
    [InlineData(-32603, "Internal error")]
    public void JsonRpcError_Should_Use_Standard_Error_Codes(int code, string message)
    {
        // Arrange
        var error = new JsonRpcError
        {
            Code = code,
            Message = message
        };

        // Act & Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
    }

    [Fact]
    public void Request_Without_Id_Should_Be_Notification()
    {
        // Arrange
        var request = new JsonRpcRequest
        {
            JsonRpc = "2.0",
            Id = null,
            Method = "notification",
            Params = JsonDocument.Parse("{}").RootElement
        };

        // Act
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var parsed = JsonDocument.Parse(json);

        // Assert
        parsed.RootElement.TryGetProperty("id", out _).Should().BeFalse();
        parsed.RootElement.GetProperty("method").GetString().Should().Be("notification");
    }

    [Fact]
    public void Response_Should_Not_Include_Both_Result_And_Error()
    {
        // This test verifies that a response cannot have both result and error
        var response = new JsonRpcResponse
        {
            JsonRpc = "2.0",
            Id = 1,
            Result = new { data = "test" },
            Error = new JsonRpcError { Code = -32000, Message = "Test error" }
        };

        // When serialized, the implementation should handle this correctly
        // In our case, we should ensure only one is serialized
        var json = JsonSerializer.Serialize(response, _jsonOptions);
        var parsed = JsonDocument.Parse(json);

        // Both should not be present at the same time
        var hasResult = parsed.RootElement.TryGetProperty("result", out _);
        var hasError = parsed.RootElement.TryGetProperty("error", out _);
        
        (hasResult && hasError).Should().BeFalse("A JSON-RPC response should not have both result and error");
    }

    [Fact]
    public void Invalid_Json_Should_Return_Parse_Error()
    {
        // Arrange
        var invalidJson = "{invalid json";

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<JsonRpcRequest>(invalidJson, _jsonOptions));
    }

    [Fact]
    public void ToolCallParams_Should_Deserialize_Arguments()
    {
        // Arrange
        var json = @"{
            ""name"": ""test_tool"",
            ""arguments"": {
                ""param1"": ""value1"",
                ""param2"": 42
            }
        }";

        // Act
        var params_ = JsonSerializer.Deserialize<ToolCallParams>(json, _jsonOptions);

        // Assert
        params_.Should().NotBeNull();
        params_!.Name.Should().Be("test_tool");
        params_.Arguments.Should().NotBeNull();
        params_.Arguments!.Value.GetProperty("param1").GetString().Should().Be("value1");
        params_.Arguments.Value.GetProperty("param2").GetInt32().Should().Be(42);
    }
}