using ODataMcp.Core.Models;

namespace ODataMcp.Core.Client;

/// <summary>
/// Interface for OData client operations
/// </summary>
public interface IODataClient
{
    /// <summary>
    /// Get service metadata
    /// </summary>
    Task<ODataMetadata> GetMetadataAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Query entities
    /// </summary>
    Task<object> QueryEntitiesAsync(string entitySetName, ODataQueryOptions options, CancellationToken cancellationToken);
    
    /// <summary>
    /// Get a single entity by key
    /// </summary>
    Task<object?> GetEntityAsync(string entitySetName, string key, ODataQueryOptions? options, CancellationToken cancellationToken);
    
    /// <summary>
    /// Create a new entity
    /// </summary>
    Task<object> CreateEntityAsync(string entitySetName, object entity, CancellationToken cancellationToken);
    
    /// <summary>
    /// Update an existing entity
    /// </summary>
    Task UpdateEntityAsync(string entitySetName, string key, object entity, CancellationToken cancellationToken);
    
    /// <summary>
    /// Delete an entity
    /// </summary>
    Task DeleteEntityAsync(string entitySetName, string key, CancellationToken cancellationToken);
    
    /// <summary>
    /// Call a function import
    /// </summary>
    Task<object?> CallFunctionAsync(string functionName, Dictionary<string, object?>? parameters, CancellationToken cancellationToken);
    
    /// <summary>
    /// Get entity count
    /// </summary>
    Task<long> GetCountAsync(string entitySetName, string? filter, CancellationToken cancellationToken);
}

/// <summary>
/// OData query options
/// </summary>
public class ODataQueryOptions
{
    public string? Filter { get; set; }
    public string? Select { get; set; }
    public string? Expand { get; set; }
    public string? OrderBy { get; set; }
    public int? Top { get; set; }
    public int? Skip { get; set; }
    public bool? Count { get; set; }
    public string? Search { get; set; }
    public string? Format { get; set; }
    
    /// <summary>
    /// Convert to query string
    /// </summary>
    public string ToQueryString()
    {
        var parameters = new List<string>();
        
        if (!string.IsNullOrEmpty(Filter))
            parameters.Add($"$filter={Uri.EscapeDataString(Filter)}");
        if (!string.IsNullOrEmpty(Select))
            parameters.Add($"$select={Uri.EscapeDataString(Select)}");
        if (!string.IsNullOrEmpty(Expand))
            parameters.Add($"$expand={Uri.EscapeDataString(Expand)}");
        if (!string.IsNullOrEmpty(OrderBy))
            parameters.Add($"$orderby={Uri.EscapeDataString(OrderBy)}");
        if (Top.HasValue)
            parameters.Add($"$top={Top}");
        if (Skip.HasValue)
            parameters.Add($"$skip={Skip}");
        if (Count == true)
            parameters.Add("$count=true");
        if (!string.IsNullOrEmpty(Search))
            parameters.Add($"$search={Uri.EscapeDataString(Search)}");
        if (!string.IsNullOrEmpty(Format))
            parameters.Add($"$format={Uri.EscapeDataString(Format)}");
            
        return parameters.Any() ? "?" + string.Join("&", parameters) : string.Empty;
    }
}