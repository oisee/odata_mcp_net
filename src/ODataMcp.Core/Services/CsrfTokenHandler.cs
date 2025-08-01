using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace ODataMcp.Core.Services;

/// <summary>
/// Handles CSRF token fetching and management for SAP OData services
/// </summary>
public class CsrfTokenHandler
{
    private readonly ILogger<CsrfTokenHandler> _logger;
    private string? _csrfToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly TimeSpan _tokenLifetime = TimeSpan.FromMinutes(30);

    public CsrfTokenHandler(ILogger<CsrfTokenHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a valid CSRF token, fetching a new one if necessary
    /// </summary>
    public async Task<string?> GetTokenAsync(HttpClient httpClient, string serviceUrl, string? username, string? password, CancellationToken cancellationToken = default)
    {
        // Check if we have a valid cached token
        if (!string.IsNullOrEmpty(_csrfToken) && DateTime.UtcNow < _tokenExpiry)
        {
            _logger.LogDebug("Using cached CSRF token");
            return _csrfToken;
        }

        _logger.LogInformation("Fetching new CSRF token");

        try
        {
            // Create a HEAD request to the service root
            var request = new HttpRequestMessage(HttpMethod.Head, serviceUrl);
            request.Headers.Add("X-CSRF-Token", "Fetch");
            
            // Add basic auth if provided
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            
            // Extract CSRF token from response headers
            if (response.Headers.TryGetValues("X-CSRF-Token", out var tokenValues))
            {
                _csrfToken = tokenValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(_csrfToken))
                {
                    _tokenExpiry = DateTime.UtcNow.Add(_tokenLifetime);
                    _logger.LogDebug("CSRF token fetched successfully");
                    return _csrfToken;
                }
            }

            // Some services return the token in a different header
            if (response.Headers.TryGetValues("x-csrf-token", out var tokenValuesLower))
            {
                _csrfToken = tokenValuesLower.FirstOrDefault();
                if (!string.IsNullOrEmpty(_csrfToken))
                {
                    _tokenExpiry = DateTime.UtcNow.Add(_tokenLifetime);
                    _logger.LogDebug("CSRF token fetched successfully (lowercase header)");
                    return _csrfToken;
                }
            }

            _logger.LogWarning("No CSRF token returned by service");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching CSRF token");
            return null;
        }
    }

    /// <summary>
    /// Invalidates the cached token
    /// </summary>
    public void InvalidateToken()
    {
        _logger.LogDebug("Invalidating CSRF token");
        _csrfToken = null;
        _tokenExpiry = DateTime.MinValue;
    }

    /// <summary>
    /// Applies the CSRF token to a request if available
    /// </summary>
    public void ApplyTokenToRequest(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_csrfToken) && DateTime.UtcNow < _tokenExpiry)
        {
            request.Headers.Add("X-CSRF-Token", _csrfToken);
            _logger.LogDebug("Applied CSRF token to request");
        }
    }
}