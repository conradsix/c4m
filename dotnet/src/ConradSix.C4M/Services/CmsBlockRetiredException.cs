#pragma warning disable CA1032 // This exception always identifies a retired block type.
namespace ConradSix.C4M.Services;

public sealed class CmsBlockRetiredException(string name) : ArgumentException($"The {name} block is retired. Remove it before saving.")
{
}
