#pragma warning disable SA1402 // The service and its contracts are kept together.
namespace ConradSix.C4M.Content;

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

using ConradSix.C4M.Data;
using ConradSix.C4M.Validation;

using Microsoft.EntityFrameworkCore;

internal sealed record ContentSchemaResponse(string Type, bool Singleton, int SchemaVersion, string JsonSchema);

internal sealed record ContentRevisionResponse(string Type, string Key, Guid Id, int SchemaVersion, string Data, DateTimeOffset CreatedAt);

internal sealed record ContentRecordResponse(string Key, Guid LatestRevisionId, Guid? PublishedRevisionId);

internal sealed record ContentListResponse<T>(IReadOnlyList<T> Items);

#pragma warning disable CA1032 // Every request error must carry a status.
internal sealed class ContentRequestException(HttpStatusCode status, string message) : InvalidOperationException(message)
{
    public HttpStatusCode Status { get; } = status;
}

#pragma warning restore CA1032

internal sealed partial class ContentRecords(CmsEngineDbContext ctx, CmsJsonSchemaCache schemas, ICmsChangeHandler changes)
{
    public async Task<ContentSchemaResponse> RegisterAsync(RegisterContentSchemaRequest request, CancellationToken ct)
    {
        ValidateName(request.Type, 100, "Content type");
        if (request.SchemaVersion < 1)
        {
            throw BadRequest("Schema version must be a positive integer.");
        }

        if (string.IsNullOrWhiteSpace(request.JsonSchema))
        {
            throw BadRequest("A JSON schema is required.");
        }

        if (!schemas.TryGet(request.JsonSchema, out _, out var error))
        {
            throw BadRequest(error);
        }

        CmsTransactions.EnsureNoCallerTransaction(ctx.Database);
        var type = await ctx.Set<ContentType>().FindAsync([request.Type], ct);
        if (type is not null && type.Singleton != request.Singleton)
        {
            throw Conflict("The registered content type has a different collection mode.");
        }

        var schema = await ctx.Set<ContentSchema>().FindAsync([request.Type, request.SchemaVersion], ct);
        if (schema is not null)
        {
            if (!EqualJson(schema.JsonSchema, request.JsonSchema))
            {
                throw Conflict("A registered schema version cannot change.");
            }
        }
        else
        {
            if (type is null)
            {
                ctx.Add(new ContentType { Name = request.Type, Singleton = request.Singleton });
            }

            ctx.Add(new ContentSchema { Type = request.Type, Version = request.SchemaVersion, JsonSchema = request.JsonSchema });
            await this.SaveChangesAsync(ct);
        }

        return new ContentSchemaResponse(request.Type, request.Singleton, request.SchemaVersion, schema?.JsonSchema ?? request.JsonSchema);
    }

    public Task<ContentSchemaResponse[]> ListSchemasAsync(string type, CancellationToken ct) =>
        ctx.Set<ContentSchema>().AsNoTracking()
            .Where(schema => schema.Type == type)
            .Join(ctx.Set<ContentType>(), schema => schema.Type, row => row.Name, (schema, row) => new { Schema = schema, row.Singleton })
            .OrderBy(result => result.Schema.Version)
            .Select(result => new ContentSchemaResponse(result.Schema.Type, result.Singleton, result.Schema.Version, result.Schema.JsonSchema))
            .ToArrayAsync(ct);

    public Task<ContentRecordResponse[]> ListAsync(ListContentRequest request, CancellationToken ct) =>
        Page(
            ctx.Set<ContentRecord>().AsNoTracking()
            .Where(record => record.Type == request.Type)
            .OrderBy(record => record.Key)
            .Select(record => new ContentRecordResponse(record.Key, record.LatestRevisionId, record.PublishedRevisionId)),
            request.Offset,
            request.Limit)
            .ToArrayAsync(ct);

    public Task<ContentRevisionResponse[]> HistoryAsync(ContentHistoryRequest request, CancellationToken ct) =>
        Page(
            ctx.Set<ContentRevision>().AsNoTracking()
            .Where(revision => revision.Type == request.Type && revision.Key == request.Key)
            .OrderByDescending(revision => revision.CreatedAt).ThenByDescending(revision => revision.Id)
            .Select(revision => new ContentRevisionResponse(
                revision.Type, revision.Key, revision.Id, revision.SchemaVersion, revision.Data, revision.CreatedAt)),
            request.Offset,
            request.Limit)
            .ToArrayAsync(ct);

    public async Task<ContentRevisionResponse> GetPublishedAsync(ContentRecordRequest request, CancellationToken ct) =>
        await Project(this.Published(request.Type).Where(revision => revision.Key == request.Key))
            .SingleOrDefaultAsync(ct) ?? throw Missing();

    public Task<ContentRevisionResponse[]> ListPublishedAsync(ListContentRequest request, CancellationToken ct) =>
        Project(Page(this.Published(request.Type).OrderBy(revision => revision.Key), request.Offset, request.Limit))
            .ToArrayAsync(ct);

