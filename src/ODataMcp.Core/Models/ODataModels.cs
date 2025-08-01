using System.Text.Json.Serialization;

namespace ODataMcp.Core.Models;

/// <summary>
/// Represents OData service metadata
/// </summary>
public class ODataMetadata
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string? ODataVersion { get; set; }
    public Dictionary<string, EntityType> EntityTypes { get; set; } = new();
    public Dictionary<string, EntitySet> EntitySets { get; set; } = new();
    public Dictionary<string, FunctionImport> FunctionImports { get; set; } = new();
    public List<Association> Associations { get; set; } = new();
}

/// <summary>
/// Represents an OData entity type
/// </summary>
public class EntityType
{
    public string Name { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public List<Property> Properties { get; set; } = new();
    public List<NavigationProperty> NavigationProperties { get; set; } = new();
    public List<string> KeyProperties { get; set; } = new();
    public string? BaseType { get; set; }
    
    public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
}

/// <summary>
/// Represents an OData entity set
/// </summary>
public class EntitySet
{
    public string Name { get; set; } = string.Empty;
    public string EntityTypeName { get; set; } = string.Empty;
    public EntityType? EntityType { get; set; }
    
    // Capabilities
    public bool SupportsCreate { get; set; } = true;
    public bool SupportsRead { get; set; } = true;
    public bool SupportsUpdate { get; set; } = true;
    public bool SupportsDelete { get; set; } = true;
    public bool SupportsSearch { get; set; } = false;
}

/// <summary>
/// Represents a property of an entity type
/// </summary>
public class Property
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Nullable { get; set; } = true;
    public bool IsKey { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? DefaultValue { get; set; }
}

/// <summary>
/// Represents a navigation property
/// </summary>
public class NavigationProperty
{
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string ToRole { get; set; } = string.Empty;
    public string FromRole { get; set; } = string.Empty;
    public string? TargetEntityType { get; set; }
    public bool IsCollection { get; set; }
}

/// <summary>
/// Represents an association between entity types
/// </summary>
public class Association
{
    public string Name { get; set; } = string.Empty;
    public AssociationEnd End1 { get; set; } = new();
    public AssociationEnd End2 { get; set; } = new();
}

/// <summary>
/// Represents one end of an association
/// </summary>
public class AssociationEnd
{
    public string Role { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Multiplicity { get; set; } = string.Empty;
}

/// <summary>
/// Represents a function import
/// </summary>
public class FunctionImport
{
    public string Name { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public string HttpMethod { get; set; } = "GET";
    public List<FunctionParameter> Parameters { get; set; } = new();
    public bool IsBindable { get; set; }
    public bool IsSideEffecting { get; set; } = true;
}

/// <summary>
/// Represents a function parameter
/// </summary>
public class FunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Nullable { get; set; } = true;
    public string? Mode { get; set; } = "In";
}

/// <summary>
/// OData response wrapper
/// </summary>
public class ODataResponse<T>
{
    [JsonPropertyName("d")]
    public ODataResponseData<T>? Data { get; set; }
}

/// <summary>
/// OData response data
/// </summary>
public class ODataResponseData<T>
{
    [JsonPropertyName("results")]
    public List<T>? Results { get; set; }
    
    [JsonPropertyName("__count")]
    public string? Count { get; set; }
    
    [JsonPropertyName("__next")]
    public string? NextLink { get; set; }
    
    // For single entity responses
    [JsonExtensionData]
    public Dictionary<string, object?>? EntityData { get; set; }
}

/// <summary>
/// OData error response
/// </summary>
public class ODataError
{
    [JsonPropertyName("error")]
    public ODataErrorDetail? Error { get; set; }
}

/// <summary>
/// OData error detail
/// </summary>
public class ODataErrorDetail
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    
    [JsonPropertyName("message")]
    public ODataErrorMessage? Message { get; set; }
    
    // For OData v4
    [JsonPropertyName("message")]
    public string? MessageString { get; set; }
    
    [JsonPropertyName("innererror")]
    public object? InnerError { get; set; }
}

/// <summary>
/// OData error message (v3)
/// </summary>
public class ODataErrorMessage
{
    [JsonPropertyName("lang")]
    public string? Language { get; set; }
    
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}