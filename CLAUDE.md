# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A .NET 10 library that adds category-based routing to MCP (Model Context Protocol) servers. It filters MCP primitives (tools, prompts, resources) per HTTP session based on a route value (e.g., `app.MapMcp("/{category}")`), so a single MCP server can expose different capability subsets on different URL paths.

## Build & Test Commands

```bash
# Build the solution (uses .slnx format)
dotnet build CognitiveForge.McpCategoryRouting.slnx

# Run unit tests
dotnet test src/CognitiveForge.McpCategoryRouting.Tests

# Run a single test by fully-qualified name
dotnet test src/CognitiveForge.McpCategoryRouting.Tests --filter "FullyQualifiedName~McpServerPrimitiveCategoryExtensionsTests.GetCategories_should_return_empty"

# Run the sample app
dotnet run --project samples/Basic
```

## Architecture

The library hooks into the MCP SDK's `HttpServerTransportOptions.ConfigureSessionOptions` callback via `IPostConfigureOptions<HttpServerTransportOptions>`. This lets it filter primitives per-session at connection time, before any MCP messages are processed.

**Registration flow:** `IMcpServerBuilder.AddCategoryFilters()` registers `McpCategorySessionFilter` as a post-configure options service. No endpoint wrapper methods exist — consumers use the standard `app.MapMcp("path/{category}")` with a route parameter.

**Session filtering (`McpCategorySessionFilter`):** When a new MCP session starts, the filter reads the route value from `HttpContext.Request.RouteValues`, then rebuilds each primitive collection (tools, prompts, resources) keeping only items whose categories match.

**Category resolution (`McpServerPrimitiveCategoryExtensions.GetCategories`):** Reads categories from `IMcpServerPrimitive.Metadata`. `McpCategoryAttribute` takes precedence; if none present, falls back to BCL `System.ComponentModel.CategoryAttribute` (last one wins). Both support case-insensitive deduplication.

**Uncategorized behavior (`UncategorizedBehavior` enum):** Controls what happens to primitives with no category attribute — `IncludeAlways`, `ExcludeAlways`, or `FallbackRoute` (visible only when route matches `FallbackCategory`).

## Coding Conventions

See `.augment/rules/` for the full rulesets. Key points:

- Favor primary constructors, especially on record classes
- Favor dependency injection over `new` for non-DTO classes
- Favor integration tests over mock-heavy unit tests; unit test algorithmic code with many code paths
- Favor early return over nested if/else
- Use `?? throw new ArgumentNullException(nameof(parameter))` for constructor null guards
- DI lifetimes: prefer Singleton > Scoped > Transient
- Do not create usage examples unless asked

## Test Conventions

- Test framework: xUnit (with global `using Xunit`)
- Test file naming: `{SourceClass}Test.cs`, mirrors source project structure
- Use nested classes to group tests by method under test
- Test method naming: `Should_{action_description}` for unit tests; `When_{context}_it_should_{outcome}` for complex integration tests
- Use `[Fact]` for single-case, `[Theory]` for parameterized
- Integration test project: `src/CognitiveForge.McpCategoryRouting.IntegrationTests/`

## Key Dependencies

- `ModelContextProtocol` 0.9.0-preview.2 — core MCP SDK (`IMcpServerPrimitive`, `McpServerOptions`, primitive collections)
- `ModelContextProtocol.AspNetCore` 0.9.0-preview.1 — ASP.NET Core transport (`HttpServerTransportOptions`, `MapMcp`)
