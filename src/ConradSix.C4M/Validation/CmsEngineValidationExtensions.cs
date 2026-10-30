#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentValidation;

using ConradSix.C4M.Services.Model;
using ConradSix.C4M.Validation;

public static class CmsEngineValidationExtensions
{
    public static IRuleBuilderOptions<T, string> Slug<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder.SetValidator(new SlugPropertyValidator<T>());

    internal static IRuleBuilderOptions<T, TItem> NewBlock<T, TItem>(
        this IRuleBuilderInitialCollection<T, TItem> ruleBuilder)
        where TItem : INewBlock
        =>
        ruleBuilder.SetValidator(new NewBlockValidator<TItem>());

    internal static IRuleBuilderOptions<T, TProperty> NewBlock<T, TProperty>(
        this IRuleBuilder<T, TProperty> ruleBuilder)
        where TProperty : INewBlock
        =>
        ruleBuilder.SetValidator(new NewBlockValidator<TProperty>());

    internal static IRuleBuilderOptions<T, TProperty> ExistingBlock<T, TProperty>(
        this IRuleBuilder<T, TProperty> ruleBuilder)
        where TProperty : IExistingBlock
        =>
        ruleBuilder.SetValidator(new ExistingBlockValidator<TProperty>());

    internal static IRuleBuilderOptions<T, TItem> ExistingBlock<T, TItem>(
        this IRuleBuilderInitialCollection<T, TItem> ruleBuilder)
        where TItem : IExistingBlock
        =>
        ruleBuilder.SetValidator(new ExistingBlockValidator<TItem>());
}
