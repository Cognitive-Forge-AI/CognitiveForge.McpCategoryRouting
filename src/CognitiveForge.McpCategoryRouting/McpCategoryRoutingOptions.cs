namespace CognitiveForge.McpCategoryRouting;

/// <summary>
/// Options for configuring MCP category-based routing behavior.
/// </summary>
public sealed class McpCategoryRoutingOptions
{
    /// <summary>
    /// Gets or sets the name of the route value used to determine the active category.
    /// </summary>
    /// <remarks>
    /// Default is "category". This corresponds to the route parameter name in
    /// <c>app.MapMcp("/{category}")</c>.
    /// </remarks>
    public string RouteValueName { get; set; } = "category";

    /// <summary>
    /// Gets or sets a value indicating whether category matching is case-insensitive.
    /// </summary>
    /// <remarks>
    /// Default is <c>true</c>. When <c>true</c>, category names like "analytics" and "Analytics"
    /// are treated as equivalent.
    /// </remarks>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether filtering is disabled when the category
    /// route value is missing or empty.
    /// </summary>
    /// <remarks>
    /// Default is <c>true</c>. When <c>true</c> and no category is present, all primitives
    /// are included (no filtering occurs). When <c>false</c>, filtering applies even without
    /// a category value, meaning only uncategorized primitives may be visible depending on
    /// <see cref="UncategorizedBehavior"/>.
    /// </remarks>
    public bool DisableFilteringWhenCategoryMissing { get; set; } = true;

    /// <summary>
    /// Gets or sets the behavior for MCP primitives that have no category attribute.
    /// </summary>
    /// <remarks>
    /// Default is <see cref="UncategorizedBehavior.ExcludeAlways"/>. Controls visibility of
    /// primitives without <see cref="McpCategoryAttribute"/> or <see cref="System.ComponentModel.CategoryAttribute"/>.
    /// </remarks>
    public UncategorizedBehavior UncategorizedBehavior { get; set; } = UncategorizedBehavior.ExcludeAlways;

    /// <summary>
    /// Gets or sets the fallback category used when <see cref="UncategorizedBehavior"/> is
    /// set to <see cref="UncategorizedBehavior.FallbackRoute"/>.
    /// </summary>
    /// <remarks>
    /// Default is "mcp". Uncategorized primitives are visible only on routes where the category
    /// matches this value (with case sensitivity controlled by <see cref="CaseInsensitive"/>).
    /// </remarks>
    public string FallbackCategory { get; set; } = "mcp";
}

