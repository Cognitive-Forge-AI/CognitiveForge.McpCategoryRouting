extern alias Basic;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using BasicProgram = Basic::Program;

namespace CognitiveForge.McpCategoryRouting.IntegrationTests;

public class UncategorizedBehaviorTest
{
    private sealed class UncategorizedBehaviorFactory(
        UncategorizedBehavior behavior,
        string fallbackCategory = "mcp") : McpApplicationFactory<BasicProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<McpCategoryRoutingOptions>(opts =>
                {
                    opts.UncategorizedBehavior = behavior;
                    opts.FallbackCategory = fallbackCategory;
                });
            });
        }
    }

    public class IncludeAlways
    {
        [Fact]
        public async Task When_behavior_is_include_always_it_should_show_uncategorized_tools_on_any_category()
        {
            await using var app = new UncategorizedBehaviorFactory(UncategorizedBehavior.IncludeAlways);
            await using var mcpClient = await app.CreateMcpClient("/analytics");

            var tools = await mcpClient.ListToolsAsync();
            var toolNames = tools.Select(t => t.Name).ToList();

            Assert.Contains("get_summary", toolNames);
            Assert.Contains("get_server_name", toolNames);
            Assert.DoesNotContain("get_health", toolNames);
        }

        [Fact]
        public async Task When_behavior_is_include_always_it_should_show_uncategorized_tools_on_unknown_category()
        {
            await using var app = new UncategorizedBehaviorFactory(UncategorizedBehavior.IncludeAlways);
            await using var mcpClient = await app.CreateMcpClient("/unknown");

            var tools = await mcpClient.ListToolsAsync();
            var toolNames = tools.Select(t => t.Name).ToList();

            Assert.Single(toolNames);
            Assert.Contains("get_server_name", toolNames);
        }
    }

    public class FallbackRoute
    {
        [Fact]
        public async Task When_behavior_is_fallback_route_it_should_show_uncategorized_tools_on_fallback_category()
        {
            await using var app = new UncategorizedBehaviorFactory(UncategorizedBehavior.FallbackRoute, "mcp");
            await using var mcpClient = await app.CreateMcpClient("/mcp");

            var tools = await mcpClient.ListToolsAsync();
            var toolNames = tools.Select(t => t.Name).ToList();

            Assert.Contains("get_server_name", toolNames);
        }

        [Fact]
        public async Task When_behavior_is_fallback_route_it_should_hide_uncategorized_tools_on_other_categories()
        {
            await using var app = new UncategorizedBehaviorFactory(UncategorizedBehavior.FallbackRoute, "mcp");
            await using var mcpClient = await app.CreateMcpClient("/analytics");

            var tools = await mcpClient.ListToolsAsync();
            var toolNames = tools.Select(t => t.Name).ToList();

            Assert.Contains("get_summary", toolNames);
            Assert.DoesNotContain("get_server_name", toolNames);
        }
    }
}
