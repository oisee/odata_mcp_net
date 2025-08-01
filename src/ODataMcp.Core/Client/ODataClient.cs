using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ODataMcp.Core.Configuration;
using ODataMcp.Core.Metadata;
using ODataMcp.Core.Models;

namespace ODataMcp.Core.Client;

/// <summary>
/// OData client implementation
/// </summary>
public class ODataClient : IODataClient
{
    private readonly ODataMcpConfig _config;
    private readonly ILogger<ODataClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly IMetadataParser _metadataParser;
    
    private string? _csrfToken;
    private ODataMetadata? _metadata;

    public ODataClient(
        ODataMcpConfig config,
        ILogger<ODataClient> logger,
        IHttpClientFactory httpClientFactory,
        IMetadataParser metadataParser)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metadataParser = metadataParser ?? throw new ArgumentNullException(nameof(metadataParser));
        
        _httpClient = httpClientFactory.CreateClient();
        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        // Set base address
        if (!string.IsNullOrEmpty(_config.ServiceUrl))
        {
            _httpClient.BaseAddress = new Uri(_config.ServiceUrl);
        }
        
        // Configure authentication
        if (_config.HasBasicAuth)
        {
            var authBytes = Encoding.UTF8.GetBytes($"{_config.Username}:{_config.Password}");
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        }
        
        // Set default headers
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(Constants.Constants.JsonContentType));
    }

    public async Task<ODataMetadata> GetMetadataAsync(CancellationToken cancellationToken)
    {
        if (_metadata != null)
            return _metadata;
            
        _metadata = await _metadataParser.ParseAsync(_config.ServiceUrl!, _httpClient, cancellationToken);
        return _metadata;
    }

    public async Task<object> QueryEntitiesAsync(string entitySetName, ODataQueryOptions options, CancellationToken cancellationToken)
    {
        var url = entitySetName + options.ToQueryString();
        _logger.LogDebug("Querying entities: {Url}", url);
        
        var response = await ExecuteGetAsync(url, cancellationToken);
        return await ParseResponseAsync(response);
    }

    public async Task<object?> GetEntityAsync(string entitySetName, string key, ODataQueryOptions? options, CancellationToken cancellationToken)
    {
        var url = $"{entitySetName}({key})";
        if (options != null)
        {
            url += options.ToQueryString();
        }
        
        _logger.LogDebug("Getting entity: {Url}", url);
        
        var response = await ExecuteGetAsync(url, cancellationToken);
        return await ParseResponseAsync(response);
    }

    public async Task<object> CreateEntityAsync(string entitySetName, object entity, CancellationToken cancellationToken)
    {
        await EnsureCsrfTokenAsync(cancellationToken);
        
        var json = JsonSerializer.Serialize(entity);
        var content = new StringContent(json, Encoding.UTF8, Constants.Constants.JsonContentType);
        
        _logger.LogDebug("Creating entity in {EntitySet}", entitySetName);
        
        var response = await ExecutePostAsync(entitySetName, content, cancellationToken);
        return await ParseResponseAsync(response);
    }

    public async Task UpdateEntityAsync(string entitySetName, string key, object entity, CancellationToken cancellationToken)
    {
        await EnsureCsrfTokenAsync(cancellationToken);
        
        var url = $"{entitySetName}({key})";
        var json = JsonSerializer.Serialize(entity);
        var content = new StringContent(json, Encoding.UTF8, Constants.Constants.JsonContentType);
        
        _logger.LogDebug("Updating entity: {Url}", url);
        
        await ExecutePutAsync(url, content, cancellationToken);
    }

    public async Task DeleteEntityAsync(string entitySetName, string key, CancellationToken cancellationToken)
    {
        await EnsureCsrfTokenAsync(cancellationToken);
        
        var url = $"{entitySetName}({key})";
        _logger.LogDebug("Deleting entity: {Url}", url);
        
        await ExecuteDeleteAsync(url, cancellationToken);
    }

    public async Task<object?> CallFunctionAsync(string functionName, Dictionary<string, object?>? parameters, CancellationToken cancellationToken)
    {
        // TODO: Implement function import calls
        _logger.LogDebug("Calling function: {Function}", functionName);
        await Task.CompletedTask;
        return null;
    }

    public async Task<long> GetCountAsync(string entitySetName, string? filter, CancellationToken cancellationToken)
    {
        var url = $"{entitySetName}/$count";
        if (!string.IsNullOrEmpty(filter))
        {
            url += $"?$filter={Uri.EscapeDataString(filter)}";
        }
        
        _logger.LogDebug("Getting count: {Url}", url);
        
        var response = await ExecuteGetAsync(url, cancellationToken);
        var countStr = await response.Content.ReadAsStringAsync();
        
        if (long.TryParse(countStr.Trim(), out var count))
        {
            return count;
        }
        
        throw new InvalidOperationException($"Invalid count response: {countStr}");
    }

    private async Task EnsureCsrfTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_csrfToken))
            return;
            
        _logger.LogDebug("Fetching CSRF token");
        
        using var request = new HttpRequestMessage(HttpMethod.Head, _config.ServiceUrl);
        request.Headers.Add(Constants.Constants.CsrfTokenHeader, Constants.Constants.CsrfTokenFetch);
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues(Constants.Constants.CsrfTokenHeader, out var tokens))
        {
            _csrfToken = tokens.FirstOrDefault();
            _logger.LogDebug("CSRF token obtained");
        }
    }

    private async Task<HttpResponseMessage> ExecuteGetAsync(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response);
        return response;
    }

    private async Task<HttpResponseMessage> ExecutePostAsync(string url, HttpContent content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (!string.IsNullOrEmpty(_csrfToken))
        {
            request.Headers.Add(Constants.Constants.CsrfTokenHeader, _csrfToken);
        }
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response);
        return response;
    }

    private async Task ExecutePutAsync(string url, HttpContent content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };
        if (!string.IsNullOrEmpty(_csrfToken))
        {
            request.Headers.Add(Constants.Constants.CsrfTokenHeader, _csrfToken);
        }
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response);
    }

    private async Task ExecuteDeleteAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        if (!string.IsNullOrEmpty(_csrfToken))
        {
            request.Headers.Add(Constants.Constants.CsrfTokenHeader, _csrfToken);
        }
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessStatusCodeAsync(response);
    }

    private async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogError("OData request failed: {StatusCode} - {Content}", response.StatusCode, content);
            
            // Try to parse OData error
            try
            {
                var error = JsonSerializer.Deserialize<ODataError>(content);
                var message = error?.Error?.MessageString ?? error?.Error?.Message?.Value ?? "Unknown error";
                throw new HttpRequestException($"OData error: {message}", null, response.StatusCode);
            }
            catch (JsonException)
            {
                // If not valid JSON, throw generic error
                response.EnsureSuccessStatusCode();
            }
        }
    }

    private async Task<object> ParseResponseAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        
        // TODO: Apply GUID optimization, date conversion, etc.
        
        return result;
    }
}