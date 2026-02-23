# MCP Category Routing Library — Implementation Plan v2

## Target Experience (Program.cs)

```csharp
using McpCategoryRouting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .AddMcpCategoryRouting(options =>
    {
        options.UncategorizedBehavior = UncategorizedBehavior.IncludeAlways;
    });

var app = builder.Build();

// Single route with a parameter — no per-category registration needed
app.MapMcpCategory("{category}");

// Or namespaced:
// app.MapMcpCategory("mcp/{category}");

// Plain MapMcp() still works, completely unfiltered:
// app.MapMcp("/all");

app.Run();
```

Clients connect to:
- `POST /my` → tools tagged `[McpCategory("my")]`
- `POST /management` → tools tagged `[McpCategory("management")]`
- `POST /analytics` → tools tagged `[McpCategory("analytics")]`
- `POST /mcp` → uncategorized tools (when FallbackRoute behavior is used)

### Tool/Prompt/Resource Declaration

```csharp
using System.ComponentModel;
using McpCategoryRouting;
using ModelContextProtocol.Server;

// === Class-level category — all tools inherit ===

[McpServerToolType]
[McpCategory("analytics")]
public static class AnalyticsTools
{
    [McpServerTool, Description("Daily active users")]
    public static int GetDailyActiveUsers() => 42;

    [McpServerTool, Description("Purge cache")]
    [McpCategory("management")]                   // Method-level overrides class
    public static void PurgeCache() { }
}

// === Multi-category — tool appears on multiple endpoints ===

[McpServerToolType]
public static class SharedTools
{
    [McpServerTool, Description("Health check")]
    [McpCategory("my")]
    [McpCategory("management")]
    [McpCategory("analytics")]
    public static string HealthCheck() => "OK";
}

// === System.ComponentModel.CategoryAttribute also works ===

[McpServerToolType]
[Category("management")]                          // BCL attribute — single category
public static class AdminTools
{
    [McpServerTool, Description("List users")]
    public static string ListUsers() => "[]";
}

// === Uncategorized — behavior depends on options ===

[McpServerToolType]
public static class UncategorizedTools
{
    [McpServerTool, Description("Server version")]
    public static string GetVersion() => "1.0";
}
```

---

## Architecture Overview

```
 DI Registration (startup)                       Request Time (per session)
┌──────────────────────────────────┐        ┌───────────────────────────────────────┐
│ .AddMcpCategoryRouting()         │        │ POST /analytics                       │
│  ├─ Registers:                   │        │  └─ ConfigureSessionOptions fires:    │
│  │  CategoryRoutingOptions       │        │     1. User's original callback       │
│  │  CategorySessionFilter        │        │     2. CategorySessionFilter:         │
│  │    (IPostConfigureOptions     │        │        a. RouteValues["category"]     │
│  │     <HttpServerTransport      │        │           → "analytics"               │
│  │      Options>)                │        │        b. tool.GetCategories() check  │
│  └───────────────────────────────┘        │        c. filter ToolCollection       │
│                                           │        d. filter PromptCollection     │
│ .MapMcpCategory("{category}")    │        │        e. filter ResourceCollection   │
│  └─ Calls MapMcp("{category}")   │        │     3. Session sees only matching     │
│     (ASP.NET route param binding)│        │        primitives                     │
└──────────────────────────────────┘        └───────────────────────────────────────┘
```

**Key simplification vs v1**: No `CategoryRouteMap` needed. The ASP.NET route parameter
`{category}` is the category. Extracted from `HttpContext.Request.RouteValues` at runtime.

---

## Files to Create

### 1. `McpCategoryAttribute.cs`

Custom attribute with `AllowMultiple = true` for multi-category support.

```csharp
namespace McpCategoryRouting;

/// <summary>
/// Assigns an MCP primitive (tool, prompt, or resource) to one or more categories.
/// Categories are matched against route parameter values from
/// <see cref="McpCategoryRoutingExtensions.MapMcpCategory"/>.
/// <para>
/// When applied to a class, all primitives in that class inherit the category
/// unless they declare their own (method-level overrides class-level).
/// </para>
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class McpCategoryAttribute(string category) : Attribute
{
    /// <summary>The category name. Matched case-insensitively.</summary>
    public string Category { get; } = category ?? throw new ArgumentNullException(nameof(category));
}
```

### 2. `UncategorizedBehavior.cs`

