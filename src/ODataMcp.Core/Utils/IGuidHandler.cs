namespace ODataMcp.Core.Utils;

/// <summary>
/// Interface for GUID optimization
/// </summary>
public interface IGuidHandler
{
    /// <summary>
    /// Optimize GUIDs in the response data
    /// </summary>
    object OptimizeGuids(object data);
    
    /// <summary>
    /// Convert GUID string to base64
    /// </summary>
    string? ConvertToBase64(string guidString);
    
    /// <summary>
    /// Convert base64 to GUID string
    /// </summary>
    string? ConvertFromBase64(string base64String);
}