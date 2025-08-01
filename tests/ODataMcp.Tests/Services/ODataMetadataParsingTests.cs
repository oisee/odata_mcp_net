using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Moq;
using Moq.Protected;
using ODataMcp.Core.Services;
using Xunit;

namespace ODataMcp.Tests.Services;

/// <summary>
/// Integration tests for OData metadata parsing with real-world scenarios
/// </summary>
public class ODataMetadataParsingTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger> _logger;

    public ODataMetadataParsingTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);
        _logger = new Mock<ILogger>();
    }

    [Fact]
    public async Task Should_Parse_OData_V4_Metadata()
    {
        // Arrange
        var v4Metadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""ODataDemo"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityType Name=""Product"">
                            <Key>
                                <PropertyRef Name=""ID"" />
                            </Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""Name"" Type=""Edm.String"" />
                            <Property Name=""Description"" Type=""Edm.String"" />
                            <Property Name=""ReleaseDate"" Type=""Edm.DateTimeOffset"" Nullable=""false"" />
                            <Property Name=""DiscontinuedDate"" Type=""Edm.DateTimeOffset"" />
                            <Property Name=""Rating"" Type=""Edm.Int16"" Nullable=""false"" />
                            <Property Name=""Price"" Type=""Edm.Double"" Nullable=""false"" />
                        </EntityType>
                        <EntityType Name=""Category"">
                            <Key>
                                <PropertyRef Name=""ID"" />
                            </Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""Name"" Type=""Edm.String"" />
                        </EntityType>
                        <EntityContainer Name=""DemoService"">
                            <EntitySet Name=""Products"" EntityType=""ODataDemo.Product"" />
                            <EntitySet Name=""Categories"" EntityType=""ODataDemo.Category"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(v4Metadata);

        // Act
        var model = await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object);

        // Assert
        model.Should().NotBeNull();
        model.EntityContainer.Should().NotBeNull();
        model.EntityContainer.Name.Should().Be("DemoService");
        
        var entitySets = model.EntityContainer.EntitySets().ToList();
        entitySets.Should().HaveCount(2);
        entitySets.Should().Contain(es => es.Name == "Products");
        entitySets.Should().Contain(es => es.Name == "Categories");

        // Check Product entity type
        var productSet = entitySets.First(es => es.Name == "Products");
        var productType = productSet.EntityType;
        productType.Name.Should().Be("Product");
        
        var properties = productType.Properties().ToList();
        properties.Should().HaveCount(7);
        properties.Should().Contain(p => p.Name == "ID" && p.Type.Definition.TypeKind == EdmTypeKind.Primitive);
        properties.Should().Contain(p => p.Name == "Price" && p.Type.AsPrimitive().PrimitiveKind() == EdmPrimitiveTypeKind.Double);
    }

    [Fact]
    public async Task Should_Parse_OData_V2_Metadata_With_DataServices_Namespace()
    {
        // Arrange - This is similar to the Northwind V2 structure
        var v2Metadata = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
            <edmx:Edmx Version=""1.0"" xmlns:edmx=""http://schemas.microsoft.com/ado/2007/06/edmx"">
                <edmx:DataServices m:DataServiceVersion=""2.0"" xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"">
                    <Schema Namespace=""NorthwindModel"" xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices"" 
                            xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"" 
                            xmlns=""http://schemas.microsoft.com/ado/2008/09/edm"">
                        <EntityType Name=""Category"">
                            <Key>
                                <PropertyRef Name=""CategoryID"" />
                            </Key>
                            <Property Name=""CategoryID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""CategoryName"" Type=""Edm.String"" Nullable=""false"" MaxLength=""15"" />
                            <Property Name=""Description"" Type=""Edm.String"" Nullable=""true"" />
                        </EntityType>
                        <EntityType Name=""Product"">
                            <Key>
                                <PropertyRef Name=""ProductID"" />
                            </Key>
                            <Property Name=""ProductID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""ProductName"" Type=""Edm.String"" Nullable=""false"" MaxLength=""40"" />
                            <Property Name=""UnitPrice"" Type=""Edm.Decimal"" Nullable=""true"" Precision=""19"" Scale=""4"" />
                        </EntityType>
                    </Schema>
                    <Schema Namespace=""ODataWeb.Northwind.Model"" xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices"" 
                            xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"" 
                            xmlns=""http://schemas.microsoft.com/ado/2008/09/edm"">
                        <EntityContainer Name=""NorthwindEntities"" m:IsDefaultEntityContainer=""true"">
                            <EntitySet Name=""Categories"" EntityType=""NorthwindModel.Category"" />
                            <EntitySet Name=""Products"" EntityType=""NorthwindModel.Product"" />
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(v2Metadata);

        // Act
        var model = await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object);

        // Assert
        model.Should().NotBeNull();
        model.EntityContainer.Should().NotBeNull();
        
        var entitySets = model.EntityContainer.EntitySets().ToList();
        entitySets.Should().HaveCountGreaterThanOrEqualTo(2); // At least Categories and Products
        entitySets.Should().Contain(es => es.Name == "Categories" || es.Name == "Category");
        entitySets.Should().Contain(es => es.Name == "Products" || es.Name == "Product");
    }

    [Fact]
    public async Task Should_Handle_Invalid_Xml()
    {
        // Arrange
        var invalidXml = "<invalid>This is not valid EDMX</invalid>";
        SetupMetadataResponse(invalidXml);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object)
        );
    }

    [Fact]
    public async Task Should_Handle_Empty_Metadata()
    {
        // Arrange
        var emptyMetadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""Empty"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityContainer Name=""EmptyContainer"" />
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(emptyMetadata);

        // Act
        var model = await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object);

        // Assert
        model.Should().NotBeNull();
        model.EntityContainer.Should().NotBeNull();
        model.EntityContainer.EntitySets().Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Handle_Http_Errors()
    {
        // Arrange
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized,
                Content = new StringContent("Unauthorized")
            });

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object)
        );
    }

    [Fact]
    public async Task Should_Include_Authorization_Header_When_Credentials_Provided()
    {
        // Arrange
        var metadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""Test"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <EntityContainer Name=""Container"" />
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        HttpRequestMessage? capturedRequest = null;
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(metadata)
            });

        // Act
        await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", "testuser", "testpass", _logger.Object);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        
        // Decode and verify credentials
        var authBytes = Convert.FromBase64String(capturedRequest.Headers.Authorization.Parameter!);
        var authString = System.Text.Encoding.UTF8.GetString(authBytes);
        authString.Should().Be("testuser:testpass");
    }

    [Fact]
    public async Task Should_Parse_Complex_Types_And_Associations()
    {
        // Arrange
        var complexMetadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
                <edmx:DataServices>
                    <Schema Namespace=""TestNamespace"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
                        <ComplexType Name=""Address"">
                            <Property Name=""Street"" Type=""Edm.String"" />
                            <Property Name=""City"" Type=""Edm.String"" />
                            <Property Name=""PostalCode"" Type=""Edm.String"" />
                        </ComplexType>
                        <EntityType Name=""Customer"">
                            <Key>
                                <PropertyRef Name=""ID"" />
                            </Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""Name"" Type=""Edm.String"" />
                            <Property Name=""Address"" Type=""TestNamespace.Address"" />
                            <NavigationProperty Name=""Orders"" Type=""Collection(TestNamespace.Order)"" />
                        </EntityType>
                        <EntityType Name=""Order"">
                            <Key>
                                <PropertyRef Name=""ID"" />
                            </Key>
                            <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
                            <Property Name=""OrderDate"" Type=""Edm.DateTimeOffset"" />
                            <NavigationProperty Name=""Customer"" Type=""TestNamespace.Customer"" />
                        </EntityType>
                        <EntityContainer Name=""TestContainer"">
                            <EntitySet Name=""Customers"" EntityType=""TestNamespace.Customer"">
                                <NavigationPropertyBinding Path=""Orders"" Target=""Orders"" />
                            </EntitySet>
                            <EntitySet Name=""Orders"" EntityType=""TestNamespace.Order"">
                                <NavigationPropertyBinding Path=""Customer"" Target=""Customers"" />
                            </EntitySet>
                        </EntityContainer>
                    </Schema>
                </edmx:DataServices>
            </edmx:Edmx>";

        SetupMetadataResponse(complexMetadata);

        // Act
        var model = await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object);

        // Assert
        model.Should().NotBeNull();
        
        // Check complex type
        var complexTypes = model.SchemaElements.OfType<IEdmComplexType>().ToList();
        complexTypes.Should().HaveCount(1);
        complexTypes[0].Name.Should().Be("Address");
        
        // Check navigation properties
        var customerType = model.SchemaElements.OfType<IEdmEntityType>().FirstOrDefault(t => t.FullName() == "TestNamespace.Customer");
        customerType.Should().NotBeNull();
        customerType!.NavigationProperties().Should().HaveCount(1);
        customerType.NavigationProperties().First().Name.Should().Be("Orders");
    }

    [Fact]
    public async Task Should_Log_Parsing_Attempts_And_Failures()
    {
        // Arrange
        var problematicMetadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <root>Invalid metadata format</root>";

        SetupMetadataResponse(problematicMetadata);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object)
        );

        // Verify logging
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("parsing failed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(metadataXml)
            });
    }
}