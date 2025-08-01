using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ODataMcp.Core.Utils;

/// <summary>
/// Manages service hints
/// </summary>
public class HintManager : IHintManager
{
    private readonly ILogger<HintManager> _logger;
    private readonly List<HintEntry> _hints = new();
    private ServiceHints? _cliHint;
    
    public HintManager(ILogger<HintManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LoadFromFileAsync(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            // Try default location
            filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hints.json");
        }
        
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("Hints file not found: {FilePath}", filePath);
            return;
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var hintsFile = JsonSerializer.Deserialize<HintsFile>(json);
            
            if (hintsFile?.Hints != null)
            {
                _hints.AddRange(hintsFile.Hints);
                _logger.LogInformation("Loaded {Count} hints from {FilePath}", _hints.Count, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load hints from {FilePath}", filePath);
        }
    }

    public void SetCliHint(string hint)
    {
        try
        {
            // Try to parse as JSON
            _cliHint = JsonSerializer.Deserialize<ServiceHints>(hint);
            if (_cliHint != null)
            {
                _cliHint.HintSource = "CLI";
            }
        }
        catch (JsonException)
        {
            // If not JSON, treat as plain text note
            _cliHint = new ServiceHints
            {
                Notes = hint,
                HintSource = "CLI"
            };
        }
    }

    public ServiceHints? GetHintsForService(string serviceUrl)
    {
        // Check CLI hint first (highest priority)
        if (_cliHint != null)
            return _cliHint;
            
        // Find matching hints by pattern
        var matchingHints = _hints
            .Where(h => MatchesPattern(serviceUrl, h.Pattern))
            .OrderByDescending(h => h.Priority)
            .ToList();
            
        if (!matchingHints.Any())
            return null;
            
        // Merge hints (higher priority wins)
        var result = new ServiceHints
        {
            HintSource = $"Hints file (pattern: {matchingHints[0].Pattern})"
        };
        
        foreach (var hint in matchingHints.Reverse<HintEntry>())
        {
            if (!string.IsNullOrEmpty(hint.ServiceType))
                result.ServiceType = hint.ServiceType;
            if (hint.KnownIssues?.Any() == true)
                result.KnownIssues = hint.KnownIssues;
            if (hint.Workarounds?.Any() == true)
                result.Workarounds = hint.Workarounds;
            if (hint.FieldHints?.Any() == true)
                result.FieldHints = hint.FieldHints;
            if (hint.Examples?.Any() == true)
                result.Examples = hint.Examples;
            if (!string.IsNullOrEmpty(hint.Notes))
                result.Notes = hint.Notes;
        }
        
        return result;
    }

    private bool MatchesPattern(string url, string pattern)
    {
        // Simple wildcard matching
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(url, regex, 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
    
    private class HintsFile
    {
        public string? Version { get; set; }
        public List<HintEntry>? Hints { get; set; }
    }
    
    private class HintEntry : ServiceHints
    {
        public string Pattern { get; set; } = string.Empty;
        public int Priority { get; set; }
    }
}