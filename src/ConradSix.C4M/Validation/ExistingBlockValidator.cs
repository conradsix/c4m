namespace Parapet.CMS.Engine.Validation;

using FastEndpoints;

using FluentValidation;

using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Services.Model;

public class ExistingBlockValidator<T> : Validator<T>
    where T : IExistingBlock
{
    public ExistingBlockValidator()
    {
        this.RuleFor(b => b.BlockId)
            .NotEmpty();

        this.RuleFor(b => b)
            .CustomAsync(async (block, context, cancellation) =>
            {
                var ctx = this.Resolve<CmsEngineDbContext>();
                var existingBlock = await ctx.PageBlocks.FindAsync([block.BlockId], cancellation);

                if (existingBlock == null)
                {
                    context.AddFailure("BlockId", $"Block with ID '{block.BlockId}' does not exist.");
                }
            });
    }
}
