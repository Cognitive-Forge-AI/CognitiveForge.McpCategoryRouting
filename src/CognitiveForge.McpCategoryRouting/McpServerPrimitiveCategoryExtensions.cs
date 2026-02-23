using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CognitiveForge.McpCategoryRouting;

/// <summary>
/// Extension methods for extracting categories from MCP primitives.
/// </summary>
public static class McpServerPrimitiveCategoryExtensions
{
    /// <summary>
    /// Extracts the list of categories assigned to an MCP primitive.
    /// </summary>
    /// <remarks>
    /// This method reads categories from the primitive's metadata, applying the following resolution order:
    /// <list type="number">
    /// <item>If one or more <see cref="McpCategoryAttribute"/> attributes are present, their category
    /// values are used (duplicates are removed in a case-insensitive manner).</item>
    /// <item>If no <see cref="McpCategoryAttribute"/> is found, the last
    /// <see cref="System.ComponentModel.CategoryAttribute"/> is used as a fallback.</item>
    /// <item>If neither attribute type is present, an empty list is returned.</item>
    /// </list>
    /// </remarks>
    /// <param name="primitive">The MCP primitive (tool, prompt, or resource).</param>
    /// <returns>A read-only list of categories. Empty if the primitive has no category assignment.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="primitive"/> is <c>null</c>.</exception>
    public static IReadOnlyList<string> GetCategories(this IMcpServerPrimitive primitive)
    {
        ArgumentNullException.ThrowIfNull(primitive);

        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMcpCategory = false;

        foreach (var metadata in primitive.Metadata)
        {
            if (metadata is McpCategoryAttribute mcpCategory)
            {
                if (!string.IsNullOrWhiteSpace(mcpCategory.Category))
                {
                    categories.Add(mcpCategory.Category.Trim());
                }

                hasMcpCategory = true;
            }
        }

        if (!hasMcpCategory)
        {
            CategoryAttribute? lastCategory = null;
            foreach (var metadata in primitive.Metadata)
            {
                if (metadata is CategoryAttribute category)
                {
                    lastCategory = category;
                }
            }

            if (lastCategory is not null && !string.IsNullOrWhiteSpace(lastCategory.Category))
            {
                categories.Add(lastCategory.Category.Trim());
            }
        }

        return [.. categories];
    }
}

