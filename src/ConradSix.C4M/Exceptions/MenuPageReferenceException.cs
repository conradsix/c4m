#pragma warning disable CA1032 // Validation failures carry the item and destination errors for repair.
namespace Parapet.CMS.Engine.Exceptions;

public sealed class MenuPageReferenceException(IReadOnlyList<string> errors)
    : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
