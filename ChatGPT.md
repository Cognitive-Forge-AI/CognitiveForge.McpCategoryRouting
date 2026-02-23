Yes.

`MapMcp(...)` takes a **route pattern prefix**, so you can map your MCP endpoints under `"/{toolCategory}"` (or `"/mcp/{toolCategory}"`). ([Model Context Protocol][1])

Then add **request filters** that:

* on list operations: remove tools/prompts/resources that don’t match the current `{toolCategory}`
* on single operations: reject calls to tools/prompts/resources that don’t match the current `{toolCategory}`

The MCP C# SDK already supports these request filters (ListTools, CallTool, ListPrompts, GetPrompt, ListResources, ReadResource, etc.). ([Model Context Protocol][2])

Also, `McpServerTool.Metadata` already contains the custom attributes from the tool method and its declaring type (so you can read `CategoryAttribute` from `Metadata` without doing reflection again). ([GitHub][3])

---

## Drop-in implementation (single file)

Create a file: `McpCategoryFiltering.cs`

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.AspNetCore
{
    public enum McpUncategorizedBehavior
    {
        /// <summary>Show uncategorized items in every category endpoint.</summary>
        IncludeEverywhere,

        /// <summary>Never show uncategorized items on any category endpoint.</summary>
        ExcludeEverywhere,

        /// <summary>Show uncategorized items only when no category is specified.</summary>
        IncludeOnlyWhenNoCategorySpecified,

        /// <summary>Show uncategorized items only under a specific category name (FallbackCategory).</summary>
        IncludeOnlyInFallbackCategory,
    }

    public sealed class McpCategoryFilterOptions
    {
        /// <summary>
        /// The route value name to read from HttpContext.Request.RouteValues.
        /// Must match your MapMcp route param name, e.g. "/{toolCategory}".
        /// </summary>
        public string RouteValueName { get; set; } = "toolCategory";

        public bool CaseInsensitive { get; set; } = true;

        /// <summary>
        /// If true and the route value is missing/empty, no filtering is applied (keeps stdio and non-categorized endpoints working).
        /// </summary>
        public bool DisableFilteringWhenCategoryMissing { get; set; } = true;

        public McpUncategorizedBehavior UncategorizedBehavior { get; set; } = McpUncategorizedBehavior.IncludeEverywhere;

        /// <summary>
        /// Used when UncategorizedBehavior is IncludeOnlyInFallbackCategory.
        /// Example: "common" => uncategorized items only appear under "/common".
        /// </summary>
        public string? FallbackCategory { get; set; }
    }

    public static class McpServerPrimitiveCategoryExtensions
    {
        /// <summary>
        /// Reads CategoryAttribute from IMcpServerPrimitive.Metadata.
        /// If both type-level and method-level CategoryAttribute exist, the method-level one wins.
        /// </summary>
        public static string? GetCategory(this IMcpServerPrimitive primitive)
        {
            if (primitive is null) return null;

            // Metadata contains type attributes before method attributes for AIFunction-created primitives,
            // so LastOrDefault gives method-level precedence.
            return primitive.Metadata.OfType<CategoryAttribute>().LastOrDefault()?.Category?.Trim();
        }
    }

    internal sealed class McpCategoryFilterSetup(
        IHttpContextAccessor httpContextAccessor,
        IOptionsMonitor<McpCategoryFilterOptions> optionsMonitor) : IConfigureOptions<McpServerOptions>
    {
        public void Configure(McpServerOptions options)
        {
            ConfigureTools(options);
            ConfigurePrompts(options);
            ConfigureResources(options);
        }

        private void ConfigureTools(McpServerOptions options)
        {
            options.Filters.Request.ListToolsFilters.Add(next => async (context, ct) =>
            {
                var result = await next(context, ct);
                var currentCategory = GetCurrentCategory();
                FilterListedItems(result.Tools, static t => t.McpServerTool, currentCategory);
                return result;
            });

            options.Filters.Request.CallToolFilters.Add(next => async (context, ct) =>
            {
                var currentCategory = GetCurrentCategory();
                EnsureAllowed(context.MatchedPrimitive, currentCategory, kind: "tool");
                return await next(context, ct);
            });
        }

        private void ConfigurePrompts(McpServerOptions options)
        {
            options.Filters.Request.ListPromptsFilters.Add(next => async (context, ct) =>
            {
                var result = await next(context, ct);
                var currentCategory = GetCurrentCategory();
                FilterListedItems(result.Prompts, static p => p.McpServerPrompt, currentCategory);
                return result;
            });

            options.Filters.Request.GetPromptFilters.Add(next => async (context, ct) =>
            {
                var currentCategory = GetCurrentCategory();
                EnsureAllowed(context.MatchedPrimitive, currentCategory, kind: "prompt");
                return await next(context, ct);
            });
        }

        private void ConfigureResources(McpServerOptions options)
        {
            options.Filters.Request.ListResourcesFilters.Add(next => async (context, ct) =>
            {
                var result = await next(context, ct);
                var currentCategory = GetCurrentCategory();
                FilterListedItems(result.Resources, static r => r.McpServerResource, currentCategory);
                return result;
            });

            options.Filters.Request.ListResourceTemplatesFilters.Add(next => async (context, ct) =>
            {
                var result = await next(context, ct);
                var currentCategory = GetCurrentCategory();
                FilterListedItems(result.ResourceTemplates, static rt => rt.McpServerResource, currentCategory);
                return result;
            });

            options.Filters.Request.ReadResourceFilters.Add(next => async (context, ct) =>
            {
                var currentCategory = GetCurrentCategory();
                EnsureAllowed(context.MatchedPrimitive, currentCategory, kind: "resource");
                return await next(context, ct);
            });
        }

        private string? GetCurrentCategory()
        {
            var opts = optionsMonitor.CurrentValue;
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext is null) return null;

            if (!httpContext.Request.RouteValues.TryGetValue(opts.RouteValueName, out var value) || value is null)
                return null;

            return Convert.ToString(value)?.Trim();
        }

        private void FilterListedItems<TListed>(
            IList<TListed> items,
            Func<TListed, IMcpServerPrimitive?> primitiveSelector,
            string? currentCategory)
        {
            var opts = optionsMonitor.CurrentValue;

            if (opts.DisableFilteringWhenCategoryMissing && string.IsNullOrWhiteSpace(currentCategory))
                return;

            var comparer = opts.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                var primitive = primitiveSelector(items[i]);
                if (!IsAllowed(primitive, currentCategory, opts, comparer))
                    items.RemoveAt(i);
            }
        }

        private void EnsureAllowed(IMcpServerPrimitive? primitive, string? currentCategory, string kind)
        {
            var opts = optionsMonitor.CurrentValue;

            if (opts.DisableFilteringWhenCategoryMissing && string.IsNullOrWhiteSpace(currentCategory))
                return;

            var comparer = opts.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

            if (!IsAllowed(primitive, currentCategory, opts, comparer))
            {
                var cat = string.IsNullOrWhiteSpace(currentCategory) ? "<none>" : currentCategory;
                throw new McpProtocolException(
                    $"This {kind} is not available in category '{cat}'.",
                    McpErrorCode.InvalidRequest);
            }
        }

        private static bool IsAllowed(
            IMcpServerPrimitive? primitive,
            string? currentCategory,
            McpCategoryFilterOptions opts,
            StringComparer comparer)
        {
            var primitiveCategory = primitive?.GetCategory();

            if (string.IsNullOrWhiteSpace(primitiveCategory))
            {
                return opts.UncategorizedBehavior switch
                {
                    McpUncategorizedBehavior.IncludeEverywhere => true,
                    McpUncategorizedBehavior.ExcludeEverywhere => false,
                    McpUncategorizedBehavior.IncludeOnlyWhenNoCategorySpecified => string.IsNullOrWhiteSpace(currentCategory),
                    McpUncategorizedBehavior.IncludeOnlyInFallbackCategory =>
                        !string.IsNullOrWhiteSpace(opts.FallbackCategory) &&
                        !string.IsNullOrWhiteSpace(currentCategory) &&
                        comparer.Equals(currentCategory, opts.FallbackCategory),
                    _ => true
                };
            }

            if (string.IsNullOrWhiteSpace(currentCategory))
            {
                // If you want this to pass, set DisableFilteringWhenCategoryMissing=true (default).
                return false;
            }

            return comparer.Equals(currentCategory, primitiveCategory);
        }
    }

    public static class McpEndpointRouteBuilderCategoryExtensions
    {
        /// <summary>
        /// Maps MCP endpoints under "/{toolCategory}" by default.
        /// </summary>
        public static IEndpointConventionBuilder MapMcpByCategory(
            this IEndpointRouteBuilder endpoints,
            string pattern = "/{toolCategory}")
            => endpoints.MapMcp(pattern);
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class McpCategoryFilteringServiceCollectionExtensions
    {
        /// <summary>
        /// Adds category-based filtering for tools/prompts/resources using CategoryAttribute.
        /// </summary>
        public static IMcpServerBuilder AddCategoryFilters(
            this IMcpServerBuilder builder,
            Action<ModelContextProtocol.AspNetCore.McpCategoryFilterOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.AddHttpContextAccessor();

            if (configure is not null)
            {
                builder.Services.Configure(configure);
            }
            else
            {
                builder.Services.AddOptions<ModelContextProtocol.AspNetCore.McpCategoryFilterOptions>();
            }

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Transient<IConfigureOptions<McpServerOptions>, ModelContextProtocol.AspNetCore.McpCategoryFilterSetup>());

            return builder;
        }
    }
}
```

---

## Minimal usage in Program.cs

```csharp
using System.ComponentModel;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .AddCategoryFilters(o =>
    {
        o.RouteValueName = "toolCategory"; // matches "/{toolCategory}"
        o.UncategorizedBehavior = McpUncategorizedBehavior.IncludeOnlyInFallbackCategory;
        o.FallbackCategory = "common";
    })
    .WithToolsFromAssembly(typeof(Program).Assembly);

