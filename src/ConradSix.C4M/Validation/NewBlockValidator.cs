namespace Parapet.CMS.Engine.Validation;

using FastEndpoints;

using FluentValidation;

using Newtonsoft.Json;

using NJsonSchema;

using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Services.Model;

public class NewBlockValidator<T> : Validator<T>
    where T : INewBlock
{
    public NewBlockValidator()
    {
        this.RuleFor(b => b.BlockTypeId)
            .NotEmpty();

        this.RuleFor(b => b.BlockData)
            .NotEmpty();

        this.RuleFor(b => b)
            .CustomAsync(async (block, context, cancellation) =>
            {
                var ctx = this.Resolve<CmsEngineDbContext>();
                var blockType = await ctx.BlockTypes.FindAsync([block.BlockTypeId], cancellation);

                if (blockType == null)
                {
                    context.AddFailure("BlockTypeId", $"Block type with ID '{block.BlockTypeId}' does not exist");
                    return;
                }

                try
                {
                    var schema = await JsonSchema.FromJsonAsync(blockType.JsonSchema, cancellation);
                    var errors = schema.Validate(block.BlockData);

                    foreach (var error in errors)
                    {
                        var path = $@"{blockType.Name}.{error.Path}";
                        context.AddFailure(path, error.Kind.ToString());
                    }
                }
                catch (JsonReaderException ex)
                {
                    context.AddFailure("BlockData", $"Schema validation failed for block type '{blockType.Name}': {ex.Message}");
                }
            });
    }
}
