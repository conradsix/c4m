namespace ConradSix.C4M.Validation;

using System.Text.RegularExpressions;

using FluentValidation;
using FluentValidation.Validators;

public partial class SlugPropertyValidator<T> : IPropertyValidator<T, string>
{
    public string Name => "Slug";

    public string GetDefaultMessageTemplate(string errorCode) =>
        "Must contain only lowercase letters, numbers, and hyphens, and cannot start or end with a hyphen";

    public bool IsValid(ValidationContext<T> context, string value) =>
        !string.IsNullOrEmpty(value) && SlugRegex().IsMatch(value);

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
