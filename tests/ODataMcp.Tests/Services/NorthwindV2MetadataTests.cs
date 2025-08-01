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
/// Specific tests for Northwind V2 metadata parsing issue
/// </summary>
public class NorthwindV2MetadataTests
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger> _logger;

    public NorthwindV2MetadataTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);
        _logger = new Mock<ILogger>();
    }

    [Fact(Skip = "Known issue with Microsoft.OData.Edm.Csdl.CsdlReader parsing V2 metadata")]
    public async Task Should_Parse_Actual_Northwind_V2_Metadata()
    {
        // Arrange - This is a simplified version of actual Northwind V2 metadata
        // The actual metadata has issues with CsdlReader.TryParse throwing "Value cannot be null. (Parameter 'key')"
        var northwindV2Metadata = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<edmx:Edmx Version=""1.0"" xmlns:edmx=""http://schemas.microsoft.com/ado/2007/06/edmx"">
  <edmx:DataServices xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"" m:DataServiceVersion=""2.0"">
    <Schema Namespace=""NorthwindModel"" xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices"" xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"" xmlns=""http://schemas.microsoft.com/ado/2008/09/edm"">
      <EntityType Name=""Category"">
        <Key>
          <PropertyRef Name=""CategoryID"" />
        </Key>
        <Property Name=""CategoryID"" Type=""Edm.Int32"" Nullable=""false"" p8:StoreGeneratedPattern=""Identity"" xmlns:p8=""http://schemas.microsoft.com/ado/2009/02/edm/annotation"" />
        <Property Name=""CategoryName"" Type=""Edm.String"" Nullable=""false"" MaxLength=""15"" Unicode=""true"" FixedLength=""false"" />
        <Property Name=""Description"" Type=""Edm.String"" Nullable=""true"" MaxLength=""Max"" Unicode=""true"" FixedLength=""false"" />
        <Property Name=""Picture"" Type=""Edm.Binary"" Nullable=""true"" MaxLength=""Max"" FixedLength=""false"" />
        <NavigationProperty Name=""Products"" Relationship=""NorthwindModel.FK_Products_Categories"" FromRole=""Categories"" ToRole=""Products"" />
      </EntityType>
      <Association Name=""FK_Products_Categories"">
        <End Role=""Categories"" Type=""NorthwindModel.Category"" Multiplicity=""0..1"" />
        <End Role=""Products"" Type=""NorthwindModel.Product"" Multiplicity=""*"" />
        <ReferentialConstraint>
          <Principal Role=""Categories"">
            <PropertyRef Name=""CategoryID"" />
          </Principal>
          <Dependent Role=""Products"">
            <PropertyRef Name=""CategoryID"" />
          </Dependent>
        </ReferentialConstraint>
      </Association>
    </Schema>
    <Schema Namespace=""ODataWeb.Northwind.Model"" xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices"" xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"" xmlns=""http://schemas.microsoft.com/ado/2008/09/edm"">
      <EntityContainer Name=""NorthwindEntities"" p7:LazyLoadingEnabled=""true"" m:IsDefaultEntityContainer=""true"" xmlns:p7=""http://schemas.microsoft.com/ado/2009/02/edm/annotation"">
        <EntitySet Name=""Categories"" EntityType=""NorthwindModel.Category"" />
      </EntityContainer>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";

        SetupMetadataResponse(northwindV2Metadata);

        // Act & Assert
        // This test documents the known issue with parsing V2 metadata
        // The Microsoft.OData.Edm.Csdl.CsdlReader fails with "Value cannot be null. (Parameter 'key')"
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://services.odata.org/V2/Northwind/Northwind.svc/", null, null, _logger.Object)
        );

        exception.Message.Should().Contain("Failed to parse OData metadata");
    }

    [Fact]
    public async Task Should_Create_Fallback_Model_When_Parsing_Fails()
    {
        // Arrange - Metadata that will fail parsing but has extractable entity sets
        var problematicMetadata = @"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<edmx:Edmx Version=""1.0"" xmlns:edmx=""http://schemas.microsoft.com/ado/2007/06/edmx"">
  <edmx:DataServices xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"" m:DataServiceVersion=""2.0"">
    <Schema Namespace=""TestModel"" xmlns=""http://schemas.microsoft.com/ado/2008/09/edm"">
      <!-- This will cause parsing to fail due to unsupported attributes -->
      <EntityType Name=""Product"" m:HasStream=""true"">
        <Key>
          <PropertyRef Name=""ID"" />
        </Key>
        <Property Name=""ID"" Type=""Edm.Int32"" Nullable=""false"" />
      </EntityType>
    </Schema>
    <Schema Namespace=""Container"" xmlns=""http://schemas.microsoft.com/ado/2008/09/edm"">
      <EntityContainer Name=""TestContainer"">
        <EntitySet Name=""Products"" EntityType=""TestModel.Product"" />
        <EntitySet Name=""Categories"" EntityType=""TestModel.Category"" />
        <EntitySet Name=""Orders"" EntityType=""TestModel.Order"" />
      </EntityContainer>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";

        SetupMetadataResponse(problematicMetadata);

        // Act
        var model = await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object);

        // Assert - Should create fallback model with entity sets
        model.Should().NotBeNull();
        model.EntityContainer.Should().NotBeNull();
        
        var entitySets = model.EntityContainer.Elements.OfType<IEdmEntitySet>().ToList();
        entitySets.Should().HaveCount(3);
        entitySets.Should().Contain(es => es.Name == "Products");
        entitySets.Should().Contain(es => es.Name == "Categories");
        entitySets.Should().Contain(es => es.Name == "Orders");

        // Each entity should have a generic ID property
        foreach (var entitySet in entitySets)
        {
            var entityType = entitySet.EntityType;
            entityType.Should().NotBeNull();
            
            var keyProperties = entityType.Key().ToList();
            keyProperties.Should().HaveCount(1);
            keyProperties[0].Name.Should().Be("ID");
        }
    }

    [Fact]
    public async Task Should_Log_All_Parsing_Attempts()
    {
        // Arrange
        var metadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <invalid>This will fail all parsing attempts</invalid>";

        SetupMetadataResponse(metadata);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SimpleMetadataParser.ParseMetadataAsync(_httpClient, "https://example.com/odata", null, null, _logger.Object)
        );

        // Verify all parsing attempts were logged
        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Standard parsing failed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Lenient parsing failed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Manual parsing failed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        _logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All parsing approaches failed")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
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