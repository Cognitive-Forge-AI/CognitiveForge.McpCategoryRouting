using ModelContextProtocol.Server;
using System.ComponentModel;

namespace CognitiveForge.McpCategoryRouting.Tests;

public class McpServerPrimitiveCategoryExtensionsTest
{
    private sealed class FakePrimitive(IReadOnlyList<object> metadata) : IMcpServerPrimitive
    {
        public string Id => "fake";

        public IReadOnlyList<object> Metadata { get; } = metadata;
    }

    public class GetCategories : McpServerPrimitiveCategoryExtensionsTest
    {
        [Fact]
        public void Should_return_empty_when_no_category_attributes_exist()
        {
            var primitive = new FakePrimitive([]);

            var categories = primitive.GetCategories();

            Assert.Empty(categories);
        }

        [Fact]
        public void Should_prefer_McpCategoryAttribute_and_ignore_CategoryAttribute()
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
        public void Should_use_last_CategoryAttribute_when_no_custom_attribute_exists()
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

        [Fact]
        public void Should_return_category_from_single_McpCategoryAttribute()
        {
            var primitive = new FakePrimitive([new McpCategoryAttribute("analytics")]);

            var categories = primitive.GetCategories();

            Assert.Single(categories);
            Assert.Equal("analytics", categories[0]);
        }

        [Fact]
        public void Should_return_multiple_categories_from_multiple_McpCategoryAttributes()
        {
            var primitive = new FakePrimitive(
            [
                new McpCategoryAttribute("analytics"),
                new McpCategoryAttribute("ops")
            ]);

            var categories = primitive.GetCategories();

            Assert.Equal(2, categories.Count);
            Assert.Contains("analytics", categories, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("ops", categories, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void Should_deduplicate_categories_case_insensitively()
        {
            var primitive = new FakePrimitive(
            [
                new McpCategoryAttribute("Analytics"),
                new McpCategoryAttribute("analytics"),
                new McpCategoryAttribute("ANALYTICS")
            ]);

            var categories = primitive.GetCategories();

            Assert.Single(categories);
        }

        [Fact]
        public void Should_trim_whitespace_from_McpCategoryAttribute()
        {
            var primitive = new FakePrimitive([new McpCategoryAttribute("  analytics  ")]);

            var categories = primitive.GetCategories();

            Assert.Single(categories);
            Assert.Equal("analytics", categories[0]);
        }

        [Fact]
        public void Should_ignore_whitespace_only_McpCategoryAttribute()
        {
            var primitive = new FakePrimitive([new McpCategoryAttribute("   ")]);

            var categories = primitive.GetCategories();

            Assert.Empty(categories);
        }

        [Fact]
        public void Should_trim_whitespace_from_CategoryAttribute()
        {
            var primitive = new FakePrimitive([new CategoryAttribute("  ops  ")]);

            var categories = primitive.GetCategories();

            Assert.Single(categories);
            Assert.Equal("ops", categories[0]);
        }

        [Fact]
        public void Should_ignore_whitespace_only_CategoryAttribute()
        {
            var primitive = new FakePrimitive([new CategoryAttribute("   ")]);

            var categories = primitive.GetCategories();

            Assert.Empty(categories);
        }

        [Fact]
        public void Should_throw_ArgumentNullException_for_null_primitive()
        {
            IMcpServerPrimitive primitive = null!;

            Assert.Throws<ArgumentNullException>(() => primitive.GetCategories());
        }
    }
}
