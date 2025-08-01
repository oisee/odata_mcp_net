using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;

namespace ODataMcp.Core.Services;

/// <summary>
/// Simple metadata parser that handles both OData v2 and v4
/// </summary>
public static class SimpleMetadataParser
{
    public static async Task<IEdmModel> ParseMetadataAsync(HttpClient httpClient, string serviceUrl, string? username, string? password, ILogger logger)
    {
        var metadataUrl = serviceUrl.TrimEnd('/') + "/$metadata";
        
        var request = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
        
        // Add basic auth if provided
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
        }

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var xmlContent = await response.Content.ReadAsStringAsync();
        
        // Try to parse the metadata directly
        using var stringReader = new StringReader(xmlContent);
        using var xmlReader = System.Xml.XmlReader.Create(stringReader);
        
        IEdmModel? model = null;
        IEnumerable<EdmError>? errors = null;
        
        // Try different parsing approaches
        if (CsdlReader.TryParse(xmlReader, out model, out errors))
        {
            logger.LogInformation("Successfully parsed metadata using standard approach");
            return model!;
        }
        
        // If that fails, try with the ignoreUnexpectedAttributesAndElements flag
        stringReader.Dispose();
        xmlReader.Dispose();
        
        using var stringReader2 = new StringReader(xmlContent);
        using var xmlReader2 = System.Xml.XmlReader.Create(stringReader2);
        
        if (CsdlReader.TryParse(xmlReader2, true, out model, out errors))
        {
            logger.LogInformation("Successfully parsed metadata with lenient parsing");
            return model!;
        }
        
        // If that still fails, try a manual approach for OData v2
        var doc = XDocument.Parse(xmlContent);
        var dataServicesElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "DataServices");
        
        if (dataServicesElement != null)
        {
            // Try parsing just the schema content
            var schemaElement = dataServicesElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Schema");
            if (schemaElement != null)
            {
                // Create a minimal CSDL document
                var csdlDoc = new XDocument(
                    new XElement(XName.Get("Schema", "http://docs.oasis-open.org/odata/ns/edm"),
                        new XAttribute("Namespace", schemaElement.Attribute("Namespace")?.Value ?? "Default"),
                        schemaElement.Elements()
                    )
                );
                
                using var stringReader3 = new StringReader(csdlDoc.ToString());
                using var xmlReader3 = System.Xml.XmlReader.Create(stringReader3);
                
                if (CsdlReader.TryParse(xmlReader3, true, out model, out errors))
                {
                    logger.LogInformation("Successfully parsed metadata using manual schema extraction");
                    return model!;
                }
            }
        }
        
        // If all else fails, throw an error with details
        var errorMessage = errors != null ? string.Join(", ", errors.Select(e => e.ErrorMessage)) : "Unknown error";
        throw new InvalidOperationException($"Failed to parse OData metadata: {errorMessage}. First 500 chars of metadata: {xmlContent.Substring(0, Math.Min(500, xmlContent.Length))}");
    }
}