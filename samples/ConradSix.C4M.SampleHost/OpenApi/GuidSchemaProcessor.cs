namespace ConradSix.C4M.SampleHost.OpenApi;

using NJsonSchema.Generation;

// Guids are uuid strings in the document, so generated clients type them as strings.
internal sealed class GuidSchemaProcessor : ISchemaProcessor
{
    public void Process(SchemaProcessorContext context)
    {
        if (context.ContextualType == typeof(Guid) || context.ContextualType == typeof(Guid?))
        {
            context.Schema.Type = NJsonSchema.JsonObjectType.String;
            context.Schema.Format = "uuid";
        }
    }
}
