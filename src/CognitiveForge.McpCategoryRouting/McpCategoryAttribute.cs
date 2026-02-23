namespace CognitiveForge.McpCategoryRouting;

/// <summary>
/// Attribute that assigns one or more categories to MCP primitives (tools, prompts, resources).
/// </summary>
/// <remarks>
/// When applied to an MCP primitive, this attribute indicates which category routes the primitive
/// should be visible on. Multiple <see cref="McpCategoryAttribute"/> attributes can be applied to
/// a single primitive to assign it to multiple categories. If no <see cref="McpCategoryAttribute"/>
/// is present, the library falls back to <see cref="System.ComponentModel.CategoryAttribute"/>.
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class McpCategoryAttribute(string category) : Attribute
{
    /// <summary>
    /// Gets the category name.
    /// </summary>
    public string Category { get; } = category ?? throw new ArgumentNullException(nameof(category));
}

