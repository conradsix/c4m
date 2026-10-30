#pragma warning disable SA1402 // The result is kept with the write it answers.
namespace Parapet.CMS.Engine.Services.Model;

// How a new version is written. With an expected latest version, a page that has moved on is a conflict instead. With
// an idempotency key, repeating a write that was already saved returns the saved version, so a caller can retry
// after a failure of its own without saving twice.
public sealed record PageVersionWrite(Guid? ExpectedLatestPageVersionId = null, string? IdempotencyKey = null)
{
    public const int IdempotencyKeyMaxLength = 200;

    public static PageVersionWrite Unconditional { get; } = new();
}

public abstract record PageVersionWriteResult;

public sealed record PageVersionSaved(Guid PageVersionId, bool Replayed) : PageVersionWriteResult;

public sealed record PageVersionConflict(Guid? LatestPageVersionId) : PageVersionWriteResult;

public sealed record PageVersionPageMissing : PageVersionWriteResult;
