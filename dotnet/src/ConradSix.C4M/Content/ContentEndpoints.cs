#pragma warning disable SA1402 // The shared content endpoints and route contracts are kept together.
namespace ConradSix.C4M.Content;

using FastEndpoints;

internal sealed record RegisterContentSchemaRequest(string Type, int SchemaVersion, bool Singleton, string JsonSchema);

internal sealed record ContentTypeRequest(string Type);

internal sealed record ContentRecordRequest(string Type, string Key);

internal sealed record ListContentRequest(string Type, int Offset = 0, int Limit = 50);

internal sealed record ContentHistoryRequest(string Type, string Key, int Offset = 0, int Limit = 50);

internal sealed record SaveContentRecordRequest(
    string Type, string Key, int SchemaVersion, string Data, Guid? ExpectedLatestRevisionId, string? IdempotencyKey = null);

internal sealed record SetContentPublicationRequest(string Type, string Key, Guid? RevisionId, Guid? ExpectedPublishedRevisionId);

internal abstract class ContentEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected async Task RespondAsync(Func<Task<TResponse>> handle, CancellationToken cancellationToken)
    {
        try
        {
            await this.Send.OkAsync(await handle(), cancellationToken);
        }
        catch (ContentRequestException ex)
        {
            this.AddError(ex.Message);
            await this.Send.ErrorsAsync((int)ex.Status, cancellationToken);
        }
    }
}

[CmsEndpoint(CmsEndpointKind.Editor)]
internal sealed class RegisterContentSchema(ContentRecords content) : ContentEndpoint<RegisterContentSchemaRequest, ContentSchemaResponse>
{
    public override void Configure() => this.Put("/content-types/{type}/schemas/{schemaVersion:int}");

    public override Task HandleAsync(RegisterContentSchemaRequest req, CancellationToken ct) =>
        this.RespondAsync(() => content.RegisterAsync(req, ct), ct);
}

[CmsEndpoint(CmsEndpointKind.Editor)]
internal sealed class ListContentSchemas(ContentRecords content) : ContentEndpoint<ContentTypeRequest, ContentListResponse<ContentSchemaResponse>>
{
    public override void Configure() => this.Get("/content-types/{type}/schemas");

    public override Task HandleAsync(ContentTypeRequest req, CancellationToken ct) =>
        this.RespondAsync(async () => new(await content.ListSchemasAsync(req.Type, ct)), ct);
}

[CmsEndpoint(CmsEndpointKind.Editor)]
internal sealed class ListContentRecords(ContentRecords content) : ContentEndpoint<ListContentRequest, ContentListResponse<ContentRecordResponse>>
{
    public override void Configure() => this.Get("/content/{type}");

    public override Task HandleAsync(ListContentRequest req, CancellationToken ct) =>
        this.RespondAsync(async () => new(await content.ListAsync(req, ct)), ct);
}

[CmsEndpoint(CmsEndpointKind.Editor)]
internal sealed class GetContentHistory(ContentRecords content) : ContentEndpoint<ContentHistoryRequest, ContentListResponse<ContentRevisionResponse>>
{
    public override void Configure() => this.Get("/content/{type}/{key}/history");

    public override Task HandleAsync(ContentHistoryRequest req, CancellationToken ct) =>
        this.RespondAsync(async () => new(await content.HistoryAsync(req, ct)), ct);
}

[CmsEndpoint(CmsEndpointKind.Editor)]
internal sealed class SaveContentRecord(ContentRecords content) : ContentEndpoint<SaveContentRecordRequest, ContentRevisionResponse>
{
    public override void Configure() => this.Put("/content/{type}/{key}");

    public override Task HandleAsync(SaveContentRecordRequest req, CancellationToken ct) =>
        this.RespondAsync(() => content.SaveAsync(req, ct), ct);
}

[CmsEndpoint(CmsEndpointKind.Editor)]
internal sealed class SetContentPublication(ContentRecords content) : ContentEndpoint<SetContentPublicationRequest, ContentRecordResponse>
{
    public override void Configure() => this.Put("/content/{type}/{key}/publication");

    public override Task HandleAsync(SetContentPublicationRequest req, CancellationToken ct) =>
        this.RespondAsync(() => content.SetPublicationAsync(req, ct), ct);
}

[CmsEndpoint(CmsEndpointKind.Delivery)]
internal sealed class GetPublishedContent(ContentRecords content) : ContentEndpoint<ContentRecordRequest, ContentRevisionResponse>
{
    public override void Configure() => this.Get("/published-content/{type}/{key}");

    public override Task HandleAsync(ContentRecordRequest req, CancellationToken ct) =>
        this.RespondAsync(() => content.GetPublishedAsync(req, ct), ct);
}

[CmsEndpoint(CmsEndpointKind.Delivery)]
internal sealed class ListPublishedContent(ContentRecords content) : ContentEndpoint<ListContentRequest, ContentListResponse<ContentRevisionResponse>>
{
    public override void Configure() => this.Get("/published-content/{type}");

    public override Task HandleAsync(ListContentRequest req, CancellationToken ct) =>
        this.RespondAsync(async () => new(await content.ListPublishedAsync(req, ct)), ct);
}
