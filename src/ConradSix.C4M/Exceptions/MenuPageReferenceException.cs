#pragma warning disable CA1032 // Validation failures carry the item and destination errors for repair.
#pragma warning disable CA1064 // Never leaves the engine: the menu endpoints turn it into a validation error.
namespace ConradSix.C4M.Exceptions;

internal sealed class MenuPageReferenceException(IReadOnlyList<string> errors)
    : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
