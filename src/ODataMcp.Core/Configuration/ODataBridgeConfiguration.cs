namespace ODataMcp.Core.Configuration;

/// <summary>
/// Configuration for the OData MCP bridge
/// </summary>
public class ODataBridgeConfiguration
{
    /// <summary>
    /// OData service URL
    /// </summary>
    public string ServiceUrl { get; set; } = "";

    /// <summary>
    /// Username for basic authentication
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for basic authentication
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Cookie file for authentication
    /// </summary>
    public string? CookieFile { get; set; }

    /// <summary>
    /// Cookie string for authentication
    /// </summary>
    public string? CookieString { get; set; }

    /// <summary>
    /// Enable tool name shortening
    /// </summary>
    public bool ToolShrink { get; set; }

    /// <summary>
    /// List of entities to include (supports wildcards)
    /// </summary>
    public List<string>? Entities { get; set; }

    /// <summary>
    /// Enable read-only mode
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Enable read-only mode but allow functions
    /// </summary>
    public bool ReadOnlyButFunctions { get; set; }

    /// <summary>
    /// Enable verbose logging
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Enable trace mode
    /// </summary>
    public bool Trace { get; set; }

    /// <summary>
    /// Enable MCP trace logging
    /// </summary>
    public bool TraceMcp { get; set; }

    /// <summary>
    /// Enable Claude Code friendly mode
    /// </summary>
    public bool ClaudeCodeFriendly { get; set; }

    /// <summary>
    /// Maximum items to return in a single response
    /// </summary>
    public int MaxItems { get; set; } = 100;

    /// <summary>
    /// Enable pagination hints
    /// </summary>
    public bool PaginationHints { get; set; }

    /// <summary>
    /// Use legacy date format
    /// </summary>
    public bool LegacyDates { get; set; }

    /// <summary>
    /// Enable verbose error messages
    /// </summary>
    public bool VerboseErrors { get; set; }

    /// <summary>
    /// Operations to enable (C=create, S=search, F=filter, G=get, U=update, D=delete, A=action)
    /// </summary>
    public string? EnableOps { get; set; }

    /// <summary>
    /// Operations to disable (C=create, S=search, F=filter, G=get, U=update, D=delete, A=action)
    /// </summary>
    public string? DisableOps { get; set; }

    /// <summary>
    /// Path to hints file
    /// </summary>
    public string? HintsFile { get; set; }

    /// <summary>
    /// Direct hint to inject
    /// </summary>
    public string? Hint { get; set; }
}