using CognitiveForge.McpCategoryRouting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

public static class McpCategoryRoutingServiceCollectionExtensions
{
    public static IMcpServerBuilder AddCategoryFilters(
        this IMcpServerBuilder builder,
        Action<McpCategoryRoutingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }
        else
        {
            builder.Services.AddOptions<McpCategoryRoutingOptions>();
        }

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IPostConfigureOptions<HttpServerTransportOptions>,
                McpCategorySessionFilter>());

        return builder;
    }
}

