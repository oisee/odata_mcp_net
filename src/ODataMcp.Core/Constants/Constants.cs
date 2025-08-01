namespace ODataMcp.Core.Constants;

/// <summary>
/// Constants used throughout the OData MCP bridge
/// </summary>
public static class Constants
{
    public const string McpServerName = "odata-mcp";
    public const string McpServerVersion = "1.0.0";
    public const string McpProtocolVersion = "2024-11-05";
    
    // OData constants
    public const string MetadataEndpoint = "$metadata";
    public const string CountParameter = "$count";
    public const string FilterParameter = "$filter";
    public const string SelectParameter = "$select";
    public const string ExpandParameter = "$expand";
    public const string OrderByParameter = "$orderby";
    public const string TopParameter = "$top";
    public const string SkipParameter = "$skip";
    public const string InlineCountParameter = "$inlinecount";
    public const string FormatParameter = "$format";
    
    // OData v4 specific
    public const string CountParameterV4 = "$count";
    public const string SearchParameter = "$search";
    
    // HTTP headers
    public const string CsrfTokenHeader = "X-CSRF-Token";
    public const string CsrfTokenFetch = "Fetch";
    public const string ContentTypeHeader = "Content-Type";
    public const string AcceptHeader = "Accept";
    public const string AuthorizationHeader = "Authorization";
    
    // Content types
    public const string JsonContentType = "application/json";
    public const string XmlContentType = "application/xml";
    public const string AtomXmlContentType = "application/atom+xml";
    
    // Tool naming
    public const string DefaultToolPostfixFormat = "_for_{0}";
    public const string ServiceInfoToolName = "odata_service_info";
    
    // Tool prefixes
    public const string FilterToolPrefix = "filter_";
    public const string GetToolPrefix = "get_";
    public const string CreateToolPrefix = "create_";
    public const string UpdateToolPrefix = "update_";
    public const string DeleteToolPrefix = "delete_";
    public const string SearchToolPrefix = "search_";
    public const string CountToolPrefix = "count_";
    
    // Shortened tool prefixes
    public const string CreateToolPrefixShort = "create_";
    public const string UpdateToolPrefixShort = "upd_";
    public const string DeleteToolPrefixShort = "del_";
    
    // Response limits
    public const int DefaultMaxResponseSize = 5 * 1024 * 1024; // 5MB
    public const int DefaultMaxItems = 100;
    public const int MaxFieldLength = 50000;
    
    // Date formats
    public const string LegacyDatePattern = @"\/Date\((-?\d+)\)\/";
    public const string Iso8601DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK";
    
    // Error codes
    public const int JsonRpcParseError = -32700;
    public const int JsonRpcInvalidRequest = -32600;
    public const int JsonRpcMethodNotFound = -32601;
    public const int JsonRpcInvalidParams = -32602;
    public const int JsonRpcInternalError = -32603;
    
    // Operation types
    public const char OpCreate = 'C';
    public const char OpSearch = 'S';
    public const char OpFilter = 'F';
    public const char OpGet = 'G';
    public const char OpUpdate = 'U';
    public const char OpDelete = 'D';
    public const char OpAction = 'A';
    public const char OpRead = 'R'; // Expands to S, F, G
}