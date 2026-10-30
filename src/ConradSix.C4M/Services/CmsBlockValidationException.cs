#pragma warning disable CA1032 // The exception represents a rejected block's business rules.
namespace ConradSix.C4M.Services;

public sealed class CmsBlockValidationException(string message) : ArgumentException(message);
