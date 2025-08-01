namespace ODataMcp.Core.Utils;

/// <summary>
/// Interface for shortening entity names
/// </summary>
public interface INameShortener
{
    /// <summary>
    /// Shorten an entity name
    /// </summary>
    string Shorten(string entityName, int targetLength = 20);
}