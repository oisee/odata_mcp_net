using System.Net.Http;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using ODataMcp.Core.Debug;

namespace ODataMcp.Core.Services;

/// <summary>
/// Simple OData service implementation using direct metadata parsing
/// </summary>
public class SimpleODataService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SimpleODataService> _logger;
    private readonly string _serviceUrl;
    private readonly string? _username;
    private readonly string? _password;
    private IEdmModel? _model;

    public SimpleODataService(HttpClient httpClient, string serviceUrl, string? username, string? password, ILogger<SimpleODataService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _serviceUrl = serviceUrl ?? throw new ArgumentNullException(nameof(serviceUrl));
        _username = username;
        _password = password;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEdmModel> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (_model != null)
            return _model;

        _model = await SimpleMetadataParser.ParseMetadataAsync(_httpClient, _serviceUrl, _username, _password, _logger);
        return _model;
    }

    public async Task<object> ExecuteQueryAsync(string entitySetName, Dictionary<string, string> queryOptions, CancellationToken cancellationToken = default)
    {
        var url = $"{_serviceUrl.TrimEnd('/')}/{entitySetName}";
        
        // Build query string
        var queryParams = new List<string>();
        foreach (var (key, value) in queryOptions)
        {
            if (!string.IsNullOrEmpty(value))
            {
                queryParams.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        if (queryParams.Any())
        {
            url += "?" + string.Join("&", queryParams);
        }

        _logger.LogDebug("Executing OData query: {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        
        // Add basic auth if provided
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<object>(json)!;
    }

    public async Task<object?> GetByKeyAsync(string entitySetName, string key, CancellationToken cancellationToken = default)
    {
        var url = $"{_serviceUrl.TrimEnd('/')}/{entitySetName}({key})";
        
        _logger.LogDebug("Getting entity by key: {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        
        // Add basic auth if provided
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<object>(json)!;
    }

    public async Task<object> CreateAsync(string entitySetName, object entity, CancellationToken cancellationToken = default)
    {
        var url = $"{_serviceUrl.TrimEnd('/')}/{entitySetName}";
        
        var json = System.Text.Json.JsonSerializer.Serialize(entity);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        
        // Add basic auth if provided
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<object>(responseJson)!;
    }

    public async Task<object> UpdateAsync(string entitySetName, string key, object updates, CancellationToken cancellationToken = default)
    {
        var url = $"{_serviceUrl.TrimEnd('/')}/{entitySetName}({key})";
        
        var json = System.Text.Json.JsonSerializer.Serialize(updates);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        
        // Add basic auth if provided
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Get updated entity
        return await GetByKeyAsync(entitySetName, key, cancellationToken) ?? updates;
    }

    public async Task<bool> DeleteAsync(string entitySetName, string key, CancellationToken cancellationToken = default)
    {
        var url = $"{_serviceUrl.TrimEnd('/')}/{entitySetName}({key})";
        
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        
        // Add basic auth if provided
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<object> ExecuteFunctionAsync(string functionUrl, CancellationToken cancellationToken = default)
    {
        var url = $"{_serviceUrl.TrimEnd('/')}/{functionUrl}";
        
        
        _logger.LogInformation("ExecuteFunctionAsync: Full URL = {Url}", url);
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        
        // V2 services typically return XML by default, but we can accept both
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/atom+xml"));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        
        // Add OData V2 specific headers
        request.Headers.Add("DataServiceVersion", "2.0");
        request.Headers.Add("MaxDataServiceVersion", "2.0");
        
        // Add basic auth if provided
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("HTTP {StatusCode} {ReasonPhrase} for URL: {Url}. Response: {ErrorContent}", 
                (int)response.StatusCode, response.ReasonPhrase, url, errorContent);
        }
        
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        // Parse the response based on content type
        if (response.Content.Headers.ContentType?.MediaType == "application/json")
        {
            return System.Text.Json.JsonSerializer.Deserialize<object>(content) ?? new { };
        }
        else
        {
            // For XML responses, parse and convert to dynamic object
            var doc = XDocument.Parse(content);
            var entries = new List<object>();
            
            // Parse atom feed format
            foreach (var entry in doc.Descendants().Where(e => e.Name.LocalName == "entry"))
            {
                var properties = entry.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "properties");
                    
                if (properties != null)
                {
                    var item = new Dictionary<string, object>();
                    foreach (var prop in properties.Elements())
                    {
                        item[prop.Name.LocalName] = prop.Value;
                    }
                    entries.Add(item);
                }
            }
            
            return new { value = entries };
        }
    }
}