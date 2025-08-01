using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Moq;
using Moq.Protected;
using ODataMcp.Core.Configuration;
using ODataMcp.Core.Mcp;
using Xunit;

namespace ODataMcp.Tests.Mcp;

/// <summary>
/// Tests for MCP tools generation and execution
/// </summary>
public class McpToolsTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<SimpleMcpServerV2>> _logger;
    private readonly IServiceProvider _serviceProvider;

    public McpToolsTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);
        _logger = new Mock<ILogger<SimpleMcpServerV2>>();
        
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task ListTools_Should_Generate_Tools_From_Metadata()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata",
            ReadOnly = false
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""TestNamespace"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""Product"">
                            <Key>
                                <PropertyRef Name=""ID"" />
                            </Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""Name"" Type=""Edm.String"" />
                            <Property Name=""Price"" Type=""Edm.Decimal"" />
                        </EntityType>
                        <EntityContainer Name=""TestContainer"">
                            <EntitySet Name=""Products"" EntityType=""TestNamespace.Product"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(metadataXml);

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);
        await server.InitializeAsync();

        // Act
        var result = await server.ListToolsAsync();
        
        // Assert
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonDocument.Parse(json);
        
        var tools = parsed.RootElement.GetProperty("tools").EnumerateArray().ToList();
        
        // Should have: service_info + filter + get + count + search + create + update + delete = 8 tools
        tools.Should().HaveCount(8);
        
        // Check service info tool
        var serviceInfoTool = tools.FirstOrDefault(t => t.GetProperty("name").GetString() == "odata_service_info");
        serviceInfoTool.GetProperty("description").GetString().Should().Be("Get information about the OData service");
        
        // Check filter tool
        var filterTool = tools.FirstOrDefault(t => t.GetProperty("name").GetString() == "filter_Products");
        filterTool.GetProperty("description").GetString().Should().Be("Query Products with OData filters");
        
        // Check get tool
        var getTool = tools.FirstOrDefault(t => t.GetProperty("name").GetString() == "get_Products");
        getTool.GetProperty("description").GetString().Should().Be("Get a specific Products by key");
        
        // Check CRUD tools exist
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "create_Products");
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "update_Products");
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "delete_Products");
        
        // Check count tool
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "count_Products");
        
        // Check search tool (should exist because Product has string properties)
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "search_Products");
    }

    [Fact]
    public async Task ListTools_Should_Respect_ReadOnly_Configuration()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata",
            ReadOnly = true // Read-only mode
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""TestNamespace"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""Product"">
                            <Key>
                                <PropertyRef Name=""ID"" />
                            </Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""Name"" Type=""Edm.String"" />
                        </EntityType>
                        <EntityContainer Name=""TestContainer"">
                            <EntitySet Name=""Products"" EntityType=""TestNamespace.Product"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(metadataXml);

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);
        await server.InitializeAsync();

        // Act
        var result = await server.ListToolsAsync();
        
        // Assert
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonDocument.Parse(json);
        
        var tools = parsed.RootElement.GetProperty("tools").EnumerateArray().ToList();
        
        // Should NOT have create, update, delete tools
        tools.Should().NotContain(t => t.GetProperty("name").GetString()!.StartsWith("create_"));
        tools.Should().NotContain(t => t.GetProperty("name").GetString()!.StartsWith("update_"));
        tools.Should().NotContain(t => t.GetProperty("name").GetString()!.StartsWith("delete_"));
        
        // Should still have read tools
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "filter_Products");
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "get_Products");
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "count_Products");
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "search_Products");
    }

    [Fact]
    public async Task ListTools_Should_Apply_Entity_Filters()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata",
            Entities = new List<string> { "Products", "Cust*" } // Only Products and entities starting with Cust
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""TestNamespace"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""Product"">
                            <Key><PropertyRef Name=""ID"" /></Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                        </EntityType>
                        <EntityType Name=""Customer"">
                            <Key><PropertyRef Name=""ID"" /></Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                        </EntityType>
                        <EntityType Name=""Order"">
                            <Key><PropertyRef Name=""ID"" /></Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                        </EntityType>
                        <EntityContainer Name=""TestContainer"">
                            <EntitySet Name=""Products"" EntityType=""TestNamespace.Product"" />
                            <EntitySet Name=""Customers"" EntityType=""TestNamespace.Customer"" />
                            <EntitySet Name=""Orders"" EntityType=""TestNamespace.Order"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(metadataXml);

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);
        await server.InitializeAsync();

        // Act
        var result = await server.ListToolsAsync();
        
        // Assert
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonDocument.Parse(json);
        
        var tools = parsed.RootElement.GetProperty("tools").EnumerateArray().ToList();
        
        // Should have tools for Products and Customers, but not Orders
        tools.Should().Contain(t => t.GetProperty("name").GetString()!.Contains("Products"));
        tools.Should().Contain(t => t.GetProperty("name").GetString()!.Contains("Customers"));
        tools.Should().NotContain(t => t.GetProperty("name").GetString()!.Contains("Orders"));
    }

    [Fact]
    public async Task ListTools_Should_Use_Tool_Shrink_Names()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata",
            ToolShrink = true
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""TestNamespace"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""CustomerOrder"">
                            <Key><PropertyRef Name=""ID"" /></Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                        </EntityType>
                        <EntityContainer Name=""TestContainer"">
                            <EntitySet Name=""CustomerOrders"" EntityType=""TestNamespace.CustomerOrder"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(metadataXml);

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);
        await server.InitializeAsync();

        // Act
        var result = await server.ListToolsAsync();
        
        // Assert
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonDocument.Parse(json);
        
        var tools = parsed.RootElement.GetProperty("tools").EnumerateArray().ToList();
        
        // Should have shortened name: CustomerOrders -> customer_orders
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "filter_customer_orders");
        tools.Should().NotContain(t => t.GetProperty("name").GetString() == "filter_CustomerOrders");
    }

    [Fact]
    public async Task Tool_Input_Schema_Should_Be_Valid()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata",
            MaxItems = 50
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""TestNamespace"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""Product"">
                            <Key>
                                <PropertyRef Name=""ID"" />
                            </Key>
                            <Property Name=""ID"" Type=""Edm.Guid"" Nullable=""false"" />
                            <Property Name=""Name"" Type=""Edm.String"" />
                            <Property Name=""Price"" Type=""Edm.Decimal"" Nullable=""false"" />
                            <Property Name=""IsActive"" Type=""Edm.Boolean"" />
                        </EntityType>
                        <EntityContainer Name=""TestContainer"">
                            <EntitySet Name=""Products"" EntityType=""TestNamespace.Product"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(metadataXml);

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);
        await server.InitializeAsync();

        // Act
        var result = await server.ListToolsAsync();
        
        // Assert
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonDocument.Parse(json);
        
        var tools = parsed.RootElement.GetProperty("tools").EnumerateArray().ToList();
        
        // Check filter tool schema
        var filterTool = tools.First(t => t.GetProperty("name").GetString() == "filter_Products");
        var filterSchema = filterTool.GetProperty("inputSchema");
        filterSchema.GetProperty("type").GetString().Should().Be("object");
        filterSchema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        
        var filterProps = filterSchema.GetProperty("properties");
        filterProps.GetProperty("top").GetProperty("maximum").GetInt32().Should().Be(50);
        
        // Check get tool schema (key is GUID)
        var getTool = tools.First(t => t.GetProperty("name").GetString() == "get_Products");
        var getSchema = getTool.GetProperty("inputSchema");
        var getProps = getSchema.GetProperty("properties");
        getProps.GetProperty("ID").GetProperty("type").GetString().Should().Be("string"); // GUID is string in JSON
        
        var getRequired = getSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        getRequired.Should().Contain("ID");
        
        // Check create tool schema
        var createTool = tools.First(t => t.GetProperty("name").GetString() == "create_Products");
        var createSchema = createTool.GetProperty("inputSchema");
        var createProps = createSchema.GetProperty("properties");
        
        // Check property types
        createProps.GetProperty("ID").GetProperty("type").GetString().Should().Be("string");
        createProps.GetProperty("Name").GetProperty("type").GetString().Should().Be("string");
        createProps.GetProperty("Price").GetProperty("type").GetString().Should().Be("number");
        createProps.GetProperty("IsActive").GetProperty("type").GetString().Should().Be("boolean");
        
        // Check required fields (non-nullable)
        var createRequired = createSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        createRequired.Should().Contain("ID");
        createRequired.Should().Contain("Price");
        createRequired.Should().NotContain("Name"); // Nullable
        createRequired.Should().NotContain("IsActive"); // Nullable
    }

    [Fact]
    public async Task Search_Tool_Should_Only_Exist_For_Entities_With_String_Properties()
    {
        // Arrange
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = "https://example.com/odata"
        };

        var metadataXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""TestNamespace"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""NumericEntity"">
                            <Key><PropertyRef Name=""ID"" /></Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""Value"" Type=""Edm.Decimal"" />
                            <Property Name=""Count"" Type=""Edm.Int64"" />
                        </EntityType>
                        <EntityType Name=""TextEntity"">
                            <Key><PropertyRef Name=""ID"" /></Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""Name"" Type=""Edm.String"" />
                            <Property Name=""Description"" Type=""Edm.String"" />
                        </EntityType>
                        <EntityContainer Name=""TestContainer"">
                            <EntitySet Name=""NumericEntities"" EntityType=""TestNamespace.NumericEntity"" />
                            <EntitySet Name=""TextEntities"" EntityType=""TestNamespace.TextEntity"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(metadataXml);

        var server = new SimpleMcpServerV2(config, _httpClient, _logger.Object, _serviceProvider);
        await server.InitializeAsync();

        // Act
        var result = await server.ListToolsAsync();
        
        // Assert
        var json = JsonSerializer.Serialize(result);
        var parsed = JsonDocument.Parse(json);
        
        var tools = parsed.RootElement.GetProperty("tools").EnumerateArray().ToList();
        
        // Should have search tool for TextEntities but not NumericEntities
        tools.Should().Contain(t => t.GetProperty("name").GetString() == "search_TextEntities");
        tools.Should().NotContain(t => t.GetProperty("name").GetString() == "search_NumericEntities");
    }

    private void SetupMetadataResponse(string metadataXml)
    {
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
    }
}