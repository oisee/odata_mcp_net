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
        IEdmModel? model = null;
        IEnumerable<EdmError>? errors = null;
        
        try
        {
            using var stringReader = new StringReader(xmlContent);
            using var xmlReader = System.Xml.XmlReader.Create(stringReader);
            
            // Try different parsing approaches
            if (CsdlReader.TryParse(xmlReader, out model, out errors))
            {
                logger.LogInformation("Successfully parsed metadata using standard approach");
                return model!;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Standard parsing failed, trying lenient approach");
        }
        
        try
        {
            // If that fails, try with the ignoreUnexpectedAttributesAndElements flag
            using var stringReader2 = new StringReader(xmlContent);
            using var xmlReader2 = System.Xml.XmlReader.Create(stringReader2);
            
            if (CsdlReader.TryParse(xmlReader2, true, out model, out errors))
            {
                logger.LogInformation("Successfully parsed metadata with lenient parsing");
                return model!;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lenient parsing failed, trying manual approach");
        }
        
        // Try V2-specific parser using legacy libraries
        logger.LogInformation("Attempting V2-specific metadata parsing");
        var v2Model = V2MetadataParser.TryParseV2Metadata(xmlContent, logger);
        if (v2Model != null)
        {
            logger.LogInformation("V2MetadataParser succeeded");
            return v2Model;
        }
        else
        {
            logger.LogWarning("V2MetadataParser returned null");
        }
        
        // Try manual parsing for OData v2 format as fallback
        logger.LogInformation("Attempting manual V2 metadata parsing");
        var parsedModel = TryParseV2Metadata(xmlContent, logger);
        if (parsedModel != null)
        {
            logger.LogInformation("Manual V2 parsing succeeded");
            return parsedModel;
        }
        else
        {
            logger.LogWarning("Manual V2 parsing returned null");
        }
        
        // If all parsing approaches fail, throw an exception with details
        var errorMessage = errors != null ? string.Join(", ", errors.Select(e => e.ErrorMessage)) : "Unknown error";
        var metadataSnippet = xmlContent.Length > 500 ? xmlContent.Substring(0, 500) + "..." : xmlContent;
        throw new InvalidOperationException($"Failed to parse OData metadata. All parsing strategies failed.\nErrors: {errorMessage}\nMetadata snippet: {metadataSnippet}");
    }
    
    private static IEdmModel? TryParseV2Metadata(string xmlContent, ILogger logger)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var schemaElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Schema");
            
            if (schemaElement == null)
                return null;
                
            var model = new EdmModel();
            var namespaceName = schemaElement.Attribute("Namespace")?.Value ?? "Default";
            
            // Find entity container
            var containerElement = schemaElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "EntityContainer");
            if (containerElement == null)
                return null;
                
            var containerName = containerElement.Attribute("Name")?.Value ?? "Container";
            var container = new EdmEntityContainer(namespaceName, containerName);
            model.AddElement(container);
            
            var entityTypes = new Dictionary<string, EdmEntityType>();
            
            // Parse entity types
            foreach (var entityTypeElement in schemaElement.Descendants().Where(e => e.Name.LocalName == "EntityType"))
            {
                var typeName = entityTypeElement.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(typeName)) continue;
                
                var entityType = new EdmEntityType(namespaceName, typeName);
                entityTypes[typeName] = entityType;
                
                // Parse properties
                var keyProperties = new List<EdmStructuralProperty>();
                
                foreach (var propertyElement in entityTypeElement.Descendants().Where(e => e.Name.LocalName == "Property"))
                {
                    var propName = propertyElement.Attribute("Name")?.Value;
                    var propType = propertyElement.Attribute("Type")?.Value;
                    var nullable = propertyElement.Attribute("Nullable")?.Value != "false";
                    
                    if (string.IsNullOrEmpty(propName) || string.IsNullOrEmpty(propType)) continue;
                    
                    var edmType = MapV2TypeToEdmType(propType, nullable);
                    var property = new EdmStructuralProperty(entityType, propName, edmType);
                    entityType.AddProperty(property);
                    
                    // Check if this is a key property
                    var keyElement = entityTypeElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Key");
                    if (keyElement != null)
                    {
                        var keyPropertyRefs = keyElement.Descendants().Where(e => e.Name.LocalName == "PropertyRef");
                        if (keyPropertyRefs.Any(pr => pr.Attribute("Name")?.Value == propName))
                        {
                            keyProperties.Add(property);
                        }
                    }
                }
                
                if (keyProperties.Any())
                {
                    entityType.AddKeys(keyProperties);
                }
                
                model.AddElement(entityType);
            }
            
            // Parse entity sets
            foreach (var entitySetElement in containerElement.Descendants().Where(e => e.Name.LocalName == "EntitySet"))
            {
                var setName = entitySetElement.Attribute("Name")?.Value;
                var entityTypeName = entitySetElement.Attribute("EntityType")?.Value;
                
                if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(entityTypeName)) continue;
                
                // Extract just the type name from fully qualified name
                var typeName = entityTypeName.Split('.').Last();
                
                if (entityTypes.TryGetValue(typeName, out var entityType))
                {
                    var entitySet = new EdmEntitySet(container, setName, entityType);
                    container.AddElement(entitySet);
                }
            }
            
            logger.LogInformation("Successfully parsed V2 metadata manually - found {Count} entity types", entityTypes.Count);
            return model;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Manual V2 parsing failed");
            return null;
        }
    }
    
    private static IEdmTypeReference MapV2TypeToEdmType(string v2Type, bool nullable)
    {
        var primitiveType = v2Type switch
        {
            "Edm.String" => EdmCoreModel.Instance.GetString(nullable),
            "Edm.Int32" => EdmCoreModel.Instance.GetInt32(nullable),
            "Edm.Int64" => EdmCoreModel.Instance.GetInt64(nullable),
            "Edm.Decimal" => EdmCoreModel.Instance.GetDecimal(nullable),
            "Edm.Double" => EdmCoreModel.Instance.GetDouble(nullable),
            "Edm.Boolean" => EdmCoreModel.Instance.GetBoolean(nullable),
            "Edm.DateTime" => EdmCoreModel.Instance.GetDateTimeOffset(nullable),
            "Edm.Guid" => EdmCoreModel.Instance.GetGuid(nullable),
            "Edm.Binary" => EdmCoreModel.Instance.GetBinary(nullable),
            _ => EdmCoreModel.Instance.GetString(nullable) // Default to string for unknown types
        };
        
        return primitiveType;
    }

}