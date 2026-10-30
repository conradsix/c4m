namespace Parapet.CMS.Engine.Validation;

using FastEndpoints;

using FluentValidation;

using Newtonsoft.Json;

using NJsonSchema;

using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Model;

public class MenuItemValidator : Validator<MenuItem>
{
    public MenuItemValidator(int currentDepth = 1, string itemPath = "")
    {
        this.RuleFor(m => m.Title)
            .NotEmpty()
            .MaximumLength(200);

        this.RuleFor(m => m.ActionTypeId)
            .NotEmpty();

        this.RuleFor(m => m.ActionData)
            .NotEmpty();

        this.RuleFor(m => m)
            .CustomAsync(async (menuItem, context, cancellation) =>
            {
                var ctx = this.Resolve<CmsEngineDbContext>();
                var actionType = await ctx.MenuActionTypes.FindAsync([menuItem.ActionTypeId], cancellation);

                if (actionType == null)
                {
                    context.AddFailure("ActionTypeId", $"Menu action type with ID '{menuItem.ActionTypeId}' does not exist");
                    return;
                }

                if (actionType.Name.Equals("empty", StringComparison.Ordinal)
                    && (menuItem.Children == null || menuItem.Children.Count == 0))
                {
                    context.AddFailure("Children", "A non-navigating menu item must contain at least one child");
                }

                try
                {
                    var schema = await JsonSchema.FromJsonAsync(actionType.JsonSchema, cancellation);
                    var errors = schema.Validate(menuItem.ActionData);

                    foreach (var error in errors)
                    {
                        var path = $@"{actionType.Name}.{error.Path}";
                        context.AddFailure(path, error.Kind.ToString());
                    }
                }
                catch (JsonReaderException ex)
                {
                    context.AddFailure("ActionData", $"Schema validation failed for action type '{actionType.Name}': {ex.Message}");
                }
            });

        // Check depth limit before validating children
        this.RuleFor(m => m.Children)
            .Must(_ => currentDepth < MenuConstants.MaxMenuDepth)
            .WithMessage($"Menu items cannot be nested more than {MenuConstants.MaxMenuDepth} levels deep. Current item is at depth {currentDepth}.")
            .When(m => m.Children != null && m.Children.Count > 0);

        // Recursively validate children with incremented depth
        this.RuleForEach(m => m.Children)
            .SetValidator((parent, child) =>
            {
                var childPath = string.IsNullOrEmpty(itemPath) ? parent.Title : $"{itemPath} → {parent.Title}";
                return new MenuItemValidator(currentDepth + 1, childPath);
            })
            .When(m => m.Children != null && m.Children.Count > 0 && currentDepth < MenuConstants.MaxMenuDepth);
    }
}
