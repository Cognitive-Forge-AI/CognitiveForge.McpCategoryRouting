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
}
