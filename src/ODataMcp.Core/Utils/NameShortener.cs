using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ODataMcp.Core.Utils;

/// <summary>
/// Shortens entity names for tool naming
/// </summary>
public class NameShortener : INameShortener
{
    private readonly ILogger<NameShortener> _logger;
    private readonly Dictionary<string, string> _domainKeywords = new()
    {
        ["SCREENING"] = "Scrn",
        ["ADDRESS"] = "Addr",
        ["INVESTIGATION"] = "Inv",
        ["BUSINESS"] = "Biz",
        ["CUSTOMER"] = "Cust",
        ["PRODUCT"] = "Prod",
        ["ORDER"] = "Ord",
        ["EMPLOYEE"] = "Emp",
        ["SUPPLIER"] = "Supp",
        ["CATEGORY"] = "Cat",
        ["INVENTORY"] = "Inv",
        ["TRANSACTION"] = "Trans",
        ["DOCUMENT"] = "Doc",
        ["ACCOUNT"] = "Acct",
        ["PAYMENT"] = "Pay"
    };
    
    public NameShortener(ILogger<NameShortener> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Shorten(string entityName, int targetLength = 20)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return entityName;
            
        if (entityName.Length <= targetLength)
            return entityName;
            
        _logger.LogDebug("Shortening entity name: {EntityName} (target: {TargetLength})", entityName, targetLength);
        
        // Stage 1: Tokenization
        var tokens = TokenizeEntityName(entityName);
        
        // Stage 2: Find longest meaningful token
        var longestToken = tokens.OrderByDescending(t => t.Length).FirstOrDefault() ?? entityName;
        
        // Stage 3: CamelCase decomposition
        var words = DecomposeCamelCase(longestToken);
        
        // Stage 4: Apply domain keywords
        words = ApplyDomainKeywords(words);
        
        // Stage 5: Progressive reduction
        var result = ProgressiveWordReduction(words, targetLength);
        
        // Stage 6: Vowel removal if still too long
        if (result.Length > targetLength)
        {
            result = RemoveVowels(result, targetLength);
        }
        
        _logger.LogDebug("Shortened '{EntityName}' to '{Result}'", entityName, result);
        return result;
    }

    private List<string> TokenizeEntityName(string entityName)
    {
        // Split on common delimiters
        return Regex.Split(entityName, @"[_\-\.\s]+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private List<string> DecomposeCamelCase(string text)
    {
        // Split CamelCase/PascalCase into words
        var words = new List<string>();
        var currentWord = "";
        
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]) && !char.IsUpper(text[i - 1]))
            {
                if (!string.IsNullOrEmpty(currentWord))
                {
                    words.Add(currentWord);
                    currentWord = "";
                }
            }
            currentWord += text[i];
        }
        
        if (!string.IsNullOrEmpty(currentWord))
        {
            words.Add(currentWord);
        }
        
        return words;
    }

    private List<string> ApplyDomainKeywords(List<string> words)
    {
        return words.Select(word =>
        {
            var upperWord = word.ToUpper();
            return _domainKeywords.ContainsKey(upperWord) 
                ? _domainKeywords[upperWord] 
                : word;
        }).ToList();
    }

    private string ProgressiveWordReduction(List<string> words, int targetLength)
    {
        if (!words.Any())
            return string.Empty;
            
        // Start with full words
        var result = string.Join("", words);
        if (result.Length <= targetLength)
            return result;
            
        // Remove common suffixes
        var suffixes = new[] { "Set", "Type", "Info", "Data", "List", "Collection" };
        words = words.Select(w =>
        {
            foreach (var suffix in suffixes)
            {
                if (w.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && w.Length > suffix.Length)
                {
                    return w[..^suffix.Length];
                }
            }
            return w;
        }).ToList();
        
        result = string.Join("", words);
        if (result.Length <= targetLength)
            return result;
            
        // Abbreviate words
        if (words.Count > 1)
        {
            // Keep first word, abbreviate others
            var abbreviated = new List<string> { words[0] };
            abbreviated.AddRange(words.Skip(1).Select(w => w.Length > 3 ? w.Substring(0, 3) : w));
            result = string.Join("", abbreviated);
            if (result.Length <= targetLength)
                return result;
        }
        
        return result;
    }

    private string RemoveVowels(string text, int targetLength)
    {
        if (text.Length <= targetLength)
            return text;
            
        var vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u', 'A', 'E', 'I', 'O', 'U' };
        var result = new System.Text.StringBuilder();
        
        foreach (char c in text)
        {
            if (!vowels.Contains(c) || result.Length == 0 || result.Length >= targetLength)
            {
                result.Append(c);
            }
        }
        
        return result.ToString();
    }
}