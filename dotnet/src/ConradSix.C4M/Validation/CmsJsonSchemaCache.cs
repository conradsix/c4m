namespace ConradSix.C4M.Validation;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

// Compiled block and menu action schemas, by their text. Registered schemas never change, so each compiles once.
internal sealed class CmsJsonSchemaCache
{
    private readonly ConcurrentDictionary<string, (CmsJsonSchema? Schema, string? Error)> _schemas = new(StringComparer.Ordinal);

    public bool TryGet(
        string json,
        [NotNullWhen(true)] out CmsJsonSchema? schema,
        [NotNullWhen(false)] out string? error)
    {
        (schema, error) = this._schemas.GetOrAdd(
            json,
            static text => CmsJsonSchema.TryCompile(text, out var compiled, out var reason) ? (compiled, null) : (null, reason));
        return schema is not null;
    }

    // Checks data against its type's schema.
    public IReadOnlyList<CmsJsonSchemaError> Check(string typeName, string schema, string data)
    {
        if (!this.TryGet(schema, out var compiled, out var unsupported))
        {
            return [new(null, $"The schema of {typeName} can't be checked: {unsupported}")];
        }

        try
        {
            return compiled.Validate(data);
        }
        catch (JsonException)
        {
            return [new(null, $"The data for {typeName} isn't valid JSON.")];
        }
    }
}
