---
type: "agent_requested"
description: ".NET and CSharp (C#) testing guidelines (*.cs)"
---

# Testing Strategy

## Test Projects

**Source of Truth**: `src/CognitiveForge.McpCategoryRouting.Tests/`, `src/CognitiveForge.McpCategoryRouting.IntegrationTests/`

### CognitiveForge.McpCategoryRouting.Tests

Unit tests for core functionality (extensions, filters, configuration).

- Mirrors `src/CognitiveForge.McpCategoryRouting/` project structure

### CognitiveForge.McpCategoryRouting.IntegrationTests

Integration tests with real MCP server instances.

- End-to-end category routing workflows
- Session filtering with actual MCP server primitives
- Complete registration-to-routing flows

## Naming Conventions

Use nested classes to group tests by the method under test. Test methods use the `Should_*` pattern.

**Structure**:

```
{ClassName}Test
└── {MethodName} (nested class inheriting from parent)
    └── Should_{action_description}
```

**File naming**: `{SourceClass}Test.cs`

**Example**:

```csharp
public class McpServerPrimitiveCategoryExtensionsTests
{
    // shared setup...

    public class GetCategories : McpServerPrimitiveCategoryExtensionsTests
    {
        [Fact]
        public void Should_return_empty_when_no_category_attributes_exist()

        [Fact]
        public void Should_prefer_McpCategoryAttribute_and_ignore_CategoryAttribute()
    }
}
```

**File Structure**: Test project mirrors the source project structure:

```
CognitiveForge.McpCategoryRouting/
├── McpCategoryAttribute.cs
└── McpServerPrimitiveCategoryExtensions.cs

CognitiveForge.McpCategoryRouting.Tests/
├── McpCategoryAttributeTest.cs
└── McpServerPrimitiveCategoryExtensionsTest.cs
```

### Integration Tests

Integration tests verify complete workflows with real MCP server instances.

**File naming**: `{UseCase}Test.cs`

**Two naming patterns based on complexity**:

| Pattern                              | When to Use                      |
|--------------------------------------|----------------------------------|
| `Should_{action}`                    | Simple, focused tests            |
| `When_{context}_it_should_{outcome}` | Complex workflows, E2E scenarios |

**Examples**:

```csharp
// Simple scenario - single action/assertion
[Fact]
public async Task Should_filter_tools_by_category()

// Complex scenario - full workflow with context
[Fact]
public async Task When_a_session_has_a_category_filter_it_should_only_expose_tools_matching_the_category()
```

### Key Rules

1. Keep unit test names short — the nested class provides method context
2. Integration test names can be longer — they describe full scenarios
3. Use `[Fact]` for single-case tests, `[Theory]` for parameterized tests
4. Use lowercase `it_should` (not `ItShould`) for natural reading

## Running Tests

```bash
# Unit tests
dotnet test src/CognitiveForge.McpCategoryRouting.Tests

# Integration tests
dotnet test src/CognitiveForge.McpCategoryRouting.IntegrationTests
```
