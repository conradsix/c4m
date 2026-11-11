namespace ConradSix.C4M.SampleHost.OpenApi;

using NJsonSchema;

using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

// Properties that can't be null are required in the document, so generated clients don't type them as optional.
internal sealed class RequiredFromNullabilityProcessor : IDocumentProcessor
{
    public void Process(DocumentProcessorContext context)
    {
        foreach (var kv in context.Document.Components.Schemas)
        {
            MarkRequired(kv.Value);
        }
    }

    private static void MarkRequired(JsonSchema schema)
    {
        if (schema.Properties is { Count: > 0 })
        {
            foreach (var (name, prop) in schema.Properties)
            {
                var isNullable = prop.IsNullableRaw == true;
                if (!isNullable && !schema.RequiredProperties.Contains(name))
                {
                    schema.RequiredProperties.Add(name);
                }
            }
        }

        // Recurse into nested schemas
        foreach (var s in schema.AllOf)
        {
            MarkRequired(s);
        }

        foreach (var s in schema.OneOf)
        {
            MarkRequired(s);
        }

        foreach (var s in schema.AnyOf)
        {
            MarkRequired(s);
        }

        if (schema.Item != null)
        {
            MarkRequired(schema.Item);
        }

        foreach (var s in schema.Items)
        {
            MarkRequired(s);
        }

        if (schema.AdditionalPropertiesSchema != null)
        {
            MarkRequired(schema.AdditionalPropertiesSchema);
        }
    }
}
