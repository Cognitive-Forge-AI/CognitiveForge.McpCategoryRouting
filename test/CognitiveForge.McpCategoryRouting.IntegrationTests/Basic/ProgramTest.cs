extern alias Basic;

using BasicProgram = Basic::Program;

namespace CognitiveForge.McpCategoryRouting.IntegrationTests.Basic;

public class ProgramTest
{
    [Fact]
    public async Task Should_start()
    {
        await using var app = new McpApplicationFactory<BasicProgram>();
        await using var mcpClient = await app.CreateMcpClient();
    }

    [Fact]
    public async Task When_connecting_to_analytics_it_should_only_list_analytics_tools()
    {
        await using var app = new McpApplicationFactory<BasicProgram>();
        await using var mcpClient = await app.CreateMcpClient("/analytics");

        var tools = await mcpClient.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("get_summary", tools[0].Name);
    }

    [Fact]
    public async Task When_connecting_to_ops_it_should_only_list_ops_tools()
    {
        await using var app = new McpApplicationFactory<BasicProgram>();
        await using var mcpClient = await app.CreateMcpClient("/ops");

        var tools = await mcpClient.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("get_health", tools[0].Name);
    }

    [Fact]
    public async Task When_connecting_to_unknown_category_it_should_list_no_tools()
    {
        await using var app = new McpApplicationFactory<BasicProgram>();
        await using var mcpClient = await app.CreateMcpClient("/unknown");

        var tools = await mcpClient.ListToolsAsync();

        Assert.Empty(tools);
    }

    [Fact]
    public async Task When_connecting_with_different_case_it_should_match_case_insensitively()
    {
        await using var app = new McpApplicationFactory<BasicProgram>();
        await using var mcpClient = await app.CreateMcpClient("/ANALYTICS");

        var tools = await mcpClient.ListToolsAsync();

        Assert.Single(tools);
        Assert.Equal("get_summary", tools[0].Name);
    }
}
