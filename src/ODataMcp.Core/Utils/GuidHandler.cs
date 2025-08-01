using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ODataMcp.Core.Utils;

/// <summary>
/// Handles GUID optimization between base64 and standard formats
/// </summary>
public class GuidHandler : IGuidHandler
{
    private readonly ILogger<GuidHandler> _logger;
    
    public GuidHandler(ILogger<GuidHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public object OptimizeGuids(object data)
    {
        if (data == null)
            return null!;
            
        try
        {
            // Convert to JsonElement for easier processing
            var json = JsonSerializer.Serialize(data);
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            var optimized = OptimizeGuidsInElement(element);
            
            // Convert back to object
            return JsonSerializer.Deserialize<object>(optimized.GetRawText())!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing GUIDs");
            return data;
        }
    }

    public string? ConvertToBase64(string guidString)
    {
        try
        {
            if (Guid.TryParse(guidString, out var guid))
            {
                return Convert.ToBase64String(guid.ToByteArray());
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to convert GUID to base64: {Guid}", guidString);
        }
        
        return null;
    }

    public string? ConvertFromBase64(string base64String)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64String);
            if (bytes.Length == 16)
            {
                var guid = new Guid(bytes);
                return guid.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to convert base64 to GUID: {Base64}", base64String);
        }
        
        return null;
    }

    private JsonElement OptimizeGuidsInElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new Dictionary<string, object?>();
                
                // Check if this is a GUID structure
                if (IsGuidStructure(element))
                {
                    // Convert GUID structure to base64 string
                    // TODO: Implement GUID structure detection and conversion
                    return element;
                }
                
                // Recursively process properties
                foreach (var prop in element.EnumerateObject())
                {
                    obj[prop.Name] = OptimizeGuidsInElement(prop.Value);
                }
                
                return JsonSerializer.SerializeToElement(obj);
                
            case JsonValueKind.Array:
                var array = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    array.Add(OptimizeGuidsInElement(item));
                }
                return JsonSerializer.SerializeToElement(array);
                
            case JsonValueKind.String:
                var str = element.GetString();
                if (!string.IsNullOrEmpty(str))
                {
                    // Try to convert base64 GUID to standard format
                    var converted = ConvertFromBase64(str);
                    if (converted != null)
                    {
                        return JsonSerializer.SerializeToElement(converted);
                    }
                }
                return element;
                
            default:
                return element;
        }
    }

    private bool IsGuidStructure(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;
            
        // Check for __metadata.type ending with "Edm.Guid"
        if (element.TryGetProperty("__metadata", out var metadata) &&
            metadata.ValueKind == JsonValueKind.Object &&
            metadata.TryGetProperty("type", out var typeElement) &&
            typeElement.ValueKind == JsonValueKind.String)
        {
            var type = typeElement.GetString();
            return type?.EndsWith("Edm.Guid", StringComparison.OrdinalIgnoreCase) == true;
        }
        
        return false;
    }
}