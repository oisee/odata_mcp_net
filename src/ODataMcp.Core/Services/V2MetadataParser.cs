using System.Data.Services.Client;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;

namespace ODataMcp.Core.Services;

/// <summary>
/// Metadata parser specifically for OData v2 using legacy Microsoft.Data.Services.Client
/// </summary>
public static class V2MetadataParser
{
    public static IEdmModel? TryParseV2Metadata(string xmlContent, ILogger logger)
    {
        try
        {
            logger.LogInformation("Parsing V2 metadata, length: {Length}", xmlContent.Length);
            
            // Parse using legacy Data Services approach
            var model = ParseV2XmlToEdmModel(xmlContent, logger);
            if (model != null)
            {
                logger.LogInformation("Successfully parsed V2 metadata using legacy parser");
                return model;
            }
            
            logger.LogWarning("Failed to parse V2 metadata");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing V2 metadata");
            return null;
        }
    }
    
    private static IEdmModel? ParseV2XmlToEdmModel(string xmlContent, ILogger logger)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            
            // Check if this is a V2 EDMX document
            var edmxElement = doc.Root;
            if (edmxElement?.Name.LocalName != "Edmx")
            {
                logger.LogWarning("Root element is not Edmx");
                return null;
            }
            
            var version = edmxElement.Attribute("Version")?.Value;
            logger.LogInformation("EDMX Version: {Version}", version);
            
