namespace ConradSix.C4M.Validation;

using ConradSix.C4M.Data;
using ConradSix.C4M.Services.Model;

using FastEndpoints;

using FluentValidation;

internal class NewBlockValidator<T> : Validator<T>
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

                foreach (var error in this.Resolve<CmsJsonSchemaCache>().Check(blockType.Name, blockType.JsonSchema, block.BlockData))
                {
                    context.AddFailure(error.Path is null ? "BlockData" : $"{blockType.Name}.{error.Path}", error.Kind);
                }
            });
    }
}
