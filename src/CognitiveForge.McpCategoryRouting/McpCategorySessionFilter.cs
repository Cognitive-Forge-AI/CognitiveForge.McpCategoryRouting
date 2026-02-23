using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

namespace CognitiveForge.McpCategoryRouting;

internal sealed class McpCategorySessionFilter(IOptions<McpCategoryRoutingOptions> options)
    : IPostConfigureOptions<HttpServerTransportOptions>
{
    public void PostConfigure(string? name, HttpServerTransportOptions transportOptions)
    {
        ArgumentNullException.ThrowIfNull(transportOptions);

        var originalConfigureSessionOptions = transportOptions.ConfigureSessionOptions;

        transportOptions.ConfigureSessionOptions = async (httpContext, serverOptions, cancellationToken) =>
        {
            if (originalConfigureSessionOptions is not null)
            {
                await originalConfigureSessionOptions(httpContext, serverOptions, cancellationToken);
            }

            var routingOptions = options.Value;
            var category = GetRouteCategory(httpContext, routingOptions);

            if (string.IsNullOrWhiteSpace(category) && routingOptions.DisableFilteringWhenCategoryMissing)
            {
                return;
            }

            FilterTools(serverOptions, category, routingOptions);
            FilterPrompts(serverOptions, category, routingOptions);
            FilterResources(serverOptions, category, routingOptions);
        };
    }

    private static string? GetRouteCategory(HttpContext httpContext, McpCategoryRoutingOptions routingOptions)
    {
        if (!httpContext.Request.RouteValues.TryGetValue(routingOptions.RouteValueName, out var rawCategory) || rawCategory is null)
        {
            return null;
        }

        return Convert.ToString(rawCategory)?.Trim();
    }

    private static void FilterTools(
        McpServerOptions serverOptions,
        string? category,
        McpCategoryRoutingOptions routingOptions)
    {
        var tools = serverOptions.ToolCollection;
        if (tools is null || tools.Count == 0)
        {
            return;
        }

        var filtered = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in tools)
        {
            if (IsAllowed(tool.GetCategories(), category, routingOptions))
            {
                filtered.Add(tool);
            }
        }

        serverOptions.ToolCollection = filtered;
    }

    private static void FilterPrompts(
        McpServerOptions serverOptions,
        string? category,
        McpCategoryRoutingOptions routingOptions)
    {
        var prompts = serverOptions.PromptCollection;
        if (prompts is null || prompts.Count == 0)
        {
            return;
        }

        var filtered = new McpServerPrimitiveCollection<McpServerPrompt>(StringComparer.OrdinalIgnoreCase);
        foreach (var prompt in prompts)
        {
            if (IsAllowed(prompt.GetCategories(), category, routingOptions))
            {
                filtered.Add(prompt);
            }
        }

        serverOptions.PromptCollection = filtered;
    }

    private static void FilterResources(
        McpServerOptions serverOptions,
        string? category,
        McpCategoryRoutingOptions routingOptions)
    {
        var resources = serverOptions.ResourceCollection;
        if (resources is null || resources.Count == 0)
        {
            return;
        }

        var filtered = new McpServerResourceCollection();
        foreach (var resource in resources)
        {
            if (IsAllowed(resource.GetCategories(), category, routingOptions))
            {
                filtered.Add(resource);
            }
        }

        serverOptions.ResourceCollection = filtered;
    }

    private static bool IsAllowed(
        IReadOnlyList<string> categories,
        string? category,
        McpCategoryRoutingOptions routingOptions)
    {
        if (categories.Count == 0)
        {
            return routingOptions.UncategorizedBehavior switch
            {
                UncategorizedBehavior.IncludeAlways => true,
                UncategorizedBehavior.ExcludeAlways => false,
                UncategorizedBehavior.FallbackRoute => EqualsWithOptions(
                    category,
                    routingOptions.FallbackCategory,
                    routingOptions.CaseInsensitive),
                _ => false
            };
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        foreach (var itemCategory in categories)
        {
            if (EqualsWithOptions(itemCategory, category, routingOptions.CaseInsensitive))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EqualsWithOptions(string? left, string? right, bool caseInsensitive)
        => string.Equals(
            left,
            right,
            caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