    public async Task<ContentRevisionResponse> SaveAsync(SaveContentRecordRequest request, CancellationToken ct)
    {
        ValidateName(request.Key, 120, "Record key");
        CmsTransactions.EnsureNoCallerTransaction(ctx.Database);
        var type = await ctx.Set<ContentType>().FindAsync([request.Type], ct)
            ?? throw BadRequest("The content type is not registered.");
        if (type.Singleton && request.Key != "default")
        {
            throw BadRequest("A singleton uses the key 'default'.");
        }

        if (request.IdempotencyKey is { Length: 0 or > 200 } || string.IsNullOrWhiteSpace(request.Data))
        {
            throw BadRequest("Supply JSON data and an idempotency key of 1 to 200 characters when using a key.");
        }

        var descriptor = await ctx.Set<ContentSchema>().FindAsync([request.Type, request.SchemaVersion], ct)
            ?? throw BadRequest("The schema version is not registered.");
        var errors = schemas.Check(request.Type, descriptor.JsonSchema, request.Data);
        if (errors.Count > 0)
        {
            throw BadRequest(string.Join(" ", errors.Select(error => $"{error.Path}: {error.Kind}")));
        }

        var record = await ctx.Set<ContentRecord>().FindAsync([request.Type, request.Key], ct);
        if (request.IdempotencyKey is { } key)
        {
            var prior = await ctx.Set<ContentRevision>().AsNoTracking().SingleOrDefaultAsync(
                revision => revision.Type == request.Type && revision.Key == request.Key && revision.IdempotencyKey == key, ct);
            if (prior is not null)
            {
                return prior.SchemaVersion == request.SchemaVersion && EqualJson(prior.Data, request.Data)
                    ? ToResponse(prior)
                    : throw Conflict("The idempotency key was already used for different data.");
            }
        }

        if (record?.LatestRevisionId != request.ExpectedLatestRevisionId)
        {
            throw Conflict("The record has changed. Reload it before saving.");
        }

        var next = new ContentRevision
        {
            Type = request.Type,
            Key = request.Key,
            Id = Guid.NewGuid(),
            SchemaVersion = request.SchemaVersion,
            Data = request.Data,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        if (record is null)
        {
            record = new ContentRecord { Type = request.Type, Key = request.Key };
            ctx.Add(record);
        }

        record.LatestRevisionId = next.Id;
        ctx.Add(next);
        await this.SaveChangesAsync(ct);
        return ToResponse(next);
    }

    public async Task<ContentRecordResponse> SetPublicationAsync(SetContentPublicationRequest request, CancellationToken ct)
    {
        CmsTransactions.EnsureNoCallerTransaction(ctx.Database);
        var record = await ctx.Set<ContentRecord>().FindAsync([request.Type, request.Key], ct) ?? throw Missing();
        if (record.PublishedRevisionId == request.RevisionId)
        {
            return new ContentRecordResponse(record.Key, record.LatestRevisionId, record.PublishedRevisionId);
        }

        if (record.PublishedRevisionId != request.ExpectedPublishedRevisionId)
        {
            throw Conflict("Publication has changed. Reload the record before continuing.");
        }

        if (request.RevisionId is { } id && !await ctx.Set<ContentRevision>().AnyAsync(
            revision => revision.Id == id && revision.Type == request.Type && revision.Key == request.Key, ct))
        {
            throw BadRequest("The revision does not belong to this record.");
        }

        record.PublishedRevisionId = request.RevisionId;
        await this.SaveChangesAsync(ct);
        await changes.ContentChangedAsync();
        return new ContentRecordResponse(record.Key, record.LatestRevisionId, record.PublishedRevisionId);
    }

    private static ContentRequestException BadRequest(string message) => new(HttpStatusCode.BadRequest, message);

    private static ContentRequestException Conflict(string message) => new(HttpStatusCode.Conflict, message);

    private static ContentRequestException Missing() => new(HttpStatusCode.NotFound, "Content record not found.");

    private static ContentRevisionResponse ToResponse(ContentRevision revision) =>
        new(revision.Type, revision.Key, revision.Id, revision.SchemaVersion, revision.Data, revision.CreatedAt);

    private static IQueryable<T> Page<T>(IQueryable<T> query, int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100)
        {
            throw BadRequest("Offset must be non-negative, and limit must be between 1 and 100.");
        }

        return query.Skip(offset).Take(limit);
    }

    private static void ValidateName(string value, int maxLength, string label)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength || !SlugPattern().IsMatch(value))
        {
            throw BadRequest($"{label} must be a slug of up to {maxLength} characters.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SlugPattern();

    private static bool EqualJson(string first, string second)
    {
        using var a = JsonDocument.Parse(first);
        using var b = JsonDocument.Parse(second);
        return JsonElement.DeepEquals(a.RootElement, b.RootElement);
    }

    private static IQueryable<ContentRevisionResponse> Project(IQueryable<ContentRevision> revisions) =>
        revisions.Select(revision => new ContentRevisionResponse(
            revision.Type, revision.Key, revision.Id, revision.SchemaVersion, revision.Data, revision.CreatedAt));

    private IQueryable<ContentRevision> Published(string type) =>
        ctx.Set<ContentRecord>().AsNoTracking().Where(record => record.Type == type && record.PublishedRevisionId.HasValue)
            .Join(ctx.Set<ContentRevision>(), record => record.PublishedRevisionId, revision => (Guid?)revision.Id, (_, revision) => revision);

    private async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            CmsTransactions.ForgetUnsaved(ctx.ChangeTracker);
            throw Conflict("The content has changed. Reload it before continuing.");
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            CmsTransactions.ForgetUnsaved(ctx.ChangeTracker);
            throw Conflict("An operation created this content concurrently. Reload it before continuing.");
        }
    }
}
