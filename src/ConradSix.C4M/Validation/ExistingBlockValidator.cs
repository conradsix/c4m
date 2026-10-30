namespace ConradSix.C4M.Validation;

using ConradSix.C4M.Data;
using ConradSix.C4M.Services.Model;

using FastEndpoints;

using FluentValidation;

internal class ExistingBlockValidator<T> : Validator<T>
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
