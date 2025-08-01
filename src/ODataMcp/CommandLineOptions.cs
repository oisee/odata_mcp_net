using CommandLine;

namespace ODataMcp;

/// <summary>
/// Command line options for the OData MCP bridge
/// </summary>
public class CommandLineOptions
{
    [Value(0, MetaName = "service-url", HelpText = "OData service URL")]
    public string? PositionalServiceUrl { get; set; }

    [Option("service", HelpText = "URL of the OData service (overrides positional argument and ODATA_SERVICE_URL env var)")]
    public string? ServiceUrl { get; set; }

    [Option('u', "user", HelpText = "Username for basic authentication (overrides ODATA_USERNAME env var)")]
    public string? Username { get; set; }

    [Option('p', "password", HelpText = "Password for basic authentication (overrides ODATA_PASSWORD env var)")]
    public string? Password { get; set; }

    [Option("pass", Hidden = true, HelpText = "Password for basic authentication (alias for --password)")]
    public string? PasswordAlias { get; set; }

    [Option("cookie-file", HelpText = "Path to cookie file in Netscape format")]
    public string? CookieFile { get; set; }

    [Option("cookie-string", HelpText = "Cookie string (key1=val1; key2=val2)")]
    public string? CookieString { get; set; }

    [Option("tool-prefix", HelpText = "Custom prefix for tool names (use with --no-postfix)")]
    public string? ToolPrefix { get; set; }

    [Option("tool-postfix", HelpText = "Custom postfix for tool names (default: _for_<service_id>)")]
    public string? ToolPostfix { get; set; }

    [Option("no-postfix", Default = false, HelpText = "Use prefix instead of postfix for tool naming")]
    public bool NoPostfix { get; set; }

    [Option("tool-shrink", Default = false, HelpText = "Use shortened tool names (create_, get_, upd_, del_, search_, filter_)")]
    public bool ToolShrink { get; set; }

    [Option("entities", HelpText = "Comma-separated list of entities to generate tools for (e.g., 'Products,Categories,Orders'). Supports wildcards: 'Product*,Order*'")]
    public string? Entities { get; set; }

    [Option("functions", HelpText = "Comma-separated list of function imports to generate tools for (e.g., 'GetProducts,CreateOrder'). Supports wildcards: 'Get*,Create*'")]
    public string? Functions { get; set; }

    [Option('v', "verbose", Default = false, HelpText = "Enable verbose output to stderr")]
    public bool Verbose { get; set; }

    [Option("debug", Hidden = true, Default = false, HelpText = "Alias for --verbose")]
    public bool Debug { get; set; }

    [Option("sort-tools", Default = true, HelpText = "Sort tools alphabetically in the output")]
    public bool SortTools { get; set; }

    [Option("trace", Default = false, HelpText = "Initialize MCP service and print all tools and parameters, then exit (useful for debugging)")]
    public bool Trace { get; set; }

    [Option("pagination-hints", Default = false, HelpText = "Add pagination support with suggested_next_call and has_more indicators")]
    public bool PaginationHints { get; set; }

    [Option("legacy-dates", Default = true, HelpText = "Support epoch timestamp format (/Date(1234567890000)/) - enabled by default for SAP")]
    public bool LegacyDates { get; set; }

    [Option("no-legacy-dates", Default = false, HelpText = "Disable legacy date format conversion")]
    public bool NoLegacyDates { get; set; }

    [Option("verbose-errors", Default = false, HelpText = "Provide detailed error context and debugging information")]
    public bool VerboseErrors { get; set; }

    [Option("response-metadata", Default = false, HelpText = "Include detailed __metadata blocks in entity responses")]
    public bool ResponseMetadata { get; set; }

    [Option("max-response-size", Default = 5 * 1024 * 1024, HelpText = "Maximum response size in bytes (default: 5MB)")]
    public int MaxResponseSize { get; set; }

    [Option("max-items", Default = 100, HelpText = "Maximum number of items in response (default: 100)")]
    public int MaxItems { get; set; }

    [Option("read-only", Default = false, HelpText = "Read-only mode: hide all modifying operations (create, update, delete, and functions)")]
    public bool ReadOnly { get; set; }

    [Option("ro", Hidden = true, Default = false, HelpText = "Read-only mode (shorthand for --read-only)")]
    public bool ReadOnlyShort { get; set; }

    [Option("read-only-but-functions", Default = false, HelpText = "Read-only mode but allow function imports")]
    public bool ReadOnlyButFunctions { get; set; }

    [Option("robf", Hidden = true, Default = false, HelpText = "Read-only but functions (shorthand for --read-only-but-functions)")]
    public bool ReadOnlyButFunctionsShort { get; set; }

    [Option("transport", Default = "stdio", HelpText = "Transport type: 'stdio' or 'http' (SSE)")]
    public string Transport { get; set; } = "stdio";

    [Option("http-addr", Default = "localhost:8080", HelpText = "HTTP server address (used with --transport http, defaults to localhost only for security)")]
    public string HttpAddress { get; set; } = "localhost:8080";

    [Option("i-am-security-expert-i-know-what-i-am-doing", Default = false, HelpText = "DANGEROUS: Allow non-localhost HTTP transport. MCP has no authentication!")]
    public bool SecurityExpertMode { get; set; }

    [Option("trace-mcp", Default = false, HelpText = "Enable trace logging to debug MCP communication")]
    public bool TraceMcp { get; set; }

    [Option("hints-file", HelpText = "Path to hints JSON file (defaults to hints.json in same directory as binary)")]
    public string? HintsFile { get; set; }

    [Option("hint", HelpText = "Direct hint JSON or text to inject into service info")]
    public string? Hint { get; set; }

    [Option("enable", HelpText = "Enable only specified operation types (C=create, S=search, F=filter, G=get, U=update, D=delete, A=action, R=read expands to SFG)")]
    public string? EnableOps { get; set; }

    [Option("disable", HelpText = "Disable specified operation types (C=create, S=search, F=filter, G=get, U=update, D=delete, A=action, R=read expands to SFG)")]
    public string? DisableOps { get; set; }

    [Option('c', "claude-code-friendly", Default = false, HelpText = "Remove $ prefix from OData parameters for Claude Code CLI compatibility")]
    public bool ClaudeCodeFriendly { get; set; }
}