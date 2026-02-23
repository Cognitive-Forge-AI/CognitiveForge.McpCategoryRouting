# CognitiveForge.McpCategoryRouting

Category-based routing for .NET MCP servers. Expose different subsets of tools, prompts, and resources on different URL paths from a single MCP server.

## Overview

This library hooks into the [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) SDK for .NET and filters MCP primitives per HTTP session based on a route parameter. For example, `app.MapMcp("/{category}")` lets requests to `/analytics` see only analytics tools while `/ops` sees only operations tools.

Filtering happens at session connection time via `IPostConfigureOptions<HttpServerTransportOptions>`, so there is no per-message overhead.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Installation

<!-- TODO: Update once the package is published to NuGet -->

```bash
dotnet add package CognitiveForge.McpCategoryRouting
```

## Quick Start

```csharp
using CognitiveForge.McpCategoryRouting;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly)
    .AddCategoryFilters(); // enable category filtering

var app = builder.Build();
app.MapMcp("/{category}"); // standard MapMcp with a route parameter
app.Run();

// Tools categorized as "analytics" — visible at /analytics
[McpServerToolType]
[McpCategory("analytics")]
public static class AnalyticsTools
{
    [McpServerTool, Description("Returns a tiny analytics summary.")]
    public static string GetSummary() => "Users: 42, ConversionRate: 3.1%";
}

// Tools categorized as "ops" — visible at /ops
[McpServerToolType]
[McpCategory("ops")]
public static class OperationsTools
{
    [McpServerTool, Description("Returns a simple health status.")]
    public static string GetHealth() => "OK";
}
```

See the [samples/Basic](samples/Basic) project for a complete working example.

## Configuration

Pass an options delegate to `AddCategoryFilters()`:

```csharp
.AddCategoryFilters(options =>
{
    options.RouteValueName = "category";
    options.CaseInsensitive = true;
    options.UncategorizedBehavior = UncategorizedBehavior.ExcludeAlways;
});
```

| Property | Default | Description |
|---|---|---|
| `RouteValueName` | `"category"` | ASP.NET Core route parameter name to read |
| `CaseInsensitive` | `true` | Case-insensitive category matching |
| `DisableFilteringWhenCategoryMissing` | `true` | Skip filtering when the route has no category segment (expose all primitives) |
| `UncategorizedBehavior` | `ExcludeAlways` | How to handle primitives with no category (see below) |
| `FallbackCategory` | `"mcp"` | Route value that activates uncategorized primitives when using `FallbackRoute` behavior |

## Category Assignment

Apply `[McpCategory]` to a class (affects all tools in the class) or to individual methods:

```csharp
[McpCategory("analytics")]
[McpCategory("reporting")] // multiple categories allowed
public static class MultiCategoryTools { ... }
```

`[System.ComponentModel.Category]` is also supported as a fallback when `[McpCategory]` is not present.

## Uncategorized Primitives

The `UncategorizedBehavior` enum controls visibility of primitives that have no category:

| Value | Behavior |
|---|---|
| `IncludeAlways` | Always visible on every route |
| `ExcludeAlways` | Never visible (default) |
| `FallbackRoute` | Visible only when the route matches `FallbackCategory` |

## Building

```bash
dotnet build CognitiveForge.McpCategoryRouting.slnx
```

## Testing

```bash
# Run all tests
dotnet test CognitiveForge.McpCategoryRouting.slnx

# Run unit tests only
dotnet test src/CognitiveForge.McpCategoryRouting.Tests

# Run integration tests only
dotnet test src/CognitiveForge.McpCategoryRouting.IntegrationTests

# Run a single test
dotnet test src/CognitiveForge.McpCategoryRouting.Tests --filter "FullyQualifiedName~GetCategories_should_return_empty"
```

## Contributing

<!-- TODO: Add contribution guidelines -->

Contributions are welcome! Please open an issue or submit a pull request.

## License

This project is licensed under the [MIT License](LICENSE).
