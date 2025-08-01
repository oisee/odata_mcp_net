namespace ODataMcp.Core.Configuration;

/// <summary>
/// Configuration for the OData MCP bridge
/// </summary>
public class ODataMcpConfig
{
    public string? ServiceUrl { get; set; }
    
    // Authentication
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? CookieFile { get; set; }
    public string? CookieString { get; set; }
    
    // Tool naming
    public string? ToolPrefix { get; set; }
    public string? ToolPostfix { get; set; }
    public bool NoPostfix { get; set; }
    public bool ToolShrink { get; set; }
    public bool SortTools { get; set; } = true;
    
    // Filtering
    public string[]? Entities { get; set; }
    public string[]? Functions { get; set; }
    
    // Operation modes
    public bool ReadOnly { get; set; }
    public bool ReadOnlyButFunctions { get; set; }
    public string? EnableOps { get; set; }
    public string? DisableOps { get; set; }
    
    // Transport
    public string Transport { get; set; } = "stdio";
    public string HttpAddress { get; set; } = "localhost:8080";
    public bool SecurityExpertMode { get; set; }
    
    // Response options
    public bool PaginationHints { get; set; }
    public bool LegacyDates { get; set; } = true;
    public bool VerboseErrors { get; set; }
    public bool ResponseMetadata { get; set; }
    public int MaxResponseSize { get; set; } = 5 * 1024 * 1024;
    public int MaxItems { get; set; } = 100;
    
    // Other options
    public bool Verbose { get; set; }
    public bool Trace { get; set; }
    public bool TraceMcp { get; set; }
    public string? HintsFile { get; set; }
    public string? Hint { get; set; }
    public bool ClaudeCodeFriendly { get; set; }
    
    /// <summary>
    /// Check if the configuration has basic authentication
    /// </summary>
    public bool HasBasicAuth => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
    
    /// <summary>
    /// Check if the configuration has cookie authentication
    /// </summary>
    public bool HasCookieAuth => !string.IsNullOrEmpty(CookieFile) || !string.IsNullOrEmpty(CookieString);
    
    /// <summary>
    /// Check if the configuration is in read-only mode
    /// </summary>
    public bool IsReadOnly => ReadOnly || ReadOnlyButFunctions;
    
    /// <summary>
    /// Check if modifying functions are allowed
    /// </summary>
    public bool AllowModifyingFunctions => !ReadOnly && (ReadOnlyButFunctions || !IsReadOnly);
    
    /// <summary>
    /// Check if an operation type is enabled
    /// </summary>
    public bool IsOperationEnabled(char operationType)
    {
        operationType = char.ToUpper(operationType);
        
        // Handle read-only modes
        if (ReadOnly)
        {
            return operationType switch
            {
                'S' or 'F' or 'G' => true,
                _ => false
            };
        }
        
        if (ReadOnlyButFunctions)
        {
            return operationType switch
            {
                'S' or 'F' or 'G' or 'A' => true,
                _ => false
            };
        }
        
        // Handle enable/disable flags
        if (!string.IsNullOrEmpty(EnableOps))
        {
            var ops = EnableOps.ToUpper();
            // Expand 'R' to 'SFG'
            if (ops.Contains('R'))
            {
                ops = ops.Replace("R", "SFG");
            }
            return ops.Contains(operationType);
        }
        
        if (!string.IsNullOrEmpty(DisableOps))
        {
            var ops = DisableOps.ToUpper();
            // Expand 'R' to 'SFG'
            if (ops.Contains('R'))
            {
                ops = ops.Replace("R", "SFG");
            }
            return !ops.Contains(operationType);
        }
        
        // Default: all operations enabled
        return true;
    }
}

/// <summary>
/// Result of configuration validation
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }
    
    private ConfigurationValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
    
    public static ConfigurationValidationResult Success() => new(true);
    public static ConfigurationValidationResult Error(string message) => new(false, message);
}