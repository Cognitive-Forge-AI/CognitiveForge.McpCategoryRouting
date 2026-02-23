using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CognitiveForge.McpCategoryRouting;

public static class McpServerPrimitiveCategoryExtensions
{
    public static IReadOnlyList<string> GetCategories(this IMcpServerPrimitive primitive)
    {
        ArgumentNullException.ThrowIfNull(primitive);

        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMcpCategory = false;

        foreach (object metadata in primitive.Metadata)
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
            foreach (object metadata in primitive.Metadata)
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

