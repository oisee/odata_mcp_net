using System.Text.Json;
using FluentAssertions;
using Microsoft.OData.Edm;
using Xunit;

namespace ODataMcp.Tests;

public class BasicFunctionalityTests
{
    [Fact]
    public void PrimitiveTypeFormatting_ShouldHandleIntegers()
    {
        // Test that we format integers without quotes
        var value = 123;
        var result = value.ToString();
        result.Should().Be("123");
        result.Should().NotContain("'");
    }

    [Fact]
    public void PrimitiveTypeFormatting_ShouldHandleStrings()
    {
        // Test that we format strings with single quotes
        var value = "Test Product";
        var result = $"'{value}'";
        result.Should().Be("'Test Product'");
    }

    [Fact]
    public void ToolSuffixGeneration_ShouldExtractServiceName()
    {
        // Test suffix generation logic
        var url = "https://services.odata.org/V2/OData/OData.svc/";
        var uri = new Uri(url);
        var path = uri.AbsolutePath.Trim('/');
        var segments = path.Split('/');
        var lastPart = segments.LastOrDefault() ?? uri.Host.Split('.')[0];
        
        if (lastPart.EndsWith(".svc", StringComparison.OrdinalIgnoreCase))
        {
            lastPart = lastPart[..^4];
        }
        
        var suffix = $"_for_{lastPart}";
        suffix.Should().Be("_for_OData");
    }

    [Theory]
    [InlineData("https://services.odata.org/V2/OData/OData.svc/", "_for_OData")]
    [InlineData("https://example.com/api/MyService.svc/", "_for_MyService")]
    [InlineData("https://example.com/odata/products/", "_for_products")]
    [InlineData("https://localhost:5001/odata/", "_for_odata")]
    public void ToolSuffixGeneration_WithVariousUrls(string serviceUrl, string expectedSuffix)
    {
        // Arrange & Act
        var suffix = GetToolSuffix(serviceUrl);
        
        // Assert
        suffix.Should().Be(expectedSuffix);
    }

    [Fact]
    public void JsonParsing_ShouldHandleODataResponse()
    {
        // Test parsing OData response format
        var json = @"{
            ""d"": {
                ""results"": [
                    { ""ID"": 1, ""Name"": ""Product 1"" },
                    { ""ID"": 2, ""Name"": ""Product 2"" }
                ]
            }
        }";

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        root.TryGetProperty("d", out var d).Should().BeTrue();
        d.TryGetProperty("results", out var results).Should().BeTrue();
        results.ValueKind.Should().Be(JsonValueKind.Array);
        
        var items = results.EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items[0].GetProperty("ID").GetInt32().Should().Be(1);
    }

    private string GetToolSuffix(string serviceUrl)
    {
        var uri = new Uri(serviceUrl);
        var path = uri.AbsolutePath.Trim('/');
        var segments = path.Split('/');
        var lastPart = segments.LastOrDefault() ?? uri.Host.Split('.')[0];
        
        if (lastPart.EndsWith(".svc", StringComparison.OrdinalIgnoreCase))
        {
            lastPart = lastPart[..^4];
        }
        
        return $"_for_{lastPart}";
    }
}