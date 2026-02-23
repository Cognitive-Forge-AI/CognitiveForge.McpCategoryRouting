# MCP Category Routing Library - Merged Plan (v3)

This plan merges the strongest parts of `ChatGPT.md` and `IMPLEMENTATION_PLAN_v2.md` with your constraints:
- Use direct endpoint mapping with `app.MapMcp("anypath/{category}")` (no `MapMcpByCategory` wrapper).
- Route key (`category`) is configurable.
- Category matching must support both:
1. custom `[McpCategory(...)]`
2. BCL `[System.ComponentModel.Category(...)]`

Assumption: `app.MapMvc(...)` in your note is a typo for `app.MapMcp(...)` in the MCP SDK.

## Target Usage (Program.cs)

```csharp
using McpCategoryRouting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .AddMcpCategoryRouting(options =>
    {
        options.RouteValueName = "category";          // matches {category} in MapMcp pattern
        options.UncategorizedBehavior = UncategorizedBehavior.FallbackRoute;
        options.FallbackCategory = "mcp";
    });

var app = builder.Build();

// Primary requirement: direct usage, no wrapper method.
app.MapMcp("anypath/{category}");

// Optional plain endpoint remains unfiltered:
app.MapMcp("/all");

app.Run();
```

## Key Design Choices

1. Keep ChatGPT-style request filters:
- Filter list operations (`tools/list`, `prompts/list`, `resources/list`, template list).
- Guard single-item operations (`tools/call`, `prompts/get`, `resources/read`) to prevent bypass by direct name/URI.

2. Keep v2 attribute semantics:
- `McpCategoryAttribute` supports multi-category (`AllowMultiple = true`).
- `CategoryAttribute` is supported when no `McpCategoryAttribute` exists.
- Precedence: if any `McpCategoryAttribute` is present, ignore `CategoryAttribute`.

3. Remove route wrapper extension:
- Do not add `MapMcpByCategory` or `MapMcpCategory`.
- Only service-registration extension is needed (`AddMcpCategoryRouting`).

## Files To Implement

1. `McpCategoryAttribute.cs`
```csharp
namespace McpCategoryRouting;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class McpCategoryAttribute(string category) : Attribute
{
    public string Category { get; } = category ?? throw new ArgumentNullException(nameof(category));
}
```

2. `UncategorizedBehavior.cs`
```csharp
namespace McpCategoryRouting;

public enum UncategorizedBehavior
{
    IncludeAlways,
    ExcludeAlways,
    FallbackRoute
}
```

3. `McpCategoryRoutingOptions.cs`
```csharp
namespace McpCategoryRouting;

public sealed class McpCategoryRoutingOptions
{
    // Must match the route token in app.MapMcp(".../{category}")
    public string RouteValueName { get; set; } = "category";

    public bool CaseInsensitive { get; set; } = true;

    // If true, requests with missing route value skip filtering.
    public bool DisableFilteringWhenCategoryMissing { get; set; } = true;

    public UncategorizedBehavior UncategorizedBehavior { get; set; } = UncategorizedBehavior.ExcludeAlways;

    // Used when behavior is FallbackRoute.
    public string FallbackCategory { get; set; } = "mcp";
}
```

4. `McpServerPrimitiveCategoryExtensions.cs`
```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpCategoryRouting;

public static class McpServerPrimitiveCategoryExtensions
{
    public static IReadOnlyList<string> GetCategories(this IMcpServerPrimitive primitive)
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var hasMcpCategory = false;
        foreach (var m in primitive.Metadata)
        {
            if (m is McpCategoryAttribute mc)
            {
                if (!string.IsNullOrWhiteSpace(mc.Category))
                    categories.Add(mc.Category.Trim());
                hasMcpCategory = true;
            }
        }

        if (!hasMcpCategory)
        {
            CategoryAttribute? lastCategory = null;
            foreach (var m in primitive.Metadata)
            {
                if (m is CategoryAttribute c)
                    lastCategory = c;
            }

            if (lastCategory is not null && !string.IsNullOrWhiteSpace(lastCategory.Category))
                categories.Add(lastCategory.Category.Trim());
        }

        return [.. categories];
    }
}
```

5. `McpCategoryFilterSetup.cs`
- Register filters in `IConfigureOptions<McpServerOptions>` (ChatGPT approach).
- Read category via `IHttpContextAccessor` from `HttpContext.Request.RouteValues[RouteValueName]`.
- Apply same allow/deny logic for list and single operations.

Pseudo-shape:
```csharp
internal sealed class McpCategoryFilterSetup : IConfigureOptions<McpServerOptions>
{
    // ctor(IHttpContextAccessor, IOptionsMonitor<McpCategoryRoutingOptions>)

    public void Configure(McpServerOptions options)
    {
        ConfigureTools(options);
        ConfigurePrompts(options);
        ConfigureResources(options);
    }

    // tools/list: remove non-matching tools
    // tools/call: throw McpProtocolException for non-matching tool
    // prompts/resources: same pattern
}
```

6. `McpCategoryRoutingServiceCollectionExtensions.cs`
```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace Microsoft.Extensions.DependencyInjection;

public static class McpCategoryRoutingServiceCollectionExtensions
{
    public static IMcpServerBuilder AddMcpCategoryRouting(
        this IMcpServerBuilder builder,
        Action<McpCategoryRouting.McpCategoryRoutingOptions>? configure = null)
    {
        builder.Services.AddHttpContextAccessor();

        if (configure is not null)
            builder.Services.Configure(configure);
        else
            builder.Services.AddOptions<McpCategoryRouting.McpCategoryRoutingOptions>();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Transient<
                IConfigureOptions<McpServerOptions>,
                McpCategoryRouting.McpCategoryFilterSetup>());

        return builder;
    }
}
```

## Category Resolution Rules

1. `McpCategoryAttribute` present:
- Use all custom categories (case-insensitive distinct).
- Ignore all `CategoryAttribute` values.

2. No custom category present:
- Use `CategoryAttribute` (last one wins from metadata ordering).

3. No category attributes:
- Treat as uncategorized and apply `UncategorizedBehavior`.

## Behavior Matrix (Uncategorized)

- `IncludeAlways`: uncategorized visible on every `{category}` endpoint.
- `ExcludeAlways`: uncategorized hidden on category endpoints.
- `FallbackRoute`: uncategorized visible only when route category equals `FallbackCategory`.

## Test Plan

1. Unit tests for category extraction:
- custom only, custom multi, BCL only, both mixed (custom wins), case-insensitive dedupe.

2. Unit tests for allow/deny behavior:
- each uncategorized mode and missing route behavior.

3. Integration tests with real host:
- `app.MapMcp("mcp/{category}")` exposes only matching tools.
- direct call to non-matching tool is rejected.
- plain `app.MapMcp("/all")` remains unfiltered.

## Out of Scope

- Any `MapMcpByCategory`/`MapMcpCategory` endpoint wrapper.
- Assembly scanning registries for categories (metadata-driven only).