```csharp
namespace McpCategoryRouting;

/// <summary>
/// Controls how MCP primitives without any category attribute are handled
/// when category routing is active.
/// </summary>
public enum UncategorizedBehavior
{
    /// <summary>
    /// Uncategorized items appear on every category endpoint.
    /// </summary>
    IncludeAlways,

    /// <summary>
    /// Uncategorized items are excluded from all category endpoints.
    /// Only reachable via a plain <c>MapMcp()</c> endpoint (if mapped).
    /// </summary>
    ExcludeAlways,

    /// <summary>
    /// Uncategorized items only appear when the route parameter matches
    /// <see cref="CategoryRoutingOptions.FallbackCategory"/>.
    /// </summary>
    FallbackRoute
}
```

### 3. `CategoryRoutingOptions.cs`

```csharp
namespace McpCategoryRouting;

/// <summary>
/// Configuration options for MCP category routing.
/// </summary>
public sealed class CategoryRoutingOptions
{
    /// <summary>
    /// The name of the route parameter to extract the category from.
    /// Must match the parameter name in the route pattern passed to
    /// <see cref="McpCategoryRoutingExtensions.MapMcpCategory"/>.
    /// Default: <c>"category"</c>.
    /// </summary>
    /// <example>
    /// <c>options.RouteParameterName = "category";</c> matches
    /// <c>app.MapMcpCategory("{category}")</c> and
    /// <c>app.MapMcpCategory("mcp/{category}")</c>.
    /// </example>
    public string RouteParameterName { get; set; } = "category";

    /// <summary>
    /// How to handle tools/prompts/resources with no category attribute.
    /// Default: <see cref="UncategorizedBehavior.ExcludeAlways"/>.
    /// </summary>
    public UncategorizedBehavior UncategorizedBehavior { get; set; }
        = UncategorizedBehavior.ExcludeAlways;

    /// <summary>
    /// When <see cref="UncategorizedBehavior"/> is <see cref="UncategorizedBehavior.FallbackRoute"/>,
    /// specifies the category value that receives uncategorized items.
    /// Default: <c>"mcp"</c>, so <c>POST /mcp</c> gets uncategorized items
    /// when the route pattern is <c>"{category}"</c>.
    /// </summary>
    public string FallbackCategory { get; set; } = "mcp";
}
```

### 4. `McpServerPrimitiveExtensions.cs`

Reads category from `Metadata` — supports both `McpCategoryAttribute` and
`System.ComponentModel.CategoryAttribute`. No reflection.

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpCategoryRouting;

public static class McpServerPrimitiveExtensions
{
    /// <summary>
    /// Returns all effective category names for this tool by reading
    /// <see cref="McpCategoryAttribute"/> and <see cref="CategoryAttribute"/>
    /// from <see cref="McpServerTool.Metadata"/>.
    /// <para>
    /// Override rule: if any method-level category attribute exists, class-level
    /// ones are ignored. The SDK populates Metadata with class-level attributes
    /// first, then method-level. We detect the boundary by type grouping.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> GetCategories(this McpServerTool tool)
        => ExtractCategories(tool.Metadata);

    // TODO: Add overloads once McpServerPrompt/McpServerResource Metadata is confirmed:
    // public static IReadOnlyList<string> GetCategories(this McpServerPrompt prompt)
    //     => ExtractCategories(prompt.Metadata);
    // public static IReadOnlyList<string> GetCategories(this McpServerResource resource)
    //     => ExtractCategories(resource.Metadata);

    public static bool IsUncategorized(this McpServerTool tool)
        => tool.GetCategories().Count == 0;