var app = builder.Build();

app.MapMcpByCategory(); // maps "/{toolCategory}" + "/{toolCategory}/sse" etc.

app.Run();
```

`CategoryAttribute` usage:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
[Category("management")]
public static class ManagementTools
{
    [McpServerTool]
    public static string Health() => "ok";

    [McpServerTool]
    [Category("analytics")] // method-level overrides class-level
    public static string Report() => "report";
}
```

---

## Notes that matter for ASP.‌NET Core hosting

* `MapMcp` supports a route prefix string, so `"/{toolCategory}"` works. ([Model Context Protocol][1])
* The filtering hooks you need are the built-in request filters (ListTools, CallTool, ListPrompts, GetPrompt, ListResources, ReadResource, etc.). ([Model Context Protocol][2])
* Reading `CategoryAttribute` from `IMcpServerPrimitive.Metadata` works because the SDK populates metadata with custom attributes when creating primitives (see `CreateMetadata`). ([GitHub][3])
* If you enable `HttpServerTransportOptions.PerSessionExecutionContext = true`, `IHttpContextAccessor` won’t work inside handlers/filters. Keep it `false` (default) for route-based categorization. ([Model Context Protocol][4])

[1]: https://modelcontextprotocol.github.io/csharp-sdk/api/Microsoft.AspNetCore.Builder.McpEndpointRouteBuilderExtensions.html?utm_source=chatgpt.com "Class McpEndpointRouteBuilderExtensions | MCP C# SDK"
[2]: https://modelcontextprotocol.github.io/csharp-sdk/concepts/filters.html "https://modelcontextprotocol.github.io/csharp-sdk/concepts/filters.html"
[3]: https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/main/src/ModelContextProtocol.Core/Server/AIFunctionMcpServerTool.cs "https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/main/src/ModelContextProtocol.Core/Server/AIFunctionMcpServerTool.cs"
[4]: https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.AspNetCore.HttpServerTransportOptions.html "https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.AspNetCore.HttpServerTransportOptions.html"
