using CognitiveForge.McpCategoryRouting;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly)
    .AddCategoryFilters() // Add the category filters
;

var app = builder.Build();

app.MapGet("/", () => """
Use the MCP endpoint with a category in the path:

- /analytics -> analytics tools
- /ops -> operations tools
""");

app.MapMcp("/{category}"); // Map the MCP endpoint with the category in the path

app.Run();

[McpServerToolType]
[McpCategory("analytics")]
public static class AnalyticsTools
{
    [McpServerTool, Description("Returns a tiny analytics summary.")]
    public static string GetSummary()
        => "Users: 42, ConversionRate: 3.1%";
}

[McpServerToolType]
[McpCategory("ops")]
public static class OperationsTools
{
    [McpServerTool, Description("Returns a simple health status.")]
    public static string GetHealth()
        => "OK";
}

[McpServerToolType]
public static class CommonTools
{
    [McpServerTool, Description("No category attribute: hidden because uncategorized tools are excluded by default (UncategorizedBehavior.ExcludeAlways).")]
    public static string GetServerName()
        => "basic-sample";
}
