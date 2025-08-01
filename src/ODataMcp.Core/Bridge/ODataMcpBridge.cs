using Microsoft.Extensions.Logging;
using ODataMcp.Core.Client;
using ODataMcp.Core.Configuration;
using ODataMcp.Core.Metadata;
using ODataMcp.Core.Models;
using ODataMcp.Core.Transport;
using ODataMcp.Core.Utils;

namespace ODataMcp.Core.Bridge;

/// <summary>
/// Main bridge implementation that connects OData services to MCP
/// </summary>
public class ODataMcpBridge : IODataMcpBridge
{
    private readonly ODataMcpConfig _config;
    private readonly ILogger<ODataMcpBridge> _logger;
    private readonly IMcpServer _mcpServer;
    private readonly IODataClient _odataClient;
    private readonly IMetadataParser _metadataParser;
    private readonly IHintManager _hintManager;
    private readonly INameShortener _nameShortener;
    private readonly IGuidHandler _guidHandler;
    
    private ODataMetadata? _metadata;
    private bool _initialized;

    public ODataMcpBridge(
        ODataMcpConfig config,
        ILogger<ODataMcpBridge> logger,
        IMcpServer mcpServer,
        IODataClient odataClient,
        IMetadataParser metadataParser,
        IHintManager hintManager,
        INameShortener nameShortener,
        IGuidHandler guidHandler)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mcpServer = mcpServer ?? throw new ArgumentNullException(nameof(mcpServer));
        _odataClient = odataClient ?? throw new ArgumentNullException(nameof(odataClient));
        _metadataParser = metadataParser ?? throw new ArgumentNullException(nameof(metadataParser));
        _hintManager = hintManager ?? throw new ArgumentNullException(nameof(hintManager));
        _nameShortener = nameShortener ?? throw new ArgumentNullException(nameof(nameShortener));
        _guidHandler = guidHandler ?? throw new ArgumentNullException(nameof(guidHandler));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        
        _logger.LogInformation("Initializing OData MCP bridge...");
        
        // Load hints
        await _hintManager.LoadFromFileAsync(_config.HintsFile);
        
        // Set CLI hint if provided
        if (!string.IsNullOrEmpty(_config.Hint))
        {
            _hintManager.SetCliHint(_config.Hint);
        }
        
        // Fetch and parse metadata
        _logger.LogInformation("Fetching metadata from {ServiceUrl}", _config.ServiceUrl);
        _metadata = await _odataClient.GetMetadataAsync(cancellationToken);
        
        // Generate tools
        _logger.LogInformation("Generating MCP tools from metadata...");
        await GenerateToolsAsync();
        
        _initialized = true;
        _logger.LogInformation("OData MCP bridge initialized successfully");
    }

    public async Task RunAsync(ITransport transport, CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Bridge must be initialized before running");
        }
        
        _logger.LogInformation("Starting OData MCP bridge with {Transport} transport", transport.GetType().Name);
        
        // Start the transport with message handling from the MCP server
        await transport.StartAsync(async msg => await _mcpServer.HandleMessageAsync(msg), cancellationToken);
    }

    public TraceInfo GetTraceInfo()
    {
        var traceInfo = new TraceInfo
        {
            ServiceUrl = _config.ServiceUrl ?? string.Empty,
            ODataVersion = _metadata?.ODataVersion,
            EntitySetCount = _metadata?.EntitySets.Count ?? 0,
            FunctionImportCount = _metadata?.FunctionImports.Count ?? 0,
            GeneratedTools = _mcpServer.GetTools().Select(t => t.Name).ToList(),
            Configuration = new Dictionary<string, object>
            {
                ["Transport"] = _config.Transport,
                ["ReadOnly"] = _config.IsReadOnly,
                ["ToolShrink"] = _config.ToolShrink,
                ["MaxItems"] = _config.MaxItems,
                ["MaxResponseSize"] = _config.MaxResponseSize
            }
        };
        
        return traceInfo;
    }

    private async Task<McpMessage?> HandleMessageAsync(McpMessage message)
    {
        return await _mcpServer.HandleMessageAsync(message);
    }

    private async Task GenerateToolsAsync()
    {
        if (_metadata == null) return;
        
        // Generate service info tool
        GenerateServiceInfoTool();
        
        // Generate entity set tools
        foreach (var entitySet in _metadata.EntitySets.Values)
        {
            if (ShouldIncludeEntity(entitySet.Name))
            {
                GenerateEntitySetTools(entitySet);
            }
        }
        
        // Generate function import tools
        foreach (var function in _metadata.FunctionImports.Values)
        {
            if (ShouldIncludeFunction(function.Name) && _config.IsOperationEnabled(Constants.Constants.OpAction))
            {
                GenerateFunctionTool(function);
            }
        }
        
        await Task.CompletedTask;
    }

    private void GenerateServiceInfoTool()
    {
        // TODO: Implement service info tool generation
        _logger.LogDebug("Generated service info tool");
    }

    private void GenerateEntitySetTools(EntitySet entitySet)
    {
        // TODO: Implement entity set tool generation
        _logger.LogDebug("Generated tools for entity set: {EntitySet}", entitySet.Name);
    }

    private void GenerateFunctionTool(FunctionImport function)
    {
        // TODO: Implement function tool generation
        _logger.LogDebug("Generated tool for function: {Function}", function.Name);
    }

    private bool ShouldIncludeEntity(string entityName)
    {
        if (_config.Entities == null || _config.Entities.Length == 0)
            return true;
            
        return _config.Entities.Any(pattern => MatchesPattern(entityName, pattern));
    }

    private bool ShouldIncludeFunction(string functionName)
    {
        if (_config.Functions == null || _config.Functions.Length == 0)
            return true;
            
        return _config.Functions.Any(pattern => MatchesPattern(functionName, pattern));
    }

    private bool MatchesPattern(string name, string pattern)
    {
        if (pattern == name) return true;
        
        // Simple wildcard support
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        
        if (pattern.StartsWith("*"))
        {
            var suffix = pattern[1..];
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }
}