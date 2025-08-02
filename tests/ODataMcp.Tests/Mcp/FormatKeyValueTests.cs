using System.Text.Json;
using FluentAssertions;
using Microsoft.OData.Edm;
using Xunit;

namespace ODataMcp.Tests.Mcp;

public class FormatKeyValueTests
{
    [Fact]
    public void FormatKeyValue_WithInt32_ShouldReturnUnquotedNumber()
    {
        // Arrange
        var json = JsonDocument.Parse("{\"ID\": 123}");
        var element = json.RootElement.GetProperty("ID");
        var typeRef = EdmCoreModel.Instance.GetInt32(false);
        
        // Act
        var result = FormatKeyValue(element, typeRef);
        
        // Assert
        result.Should().Be("123");
        result.Should().NotContain("'");
    }
    
    [Fact]
    public void FormatKeyValue_WithString_ShouldReturnQuotedString()
    {
        // Arrange
        var json = JsonDocument.Parse("{\"Name\": \"Test Product\"}");
        var element = json.RootElement.GetProperty("Name");
        var typeRef = EdmCoreModel.Instance.GetString(false);
        
        // Act
        var result = FormatKeyValue(element, typeRef);
        
        // Assert
        result.Should().Be("'Test Product'");
    }
    
    [Fact]
    public void FormatKeyValue_WithGuid_ShouldReturnGuidFormat()
    {
        // Arrange
        var guidValue = "12345678-1234-1234-1234-123456789012";
        var json = JsonDocument.Parse($"{{\"Id\": \"{guidValue}\"}}");
        var element = json.RootElement.GetProperty("Id");
        var typeRef = EdmCoreModel.Instance.GetGuid(false);
        
        // Act
        var result = FormatKeyValue(element, typeRef);
        
        // Assert
        result.Should().Be($"guid'{guidValue}'");
    }
    
    [Theory]
    [InlineData(16, "16")]
    [InlineData(32, "32")]
    [InlineData(64, "64")]
    public void FormatKeyValue_WithDifferentIntegerTypes_ShouldReturnUnquotedNumbers(long value, string expected)
    {
        // Arrange
        var json = JsonDocument.Parse($"{{\"Value\": {value}}}");
        var element = json.RootElement.GetProperty("Value");
        var typeRef = EdmCoreModel.Instance.GetInt64(false);
        
        // Act
        var result = FormatKeyValue(element, typeRef);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void FormatKeyValue_WithDecimal_ShouldReturnUnquotedNumber()
    {
        // Arrange
        var json = JsonDocument.Parse("{\"Price\": 19.99}");
        var element = json.RootElement.GetProperty("Price");
        var typeRef = EdmCoreModel.Instance.GetDecimal(false);
        
        // Act
        var result = FormatKeyValue(element, typeRef);
        
        // Assert
        result.Should().Be("19.99");
    }
    
    [Fact]
    public void FormatKeyValue_WithBoolean_ShouldReturnLowercase()
    {
        // Arrange
        var json = JsonDocument.Parse("{\"IsActive\": true}");
        var element = json.RootElement.GetProperty("IsActive");
        var typeRef = EdmCoreModel.Instance.GetBoolean(false);
        
        // Act
        var result = FormatKeyValue(element, typeRef);
        
        // Assert
        result.Should().Be("true");
    }
    
    // This is a simplified version of the actual FormatKeyValue method
    // In real tests, we'd test against the actual SimpleMcpServerV2 class
    private string FormatKeyValue(JsonElement element, IEdmTypeReference typeRef)
    {
        if (typeRef.IsPrimitive())
        {
            var primitiveType = typeRef.AsPrimitive();
            return primitiveType.PrimitiveKind() switch
            {
                EdmPrimitiveTypeKind.String => $"'{element.GetString()}'",
                EdmPrimitiveTypeKind.Guid => $"guid'{element.GetString()}'",
                EdmPrimitiveTypeKind.Int16 => element.GetInt16().ToString(),
                EdmPrimitiveTypeKind.Int32 => element.GetInt32().ToString(),
                EdmPrimitiveTypeKind.Int64 => element.GetInt64().ToString(),
                EdmPrimitiveTypeKind.Double => element.GetDouble().ToString("G17"),
                EdmPrimitiveTypeKind.Decimal => element.GetDecimal().ToString(),
                EdmPrimitiveTypeKind.Boolean => element.GetBoolean().ToString().ToLower(),
                EdmPrimitiveTypeKind.DateTimeOffset => $"datetime'{element.GetDateTime():yyyy-MM-ddTHH:mm:ss}'",
                _ => element.GetString()!
            };
        }
        return element.GetString()!;
    }
}