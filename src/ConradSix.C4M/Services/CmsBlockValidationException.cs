#pragma warning disable CA1032 // The exception represents a rejected block's business rules.
namespace Parapet.CMS.Engine.Services;

public sealed class CmsBlockValidationException(string message) : ArgumentException(message);