            // For V2, we need to convert the EDMX to a modern EDM model
            var model = ConvertV2EdmxToEdmModel(doc, logger);
            return model;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error converting V2 EDMX to EDM model");
            return null;
        }
    }
    
    private static IEdmModel? ConvertV2EdmxToEdmModel(XDocument edmxDoc, ILogger logger)
    {
        try
        {
            var model = new EdmModel();
            
            // Navigate to the Schema element
            var schemaElement = edmxDoc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Schema");
                
            if (schemaElement == null)
            {
                logger.LogWarning("No Schema element found in EDMX");
                return null;
            }
            
            var namespaceName = schemaElement.Attribute("Namespace")?.Value ?? "Default";
            logger.LogInformation("Schema namespace: {Namespace}", namespaceName);
            
            // Find entity container
            var containerElement = schemaElement.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "EntityContainer");
                
            if (containerElement == null)
            {
                logger.LogWarning("No EntityContainer found");
                return null;
            }
            
            var containerName = containerElement.Attribute("Name")?.Value ?? "Container";
            var container = new EdmEntityContainer(namespaceName, containerName);
            model.AddElement(container);
            
            var entityTypes = new Dictionary<string, EdmEntityType>();
            
            // Parse entity types first
            logger.LogInformation("Parsing entity types from schema namespace: {Namespace}", namespaceName);
            foreach (var entityTypeElement in schemaElement.Descendants().Where(e => e.Name.LocalName == "EntityType"))
            {
                var entityType = ParseEntityType(entityTypeElement, namespaceName, logger);
                if (entityType != null)
                {
                    entityTypes[entityType.Name] = entityType;
                    model.AddElement(entityType);
                    logger.LogInformation("Added entity type: {EntityType}", entityType.Name);
                }
                else
                {
                    logger.LogWarning("Failed to parse entity type element");
                }
            }
            
            // Parse entity sets
            var entitySetCount = 0;
            foreach (var entitySetElement in containerElement.Descendants().Where(e => e.Name.LocalName == "EntitySet"))
            {
                var setName = entitySetElement.Attribute("Name")?.Value;
                var entityTypeName = entitySetElement.Attribute("EntityType")?.Value;
                
                if (string.IsNullOrEmpty(setName) || string.IsNullOrEmpty(entityTypeName))
                {
                    logger.LogWarning("Skipping entity set with missing name or type");
                    continue;
                }
                
                // Extract type name from fully qualified name
                var typeName = entityTypeName.Split('.').Last();
                
                if (entityTypes.TryGetValue(typeName, out var entityType))
                {
                    var entitySet = new EdmEntitySet(container, setName, entityType);
                    container.AddElement(entitySet);
                    entitySetCount++;
                    logger.LogInformation("Added entity set: {SetName} -> {TypeName}", setName, typeName);
                }
                else
                {
                    logger.LogWarning("Entity type not found for entity set: {SetName} -> {TypeName}", setName, typeName);
                }
            }
            
            // Parse function imports
            var functionCount = 0;
            logger.LogInformation("Looking for FunctionImport elements in container");
            var functionImportElements = containerElement.Descendants().Where(e => e.Name.LocalName == "FunctionImport").ToList();
            logger.LogInformation("Found {Count} FunctionImport elements", functionImportElements.Count);
            
            foreach (var functionElement in functionImportElements)
            {
                var functionName = functionElement.Attribute("Name")?.Value;
                var returnTypeName = functionElement.Attribute("ReturnType")?.Value;
                var entitySetName = functionElement.Attribute("EntitySet")?.Value;
                
                logger.LogInformation("Processing function import: Name={Name}, ReturnType={ReturnType}, EntitySet={EntitySet}", 
                    functionName, returnTypeName, entitySetName);
                
                if (string.IsNullOrEmpty(functionName))
                {
                    logger.LogWarning("Skipping function import with missing name");
                    continue;
                }
                
                // Create function and parameters
                var function = new EdmFunction(namespaceName, functionName, ParseReturnType(returnTypeName, model));
                
                // Parse parameters
                foreach (var paramElement in functionElement.Descendants().Where(e => e.Name.LocalName == "Parameter"))
                {
                    var paramName = paramElement.Attribute("Name")?.Value;
                    var paramType = paramElement.Attribute("Type")?.Value;
                    
                    logger.LogInformation("  Parameter: {Name} of type {Type}", paramName, paramType);
                    
                    if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramType))
                    {
                        var edmType = MapV2TypeToEdmType(paramType, true);
                        function.AddParameter(paramName, edmType);
                    }
                }
                
                model.AddElement(function);
                
                // Create function import
                IEdmEntitySet? targetEntitySet = null;
                if (!string.IsNullOrEmpty(entitySetName))
                {
                    targetEntitySet = container.FindEntitySet(entitySetName);
                }
                
                var functionImport = new EdmFunctionImport(container, functionName, function);
                container.AddElement(functionImport);
                functionCount++;
                logger.LogInformation("Successfully added function import: {FunctionName} with {ParamCount} parameters", 
                    functionName, function.Parameters.Count());
            }
            
            logger.LogInformation("Successfully converted V2 EDMX to EDM model with {EntityTypeCount} entity types, {EntitySetCount} entity sets, and {FunctionCount} function imports", 
                entityTypes.Count, entitySetCount, functionCount);
            
            // If we didn't parse any entity sets, this is a failure
            if (entitySetCount == 0)
            {
                logger.LogError("No entity sets were parsed from V2 metadata");
                return null;
            }
            
            return model;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ConvertV2EdmxToEdmModel");
            return null;
        }
    }
    
    private static EdmEntityType? ParseEntityType(XElement entityTypeElement, string namespaceName, ILogger logger)
    {
        try
        {
            var typeName = entityTypeElement.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(typeName))
                return null;
            
            var entityType = new EdmEntityType(namespaceName, typeName);
            var keyProperties = new List<EdmStructuralProperty>();
            
            // Find key properties first
            var keyElement = entityTypeElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Key");
            var keyPropertyNames = new HashSet<string>();
            
            if (keyElement != null)
            {
                foreach (var propertyRef in keyElement.Descendants().Where(e => e.Name.LocalName == "PropertyRef"))
                {
                    var keyPropName = propertyRef.Attribute("Name")?.Value;
                    if (!string.IsNullOrEmpty(keyPropName))
                    {
                        keyPropertyNames.Add(keyPropName);
                    }
                }
            }
            
            // Parse properties
            foreach (var propertyElement in entityTypeElement.Descendants().Where(e => e.Name.LocalName == "Property"))
            {
                var propName = propertyElement.Attribute("Name")?.Value;
                var propType = propertyElement.Attribute("Type")?.Value;
                var nullable = propertyElement.Attribute("Nullable")?.Value != "false";
                
                if (string.IsNullOrEmpty(propName) || string.IsNullOrEmpty(propType))
                    continue;
                
                var edmType = MapV2TypeToEdmType(propType, nullable);
                var property = new EdmStructuralProperty(entityType, propName, edmType);
                entityType.AddProperty(property);
                
                if (keyPropertyNames.Contains(propName))
                {
                    keyProperties.Add(property);
                }
            }
            
            // Set key properties
            if (keyProperties.Any())
            {
                entityType.AddKeys(keyProperties);
            }
            
            logger.LogDebug("Parsed entity type: {TypeName} with {PropertyCount} properties, {KeyCount} keys", 
                typeName, entityType.Properties().Count(), keyProperties.Count);
            
            return entityType;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing entity type");
            return null;
        }
    }
    
    private static IEdmTypeReference MapV2TypeToEdmType(string v2Type, bool nullable)
    {
        // Handle complex types that might contain namespace
        if (v2Type.Contains('.') && !v2Type.StartsWith("Edm."))
        {
            // This is likely a complex type, for now treat as string
            return EdmCoreModel.Instance.GetString(nullable);
        }
        
        return v2Type switch
        {
            "Edm.String" => EdmCoreModel.Instance.GetString(nullable),
            "Edm.Int32" => EdmCoreModel.Instance.GetInt32(nullable),
            "Edm.Int64" => EdmCoreModel.Instance.GetInt64(nullable),
            "Edm.Int16" => EdmCoreModel.Instance.GetInt16(nullable),
            "Edm.Byte" => EdmCoreModel.Instance.GetByte(nullable),
            "Edm.Decimal" => EdmCoreModel.Instance.GetDecimal(nullable),
            "Edm.Double" => EdmCoreModel.Instance.GetDouble(nullable),
            "Edm.Single" => EdmCoreModel.Instance.GetSingle(nullable),
            "Edm.Boolean" => EdmCoreModel.Instance.GetBoolean(nullable),
            "Edm.DateTime" => EdmCoreModel.Instance.GetDateTimeOffset(nullable), // Map V2 DateTime to V4 DateTimeOffset
            "Edm.DateTimeOffset" => EdmCoreModel.Instance.GetDateTimeOffset(nullable),
            "Edm.Time" => EdmCoreModel.Instance.GetTimeOfDay(nullable),
            "Edm.Guid" => EdmCoreModel.Instance.GetGuid(nullable),
            "Edm.Binary" => EdmCoreModel.Instance.GetBinary(nullable),
            _ => EdmCoreModel.Instance.GetString(nullable) // Default to string for unknown types
        };
    }
    
    private static IEdmTypeReference ParseReturnType(string? returnTypeName, EdmModel model)
    {
        if (string.IsNullOrEmpty(returnTypeName))
        {
            // Void return
            return EdmCoreModel.Instance.GetPrimitive(EdmPrimitiveTypeKind.None, false);
        }
        
        // Handle Collection(EntityType) format
        if (returnTypeName.StartsWith("Collection(") && returnTypeName.EndsWith(")"))
        {
            var innerType = returnTypeName[11..^1]; // Remove "Collection(" and ")"
            
            // Try to find entity type in model
            var entityTypeName = innerType.Split('.').Last();
            var entityType = model.SchemaElements
                .OfType<IEdmEntityType>()
                .FirstOrDefault(t => t.Name == entityTypeName);
                
            if (entityType != null)
            {
                return EdmCoreModel.GetCollection(new EdmEntityTypeReference(entityType, false));
            }
            
            // Fallback to collection of complex type
            return EdmCoreModel.GetCollection(EdmCoreModel.Instance.GetString(false));
        }
        
        // Try as primitive type
        return MapV2TypeToEdmType(returnTypeName, false);
    }
}