    public static bool BelongsToCategory(this McpServerTool tool, string category)
        => tool.GetCategories().Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));

    // ---------------------------------------------------------------
    // Internal implementation
    // ---------------------------------------------------------------

    internal static IReadOnlyList<string> ExtractCategories(IReadOnlyList<object> metadata)
    {
        // The SDK populates Metadata as:
        //   [class-level attrs...] [method-level attrs...]
        //
        // We collect from both McpCategoryAttribute (AllowMultiple) and
        // CategoryAttribute (single per target).
        //
        // Strategy for method-overrides-class:
        //   Since Metadata is a flat list and we can't reliably detect the
        //   boundary between class and method attrs for arbitrary attribute types,
        //   we collect ALL category values. This means:
        //   - [McpCategory("a")] on class + [McpCategory("b")] on method → ["a", "b"]
        //     unless we implement override semantics.
        //
        //   For true override behavior, the TOOL AUTHOR should only put
        //   [McpCategory] on the method when they want to override.
        //   Documenting this as convention is simpler and less fragile than
        //   trying to detect the class/method boundary in a flat list.
        //
        //   The SDK's own authorization filter takes the same "collect all" approach.
        //
        //   However, for the built-in [Category] (AllowMultiple=false), there can be
        //   at most one on the class and one on the method. In that case, if both exist,
        //   the method-level one appears LAST and we take the last one to implement
        //   method-wins semantics. Since McpCategoryAttribute IS AllowMultiple,
        //   we just collect all of them.
        //
        // IMPLEMENTATION:
        //   1. Collect all McpCategoryAttribute.Category values
        //   2. If none found, check for CategoryAttribute (last one wins for override)
        //   3. Deduplicate case-insensitively

        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var mcpCategories = false;
        foreach (var attr in metadata)
        {
            if (attr is McpCategoryAttribute mca)
            {
                categories.Add(mca.Category);
                mcpCategories = true;
            }
        }

        // Fall back to BCL CategoryAttribute only if no McpCategoryAttribute found.
        // This avoids confusing interactions when both are present.
        if (!mcpCategories)
        {
            CategoryAttribute? last = null;
            foreach (var attr in metadata)
            {
                if (attr is CategoryAttribute ca)
                    last = ca;
            }
            if (last is not null)
                categories.Add(last.Category);
        }

        return [.. categories];
    }
}
```

**Precedence rule**: `McpCategoryAttribute` takes priority if present. `CategoryAttribute` is
only used as fallback when no `McpCategoryAttribute` exists. This avoids ambiguity.

### 5. `CategorySessionFilter.cs`

The core filter. Uses `RouteValues` instead of path matching.

```csharp
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

namespace McpCategoryRouting;

/// <summary>
/// Post-configures <see cref="HttpServerTransportOptions"/> to chain category-based
/// filtering into <c>ConfigureSessionOptions</c>.
/// Mirrors the SDK's <c>AuthorizationFilterSetup</c> pattern.
/// </summary>
internal sealed class CategorySessionFilter(
    IOptions<CategoryRoutingOptions> routingOptions)
    : IPostConfigureOptions<HttpServerTransportOptions>
{
    public void PostConfigure(string? name, HttpServerTransportOptions options)
    {
        var originalCallback = options.ConfigureSessionOptions;

        options.ConfigureSessionOptions = async (httpContext, mcpServerOptions, ct) =>
        {
            // Chain: user's original callback runs first.
            if (originalCallback is not null)
                await originalCallback(httpContext, mcpServerOptions, ct);

            var opts = routingOptions.Value;

            // Extract category from route parameter.
            var category = httpContext.Request.RouteValues[opts.RouteParameterName]?.ToString();
            if (category is null)
                return; // Not a category route (plain MapMcp) → no filtering.

            // --- Filter Tools ---
            FilterCollection(
                mcpServerOptions.Capabilities?.Tools?.ToolCollection,
                category,
                opts,
                static tool => tool.GetCategories(),
                filtered => mcpServerOptions.Capabilities!.Tools!.ToolCollection = filtered);

            // --- Filter Prompts ---
            // TODO: uncomment once McpServerPrompt.GetCategories() is wired up:
            // FilterCollection(
            //     mcpServerOptions.Capabilities?.Prompts?.PromptCollection,
            //     category,
            //     opts,
            //     static prompt => prompt.GetCategories(),
            //     filtered => mcpServerOptions.Capabilities!.Prompts!.PromptCollection = filtered);

            // --- Filter Resources ---
            // Same pattern.
        };
    }

    private static void FilterCollection<T>(
        IList<T>? collection,
        string category,
        CategoryRoutingOptions opts,
        Func<T, IReadOnlyList<string>> getCategories,
        Action<List<T>> applyFiltered)
    {
        if (collection is null || collection.Count == 0)
            return;

        var filtered = new List<T>(collection.Count);
        foreach (var item in collection)
        {
            var categories = getCategories(item);

            if (categories.Count == 0)
            {
                // Uncategorized → apply configured behavior
                var include = opts.UncategorizedBehavior switch
                {
                    UncategorizedBehavior.IncludeAlways => true,
                    UncategorizedBehavior.FallbackRoute =>
                        category.Equals(opts.FallbackCategory, StringComparison.OrdinalIgnoreCase),
                    _ => false // ExcludeAlways
                };
                if (include) filtered.Add(item);
            }
            else if (categories.Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)))
            {
                filtered.Add(item);
            }
        }

        applyFiltered(filtered);
    }
}
```

### 6. `McpCategoryRoutingExtensions.cs`

Two clean public methods: `AddMcpCategoryRouting()` and `MapMcpCategory()`.

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;

namespace McpCategoryRouting;

public static class McpCategoryRoutingExtensions
{
    /// <summary>
    /// Adds category-based routing for MCP endpoints. Call after
    /// <c>.WithHttpTransport()</c> and <c>.WithToolsFromAssembly()</c>.
    /// </summary>
    public static IMcpServerBuilder AddMcpCategoryRouting(
        this IMcpServerBuilder builder,
        Action<CategoryRoutingOptions>? configure = null)
    {
        if (configure is not null)
            builder.Services.Configure(configure);

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IPostConfigureOptions<HttpServerTransportOptions>,
                CategorySessionFilter>());

        return builder;
    }

    /// <summary>
    /// Maps MCP endpoints using a route pattern that contains a category parameter.
    /// Only primitives tagged with a matching <see cref="McpCategoryAttribute"/>
    /// (or <see cref="System.ComponentModel.CategoryAttribute"/>) are exposed
    /// for each category value.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="routePattern">
    /// A route pattern containing a <c>{category}</c> parameter (or whatever name is
    /// configured in <see cref="CategoryRoutingOptions.RouteParameterName"/>).
    /// Examples: <c>"{category}"</c>, <c>"mcp/{category}"</c>, <c>"api/v1/{category}"</c>.
    /// </param>
    public static IEndpointConventionBuilder MapMcpCategory(
        this IEndpointRouteBuilder endpoints,
        string routePattern = "{category}")
    {
        return endpoints.MapMcp(routePattern);
    }
}
```

