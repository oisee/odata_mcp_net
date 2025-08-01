using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ODataMcp.Core.Configuration;
using ODataMcp.Core.Mcp;
using Xunit;

namespace ODataMcp.Tests.Mcp;

/// <summary>
/// Tests for MCP handshake and initialization protocol
/// </summary>
public class McpHandshakeTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<SimpleMcpServerV2>> _logger;
    private readonly IServiceProvider _serviceProvider;

    public McpHandshakeTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);
        _logger = new Mock<ILogger<SimpleMcpServerV2>>();
        
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Initialize_Should_Return_Protocol_Version()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata"
        };

        // Mock metadata response
        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""TestNamespace"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""TestEntity"">
                            <Key>
                                <PropertyRef Name=""ID"" />
                            </Key>
                            <Property Name=""ID"" Type=""Edm.String"" Nullable=""false"" />
                            <Property Name=""Name"" Type=""Edm.String"" />
                        </EntityType>
                        <EntityContainer Name=""TestContainer"">
                            <EntitySet Name=""TestEntities"" EntityType=""TestNamespace.TestEntity"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("$metadata")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(metadataXml)
            });

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);

        // Act
        var result = await server.InitializeAsync();

        // Assert
        result.Should().NotBeNull();
        
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonDocument.Parse(json);

        parsed.RootElement.GetProperty("protocolVersion").GetString().Should().Be("2024-11-05");
        
        var serverInfo = parsed.RootElement.GetProperty("serverInfo");
        serverInfo.GetProperty("name").GetString().Should().Be("odata-mcp");
        serverInfo.GetProperty("version").GetString().Should().Be("1.0.0");

        var capabilities = parsed.RootElement.GetProperty("capabilities");
        capabilities.GetProperty("tools").GetProperty("listChanged").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Initialize_Should_Handle_Authentication()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata",
            Username = "testuser",
            Password = "testpass"
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""Test"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityContainer Name=""Container"" />
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
            {
                // Verify authorization header
                request.Headers.Authorization.Should().NotBeNull();
                request.Headers.Authorization!.Scheme.Should().Be("Basic");
                
                return new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(metadataXml)
                };
            });

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);

        // Act
        var result = await server.InitializeAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Initialize_Should_Fail_With_Invalid_Service_Url()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://invalid.example.com/odata"
        };

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.NotFound,
                Content = new StringContent("Not Found")
            });

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => server.InitializeAsync());
    }

    [Fact]
    public async Task Initialize_Should_Handle_Malformed_Metadata()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata"
        };

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent("<invalid>xml</invalid>")
            });

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => server.InitializeAsync());
    }

    [Fact]
    public async Task Initialize_Should_Set_Server_Capabilities()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata"
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""Test"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityContainer Name=""Container"" />
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(metadataXml)
            });

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);

        // Act
        var result = await server.InitializeAsync();

        // Assert
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonDocument.Parse(json);

        // Check capabilities structure
        parsed.RootElement.TryGetProperty("capabilities", out var capabilities).Should().BeTrue();
        capabilities.TryGetProperty("tools", out var tools).Should().BeTrue();
        tools.TryGetProperty("listChanged", out var listChanged).Should().BeTrue();
        listChanged.GetBoolean().Should().BeTrue();

        // Verify we don't have resources or prompts capabilities (not implemented)
        capabilities.TryGetProperty("resources", out _).Should().BeFalse();
        capabilities.TryGetProperty("prompts", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_Should_Log_Success()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata"
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""Test"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""Product"">
                            <Key><PropertyRef Name=""ID"" /></Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                        </EntityType>
                        <EntityContainer Name=""Container"">
                            <EntitySet Name=""Products"" EntityType=""Test.Product"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = System.Net.HttpStatusCode.OK,
                Content = new StringContent(metadataXml)
            });

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);

        // Act
        await server.InitializeAsync();

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing OData MCP server")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initialized OData MCP server with")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}