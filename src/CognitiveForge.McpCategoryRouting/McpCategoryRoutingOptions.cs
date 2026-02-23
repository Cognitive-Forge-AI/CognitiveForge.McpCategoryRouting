namespace CognitiveForge.McpCategoryRouting;

public sealed class McpCategoryRoutingOptions
{
    public string RouteValueName { get; set; } = "category";

    public bool CaseInsensitive { get; set; } = true;

    public bool DisableFilteringWhenCategoryMissing { get; set; } = true;

    public UncategorizedBehavior UncategorizedBehavior { get; set; } = UncategorizedBehavior.ExcludeAlways;

    public string FallbackCategory { get; set; } = "mcp";
}

