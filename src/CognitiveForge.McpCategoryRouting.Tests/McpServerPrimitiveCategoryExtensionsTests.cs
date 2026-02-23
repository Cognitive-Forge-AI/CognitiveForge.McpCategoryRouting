using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CognitiveForge.McpCategoryRouting.Tests;

public class McpServerPrimitiveCategoryExtensionsTests
{
    [Fact]
    public void GetCategories_should_return_empty_when_no_category_attributes_exist()
    {
        var primitive = new FakePrimitive([]);

        var categories = primitive.GetCategories();

        Assert.Empty(categories);
    }

    [Fact]
    public void GetCategories_should_prefer_McpCategoryAttribute_and_ignore_CategoryAttribute()
    {
        var primitive = new FakePrimitive(
        [
            new CategoryAttribute("management"),
            new McpCategoryAttribute("analytics"),
            new McpCategoryAttribute("Analytics"),
            new CategoryAttribute("ops")
        ]);

        var categories = primitive.GetCategories();

        Assert.Single(categories);
        Assert.Equal("analytics", categories[0], ignoreCase: true);
    }

    [Fact]
    public void GetCategories_should_use_last_CategoryAttribute_when_no_custom_attribute_exists()
    {
        var primitive = new FakePrimitive(
        [
            new CategoryAttribute("management"),
            new CategoryAttribute("analytics")
        ]);

        var categories = primitive.GetCategories();

        Assert.Single(categories);
        Assert.Equal("analytics", categories[0], ignoreCase: true);
    }

    private sealed class FakePrimitive(IReadOnlyList<object> metadata) : IMcpServerPrimitive
    {
        public string Id => "fake";

        public IReadOnlyList<object> Metadata { get; } = metadata;
    }
}
