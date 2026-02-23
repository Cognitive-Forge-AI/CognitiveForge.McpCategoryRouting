namespace CognitiveForge.McpCategoryRouting;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class McpCategoryAttribute(string category) : Attribute
{
    public string Category { get; } = category ?? throw new ArgumentNullException(nameof(category));
}

