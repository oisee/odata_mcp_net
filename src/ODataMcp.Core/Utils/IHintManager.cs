namespace ODataMcp.Core.Utils;

/// <summary>
/// Interface for managing service hints
/// </summary>
public interface IHintManager
{
    /// <summary>
    /// Load hints from a file
    /// </summary>
    Task LoadFromFileAsync(string? filePath);
    
    /// <summary>
    /// Set a hint from command line
    /// </summary>
    void SetCliHint(string hint);
    
    /// <summary>
    /// Get hints for a service URL
    /// </summary>
    ServiceHints? GetHintsForService(string serviceUrl);
}

/// <summary>
/// Service hints
/// </summary>
public class ServiceHints
{
    public string? ServiceType { get; set; }
    public List<string>? KnownIssues { get; set; }
    public List<string>? Workarounds { get; set; }
    public Dictionary<string, FieldHint>? FieldHints { get; set; }
    public List<ExampleQuery>? Examples { get; set; }
    public string? Notes { get; set; }
    public string HintSource { get; set; } = "Default";
}

/// <summary>
/// Field hint
/// </summary>
public class FieldHint
{
    public string? Type { get; set; }
    public string? Format { get; set; }
    public string? Example { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Example query
/// </summary>
public class ExampleQuery
{
    public string? Description { get; set; }
    public string? Query { get; set; }
    public string? Note { get; set; }
}