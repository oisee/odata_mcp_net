using ODataMcp.Core.Models;

namespace ODataMcp.Core.Metadata;

/// <summary>
/// Interface for parsing OData metadata
/// </summary>
public interface IMetadataParser
{
    /// <summary>
    /// Parse OData metadata from the service
    /// </summary>
    Task<ODataMetadata> ParseAsync(string serviceUrl, HttpClient httpClient, CancellationToken cancellationToken);
}