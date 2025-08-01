using CommandLine;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ODataMcp.Core.Configuration;
using ODataMcp.Core.Mcp;
using ODataMcp.Core.Transport;

namespace ODataMcp;

/// <summary>
/// Main entry point for the OData MCP bridge
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Load .env file if it exists
        Env.Load();

        // Parse command line arguments
        return await Parser.Default.ParseArguments<CommandLineOptions>(args)
            .MapResult<CommandLineOptions, Task<int>>(
                async (CommandLineOptions opts) => await RunBridge(opts),
                errs => Task.FromResult(1));
    }

    static async Task<int> RunBridge(CommandLineOptions options)
    {
        // Create configuration from command line options
        var config = new ODataBridgeConfiguration
        {
            ServiceUrl = options.ServiceUrl ?? options.PositionalServiceUrl ?? Environment.GetEnvironmentVariable("ODATA_URL") ?? "",
            Username = options.Username ?? Environment.GetEnvironmentVariable("ODATA_USERNAME"),
            Password = options.Password ?? options.PasswordAlias ?? Environment.GetEnvironmentVariable("ODATA_PASSWORD"),
            CookieFile = options.CookieFile,
            CookieString = options.CookieString,
            ToolShrink = options.ToolShrink,
            Entities = options.Entities?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            ReadOnly = options.ReadOnly || options.ReadOnlyShort,
            ReadOnlyButFunctions = options.ReadOnlyButFunctions || options.ReadOnlyButFunctionsShort,
            Verbose = options.Verbose || options.Debug,
            Trace = options.Trace,
            TraceMcp = options.TraceMcp,
            ClaudeCodeFriendly = options.ClaudeCodeFriendly,
            MaxItems = options.MaxItems,
            PaginationHints = options.PaginationHints,
            LegacyDates = options.NoLegacyDates ? false : options.LegacyDates,
            VerboseErrors = options.VerboseErrors
        };

        // Validate configuration
        if (string.IsNullOrWhiteSpace(config.ServiceUrl))
        {
            Console.Error.WriteLine("Error: OData service URL not provided. Use --service flag, positional argument, or ODATA_URL environment variable");
            return 1;
        }

        // Set up dependency injection
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(config.Verbose ? LogLevel.Debug : LogLevel.Information);
            
            // Only add console logging if not using stdio transport
            if (options.Transport != "stdio")
            {
                builder.AddConsole();
            }
            else
            {
                // For STDIO transport, disable console logging to avoid interference
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Warning);
            }
        });

        // Add HTTP client
        services.AddHttpClient();

        // Add configuration and services
        services.AddSingleton(config);
        services.AddSingleton<SimpleMcpServerV2>();
        services.AddSingleton<SimpleStdioTransport>();

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // Handle trace mode
        if (config.Trace)
        {
            return await RunTraceMode(serviceProvider, config);
        }

        try
        {
            // Create server and transport
            var server = serviceProvider.GetRequiredService<SimpleMcpServerV2>();
            var transport = serviceProvider.GetRequiredService<SimpleStdioTransport>();

            // Set up cancellation
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                logger.LogInformation("Shutdown requested...");
            };

            // Run the server
            logger.LogInformation("Starting OData MCP bridge for {ServiceUrl}", config.ServiceUrl);
            await transport.RunAsync(cts.Token);
            
            logger.LogInformation("OData MCP bridge stopped.");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error occurred");
            
            if (config.VerboseErrors)
            {
                Console.Error.WriteLine($"\n--- FATAL ERROR ---");
                Console.Error.WriteLine($"Exception: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                Console.Error.WriteLine($"Stack Trace:\n{ex.StackTrace}");
                Console.Error.WriteLine($"-------------------");
            }
            else
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
            
            return 1;
        }
    }

    static async Task<int> RunTraceMode(IServiceProvider serviceProvider, ODataBridgeConfiguration config)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Running in trace mode...");

        try
        {
            var server = serviceProvider.GetRequiredService<SimpleMcpServerV2>();
            
            // Initialize to get metadata
            var initResult = await server.InitializeAsync();
            var toolsResult = await server.ListToolsAsync();
            
            // Extract data from the dynamic results
            dynamic init = initResult;
            dynamic toolsObj = toolsResult;
            var tools = toolsObj.tools as IEnumerable<dynamic>;
            
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("🔍 OData MCP Bridge Trace Information");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine();
            
            Console.WriteLine($"Service URL: {config.ServiceUrl}");
            Console.WriteLine($"Protocol Version: {init.protocolVersion}");
            Console.WriteLine($"Server: {init.serverInfo.name} v{init.serverInfo.version}");
            Console.WriteLine($"Total Tools: {tools?.Count() ?? 0}");
            Console.WriteLine();
            
            Console.WriteLine("Configuration:");
            Console.WriteLine($"  Read-Only: {config.ReadOnly}");
            Console.WriteLine($"  Tool Shrink: {config.ToolShrink}");
            Console.WriteLine($"  Max Items: {config.MaxItems}");
            Console.WriteLine($"  Entities Filter: {string.Join(", ", config.Entities ?? new List<string> { "*" })}");
            Console.WriteLine();
            
            Console.WriteLine("Generated Tools:");
            if (tools != null)
            {
                foreach (dynamic tool in tools.OrderBy(t => (string)t.name))
                {
                    Console.WriteLine($"  - {tool.name}: {tool.description}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("✅ Trace complete - MCP bridge initialized successfully");
            Console.WriteLine("💡 Use without --trace to start the actual MCP server");
            Console.WriteLine(new string('=', 80));
            
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during trace mode");
            Console.Error.WriteLine($"Trace error: {ex.Message}");
            return 1;
        }
    }
}