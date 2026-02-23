using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace CognitiveForge.McpCategoryRouting.IntegrationTests;

public class McpApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    public Task<McpClient> CreateMcpClient(string path = "/mcp", CancellationToken? cancellationToken = null)
    {
        var ct = cancellationToken ?? CancellationToken.None;
        var http = CreateClient();
        ArgumentNullException.ThrowIfNull(http.BaseAddress, nameof(http.BaseAddress));

        var loggerFactory = Services.GetRequiredService<ILoggerFactory>();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(http.BaseAddress, path),
            },
            http,
            loggerFactory: loggerFactory,
            ownsHttpClient: false
        );
        return McpClient.CreateAsync(transport, new McpClientOptions(), loggerFactory, ct);
    }
}
