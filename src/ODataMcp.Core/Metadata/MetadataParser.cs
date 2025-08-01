using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using ODataMcp.Core.Models;

namespace ODataMcp.Core.Metadata;

/// <summary>
/// Parser for OData metadata
/// </summary>
public class MetadataParser : IMetadataParser
{
    private readonly ILogger<MetadataParser> _logger;
    
    public MetadataParser(ILogger<MetadataParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ODataMetadata> ParseAsync(string serviceUrl, HttpClient httpClient, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching metadata from {ServiceUrl}", serviceUrl);
        
        // Fetch metadata XML
        var metadataUrl = serviceUrl.TrimEnd('/') + "/" + Constants.Constants.MetadataEndpoint;
        
        // Create request with proper headers for metadata
        using var request = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml"));
        
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var xml = await response.Content.ReadAsStringAsync();
        
        // Parse XML
        var doc = XDocument.Parse(xml);
        var metadata = new ODataMetadata
        {
            ServiceUrl = serviceUrl
        };
        
        // TODO: Implement full metadata parsing
        // For now, return minimal metadata
        _logger.LogInformation("Metadata parsed successfully");
        
        return metadata;
    }
}