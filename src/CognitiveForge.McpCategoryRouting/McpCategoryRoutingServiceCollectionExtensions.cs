using CognitiveForge.McpCategoryRouting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MCP category-based routing in the dependency injection container.
/// </summary>
public static class McpCategoryRoutingServiceCollectionExtensions
{
    /// <summary>
    /// Registers category-based filtering for MCP primitives.
    /// </summary>
    /// <remarks>
    /// This extension method configures the MCP server to filter tools, prompts, and resources
    /// based on the category route value. Consumers should use the standard
    /// <c>app.MapMcp("path/{category}")</c> with a route parameter to specify which category route each endpoint serves.
    /// </remarks>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="configure">Optional delegate to configure <see cref="McpCategoryRoutingOptions"/>.</param>
    /// <returns>The MCP server builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
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

