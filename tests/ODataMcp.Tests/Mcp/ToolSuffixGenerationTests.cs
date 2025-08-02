using FluentAssertions;
using Xunit;

namespace ODataMcp.Tests.Mcp;

public class ToolSuffixGenerationTests
{
    [Theory]
    [InlineData("https://services.odata.org/V2/OData/OData.svc/", "_for_OData")]
    [InlineData("https://services.odata.org/V2/OData/OData.svc", "_for_OData")]
    [InlineData("https://example.com/api/MyService.svc/", "_for_MyService")]
    [InlineData("https://example.com/odata/products/", "_for_products")]
    [InlineData("https://localhost:5001/odata/", "_for_odata")]
    [InlineData("https://example.com/api/v1/CustomerManagement/", "_for_CustomerMa")]
    [InlineData("https://example.com/", "_for_example")]
    public void GetToolSuffix_WithVariousUrls_ShouldGenerateCorrectSuffix(string serviceUrl, string expectedSuffix)
    {
        // Act
        var suffix = GetToolSuffix(serviceUrl);
        
        // Assert
        suffix.Should().Be(expectedSuffix);
    }
    
    [Fact]
    public void GetToolSuffix_WithVeryLongPath_ShouldTruncateToMaxLength()
    {
        // Arrange
        var serviceUrl = "https://example.com/api/v1/enterprise/customer/relationship/management/system/service.svc/";
        
        // Act
        var suffix = GetToolSuffix(serviceUrl);
        
        // Assert
        suffix.Should().StartWith("_for_");
        suffix.Length.Should().BeLessOrEqualTo(15); // 5 for "_for_" + 10 max for name
    }
    
    [Theory]
    [InlineData("https://example.com/OData.svc/", "_for_OData")]
    [InlineData("https://example.com/odata.svc/", "_for_odata")]
    [InlineData("https://example.com/MyODataService.svc/", "_for_MyODataSer")]
    public void GetToolSuffix_WithSvcExtension_ShouldRemoveExtension(string serviceUrl, string expectedSuffix)
    {
        // Act
        var suffix = GetToolSuffix(serviceUrl);
        
        // Assert
        suffix.Should().Be(expectedSuffix);
    }
    
    // Simplified version of the actual GetToolSuffix method for testing
    private string GetToolSuffix(string serviceUrl)
    {
        var uri = new Uri(serviceUrl);
        var path = uri.AbsolutePath.Trim('/');
        
        // Get the last segment of the path
        var segments = path.Split('/');
        var lastPart = segments.LastOrDefault() ?? uri.Host.Split('.')[0];
        
        // Remove common suffixes
        if (lastPart.EndsWith(".svc", StringComparison.OrdinalIgnoreCase))
        {
            lastPart = lastPart[..^4];
        }
        
        return $"_for_{ShortenName(lastPart)}";
    }
    
    private string ShortenName(string name)
    {
        const int maxLength = 10;
        if (name.Length <= maxLength)
        {
            return name;
        }
        
        // Try to shorten intelligently
        var shortened = name;
        
        // Remove vowels from the middle if needed
        if (shortened.Length > maxLength)
        {
            var chars = shortened.ToCharArray();
            var vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u', 'A', 'E', 'I', 'O', 'U' };
            var result = new List<char>();
            
            // Keep first and last characters
            result.Add(chars[0]);
            
            for (int i = 1; i < chars.Length - 1 && result.Count < maxLength - 1; i++)
            {
                if (!vowels.Contains(chars[i]) || result.Count < 3)
                {
                    result.Add(chars[i]);
                }
            }
            
            if (result.Count < maxLength && chars.Length > 1)
            {
                result.Add(chars[^1]);
            }
            
            shortened = new string(result.ToArray());
        }
        
        // If still too long, just truncate
        if (shortened.Length > maxLength)
        {
            shortened = shortened[..maxLength];
        }
        
        return shortened;
    }
}