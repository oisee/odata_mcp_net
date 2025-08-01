using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ODataMcp.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the MCP server
/// </summary>
public class McpServerIntegrationTests : IDisposable
{
    private readonly string _executablePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServerIntegrationTests()
    {
        // Find the executable - adjust path based on where tests are run from
        var projectRoot = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(projectRoot, "odata_mcp_net.sln")) && projectRoot != Path.GetPathRoot(projectRoot))
        {
            projectRoot = Directory.GetParent(projectRoot)!.FullName;
        }
        
        _executablePath = Path.Combine(projectRoot, "src", "ODataMcp", "bin", "Debug", "net8.0", "odata-mcp");
        if (OperatingSystem.IsWindows())
        {
            _executablePath += ".exe";
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    [Fact(Skip = "Integration test - requires built executable")]
    public async Task Should_Complete_Full_MCP_Handshake()
    {
        // Arrange
        var process = StartMcpServer("https://services.odata.org/V4/OData/OData.svc/");
        
        try
        {
            // Send initialize request
            var initRequest = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    clientInfo = new
                    {
                        name = "test-client",
                        version = "1.0.0"
                    }
                }
            };

            // Act - Initialize
            var initResponse = await SendRequestAsync(process, initRequest);

            // Assert - Initialize response
            initResponse.Should().NotBeNull();
            initResponse.Should().ContainKey("result");
            var initResult = initResponse["result"] as JsonElement?;
            initResult?.GetProperty("protocolVersion").GetString().Should().Be("2024-11-05");
            initResult?.GetProperty("serverInfo").GetProperty("name").GetString().Should().Be("odata-mcp");

            // Act - List tools
            var toolsRequest = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list"
            };
            
            var toolsResponse = await SendRequestAsync(process, toolsRequest);

            // Assert - Tools response
            toolsResponse.Should().NotBeNull();
            toolsResponse.Should().ContainKey("result");
            var toolsResult = toolsResponse["result"] as JsonElement?;
            var tools = toolsResult?.GetProperty("tools").EnumerateArray().ToList();
            tools.Should().NotBeNull();
            tools!.Count.Should().BeGreaterThan(0);

            // Verify we have expected tools
            var toolNames = tools.Select(t => t.GetProperty("name").GetString()).ToList();
            toolNames.Should().Contain("odata_service_info");
            toolNames.Should().Contain(name => name!.StartsWith("filter_"));
            toolNames.Should().Contain(name => name!.StartsWith("get_"));
        }
        finally
        {
            process.Kill();
            process.WaitForExit(1000);
            process.Dispose();
        }
    }

    [Fact(Skip = "Integration test - requires built executable")]
    public async Task Should_Execute_Tool_Call()
    {
        // Arrange
        var process = StartMcpServer("https://services.odata.org/V4/OData/OData.svc/");
        
        try
        {
            // Initialize first
            await SendRequestAsync(process, new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize"
            });

            // Act - Call service info tool
            var toolCallRequest = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = "odata_service_info",
                    arguments = new { }
                }
            };

            var response = await SendRequestAsync(process, toolCallRequest);

            // Assert
            response.Should().NotBeNull();
            response.Should().ContainKey("result");
            var result = response["result"] as JsonElement?;
            var content = result?.GetProperty("content").EnumerateArray().FirstOrDefault();
            content?.GetProperty("type").GetString().Should().Be("text");
            
            var textContent = content?.GetProperty("text").GetString();
            textContent.Should().NotBeNullOrEmpty();
            
            // Parse the JSON content
            var serviceInfo = JsonDocument.Parse(textContent!);
            serviceInfo.RootElement.GetProperty("serviceUrl").GetString().Should().Contain("odata.org");
            serviceInfo.RootElement.TryGetProperty("entitySets", out _).Should().BeTrue();
        }
        finally
        {
            process.Kill();
            process.WaitForExit(1000);
            process.Dispose();
        }
    }

    [Fact(Skip = "Integration test - requires built executable")]
    public async Task Should_Handle_Invalid_Tool_Name()
    {
        // Arrange
        var process = StartMcpServer("https://services.odata.org/V4/OData/OData.svc/");
        
        try
        {
            // Initialize first
            await SendRequestAsync(process, new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize"
            });

            // Act - Call non-existent tool
            var toolCallRequest = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/call",
                @params = new
                {
                    name = "non_existent_tool",
                    arguments = new { }
                }
            };

            var response = await SendRequestAsync(process, toolCallRequest);

            // Assert - Should return error
            response.Should().NotBeNull();
            response.Should().ContainKey("error");
            var error = response["error"] as JsonElement?;
            error?.GetProperty("code").GetInt32().Should().Be(-32603); // Internal error
            error?.GetProperty("message").GetString().Should().Contain("Invalid tool name format");
        }
        finally
        {
            process.Kill();
            process.WaitForExit(1000);
            process.Dispose();
        }
    }

    private Process StartMcpServer(string serviceUrl)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = $"--service \"{serviceUrl}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start process: {_executablePath}");
        }

        // Give the process time to start
        Thread.Sleep(500);

        if (process.HasExited)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"Process exited immediately. Error: {error}");
        }

        return process;
    }

    private async Task<Dictionary<string, object?>> SendRequestAsync(Process process, object request)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        await process.StandardInput.WriteLineAsync(json);
        await process.StandardInput.FlushAsync();

        // Read response with timeout
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var responseTask = process.StandardOutput.ReadLineAsync();
        
        var response = await responseTask.WaitAsync(cts.Token);
        if (string.IsNullOrEmpty(response))
        {
            throw new InvalidOperationException("No response received");
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(response, _jsonOptions)!;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}