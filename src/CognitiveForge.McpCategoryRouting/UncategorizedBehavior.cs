namespace CognitiveForge.McpCategoryRouting;

/// <summary>
/// Controls how MCP primitives without a category attribute are handled during routing.
/// </summary>
public enum UncategorizedBehavior
{
    /// <summary>
    /// Uncategorized primitives are included in every category route.
    /// </summary>
    IncludeAlways,

    /// <summary>
    /// Uncategorized primitives are excluded from all category routes.
    /// </summary>
    ExcludeAlways,

    /// <summary>
    /// Uncategorized primitives are only visible on the route matching
    /// <see cref="McpCategoryRoutingOptions.FallbackCategory"/>.
    /// </summary>
    FallbackRoute
}