---

## What Was Removed vs v1

| v1 Component | v2 Status | Reason |
|---|---|---|
| `CategoryRouteMap` | **Removed** | Route parameter replaces path→category lookup |
| `McpToolCategoryRegistry` | **Removed** | `Metadata` on `McpServerTool` already has the attributes |
| Assembly scanning | **Removed** | Not needed — we read from `Metadata` at session time |
| Per-endpoint `MapMcpCategory("/x")` calls | **Replaced** | Single `MapMcpCategory("{category}")` |

---

## Attribute Precedence Rules

| Scenario | Source | Effective Categories |
|---|---|---|
| `[McpCategory("a")]` on class only | McpCategoryAttribute | `["a"]` |
| `[McpCategory("a")]` class + `[McpCategory("b")]` method | McpCategoryAttribute | `["a", "b"]` — both collected |
| `[McpCategory("b")]` on method only | McpCategoryAttribute | `["b"]` |
| `[McpCategory("a"), McpCategory("b")]` on method | McpCategoryAttribute | `["a", "b"]` |
| `[Category("x")]` on class only | CategoryAttribute | `["x"]` |
| `[Category("x")]` class + `[Category("y")]` method | CategoryAttribute | `["y"]` — last wins (method) |
| `[McpCategory("a")]` + `[Category("x")]` mixed | McpCategoryAttribute wins | `["a"]` — CategoryAttribute ignored |
| No category attribute at all | — | `[]` — uncategorized, behavior from options |

**Rule**: `McpCategoryAttribute` takes full priority. `CategoryAttribute` is only read when
no `McpCategoryAttribute` is found. This avoids confusing interactions.

---

## Test Plan

Following Firecut testing conventions: nested classes, `Should_*` pattern,
`When_{context}_it_should_{outcome}` for complex scenarios.

### `McpServerPrimitiveExtensionsTest.cs` (unit)

```csharp
public class McpServerPrimitiveExtensionsTest
{
    public class GetCategories : McpServerPrimitiveExtensionsTest
    {
        [Fact]
        public void Should_return_empty_list_when_no_category_attribute_exists()

        [Fact]
        public void Should_return_category_from_McpCategoryAttribute()

        [Fact]
        public void Should_return_multiple_categories_from_multiple_McpCategoryAttributes()

        [Fact]
        public void Should_return_category_from_CategoryAttribute_when_no_McpCategoryAttribute_exists()

        [Fact]
        public void Should_ignore_CategoryAttribute_when_McpCategoryAttribute_is_present()

        [Fact]
        public void Should_return_last_CategoryAttribute_value_when_multiple_exist_in_metadata()

        [Fact]
        public void Should_deduplicate_categories_case_insensitively()
    }

    public class IsUncategorized : McpServerPrimitiveExtensionsTest
    {
        [Fact]
        public void Should_return_true_when_no_category_attribute_exists()

        [Fact]
        public void Should_return_false_when_any_category_attribute_exists()
    }

    public class BelongsToCategory : McpServerPrimitiveExtensionsTest
    {
        [Fact]
        public void Should_return_true_when_tool_has_matching_category()

        [Fact]
        public void Should_return_false_when_tool_has_different_category()

        [Fact]
        public void Should_match_case_insensitively()
    }
}
```

### `CategorySessionFilterTest.cs` (unit)

```csharp
public class CategorySessionFilterTest
{
    // Shared setup: mock HttpContext, McpServerOptions with known tools

    public class PostConfigure : CategorySessionFilterTest
    {
        [Fact]
        public async Task Should_filter_tools_to_matching_category()

        [Fact]
        public async Task Should_not_filter_when_route_parameter_is_missing()

        [Fact]
        public async Task Should_chain_with_existing_ConfigureSessionOptions_callback()
    }

    public class UncategorizedBehavior : CategorySessionFilterTest
    {
        [Fact]
        public async Task Should_exclude_uncategorized_tools_when_behavior_is_ExcludeAlways()

        [Fact]
        public async Task Should_include_uncategorized_tools_on_every_endpoint_when_behavior_is_IncludeAlways()

        [Fact]
        public async Task Should_include_uncategorized_tools_only_on_fallback_route_when_behavior_is_FallbackRoute()

        [Fact]
        public async Task Should_exclude_uncategorized_tools_from_non_fallback_routes_when_behavior_is_FallbackRoute()
    }
}
```

### `McpCategoryRoutingIntegrationTest.cs` (integration)

End-to-end with a real MCP server using `WebApplicationFactory` or similar.

```csharp
public class McpCategoryRoutingIntegrationTest : IAsyncLifetime
{
    // Setup: build WebApplication with AddMcpCategoryRouting + MapMcpCategory("{category}")
    // Register tools from test assembly with various [McpCategory] and [Category] attrs

    [Fact]
    public async Task When_a_client_connects_to_a_category_endpoint_it_should_only_list_tools_for_that_category()

    [Fact]
    public async Task When_a_client_connects_to_a_category_endpoint_it_should_not_list_tools_from_other_categories()

    [Fact]
    public async Task When_a_tool_has_multiple_categories_it_should_appear_on_all_matching_endpoints()

    [Fact]
    public async Task When_a_client_connects_to_a_plain_MapMcp_endpoint_it_should_list_all_tools_unfiltered()

    [Fact]
    public async Task When_a_tool_uses_CategoryAttribute_it_should_be_filtered_the_same_as_McpCategoryAttribute()
}
```

---

## Verification Checklist

- [ ] `System.ComponentModel.CategoryAttribute` appears in `McpServerTool.Metadata`
      (create a tool with `[Category("x")]`, breakpoint, inspect `Metadata` list)
- [ ] `McpCategoryAttribute` appears in `McpServerTool.Metadata`
      (same test with the custom attribute)
- [ ] `HttpContext.Request.RouteValues["category"]` is populated when
      `MapMcp("{category}")` handles a request to e.g. `/analytics`
- [ ] Filtering `ToolCollection` in `ConfigureSessionOptions` affects `tools/list`
      (the McpServer reads per-session options, not a shared singleton)
- [ ] Plain `MapMcp("/all")` on the same app remains unaffected
- [ ] `McpServerPrompt` and `McpServerResource` expose `Metadata : IReadOnlyList<object>`

---

## File Summary

| File | Lines (est.) | Purpose |
|------|-------------|---------|
| `McpCategoryAttribute.cs` | ~15 | `[McpCategory("x")]` — AllowMultiple |
| `UncategorizedBehavior.cs` | ~20 | Enum: IncludeAlways, ExcludeAlways, FallbackRoute |
| `CategoryRoutingOptions.cs` | ~25 | RouteParameterName, UncategorizedBehavior, FallbackCategory |
| `McpServerPrimitiveExtensions.cs` | ~65 | `.GetCategories()` / `.IsUncategorized()` from Metadata |
| `CategorySessionFilter.cs` | ~65 | `IPostConfigureOptions` — reads route param, filters collections |
| `McpCategoryRoutingExtensions.cs` | ~30 | `AddMcpCategoryRouting()` + `MapMcpCategory()` |
| **Total library** | **~220** | |
| | | |
| `McpServerPrimitiveExtensionsTest.cs` | ~80 | Unit tests for category extraction |
| `CategorySessionFilterTest.cs` | ~100 | Unit tests for filtering logic |
| `McpCategoryRoutingIntegrationTest.cs` | ~80 | E2E with WebApplicationFactory |
| **Total tests** | **~260** | |
