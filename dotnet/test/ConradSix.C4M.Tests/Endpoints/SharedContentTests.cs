namespace ConradSix.C4M.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Content;

[Collection(C4MAppCollection.Name)]
public sealed class SharedContentTests(C4MAppFixture fixture) : IAsyncLifetime
{
    private const string SchemaUrl = "/content-types/notice/schemas/1";
    private const string RecordUrl = "/content/notice/default";
    private const string PublishedUrl = "/published-content/notice/default";
    private const string ValidSchema = """{"type":"object","properties":{"title":{"type":"string"}},"required":["title"]}""";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();

    [Fact]
    public async Task SharedContent_DraftAndPublication_ReadsOnlyThePublishedRevision()
    {
        using var client = fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        using var registration = await client.PutAsJsonAsync(SchemaUrl, new { Singleton = true, JsonSchema = ValidSchema }, ct);
        registration.EnsureSuccessStatusCode();

        using var draft = await client.PutAsJsonAsync(RecordUrl, new { SchemaVersion = 1, Data = """{"title":"Open"}""", IdempotencyKey = "notice-one" }, ct);
        draft.EnsureSuccessStatusCode();
        var first = (await draft.Content.ReadFromJsonAsync<ContentRevisionResponse>(ct))!;
        using var missing = await client.GetAsync(PublishedUrl, ct);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var publication = await client.PutAsJsonAsync($"{RecordUrl}/publication", new { RevisionId = first.Id }, ct);
        publication.EnsureSuccessStatusCode();
        using var second = await client.PutAsJsonAsync(RecordUrl, new { SchemaVersion = 1, Data = """{"title":"New draft"}""", ExpectedLatestRevisionId = first.Id }, ct);
        second.EnsureSuccessStatusCode();

        var published = await client.GetFromJsonAsync<ContentRevisionResponse>(PublishedUrl, ct);
        published!.Id.Should().Be(first.Id);

        using var stale = await client.PutAsJsonAsync(RecordUrl, new { SchemaVersion = 1, Data = """{"title":"Stale"}""", ExpectedLatestRevisionId = first.Id }, ct);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var unpublish = await client.PutAsJsonAsync($"{RecordUrl}/publication", new { ExpectedPublishedRevisionId = first.Id }, ct);
        unpublish.EnsureSuccessStatusCode();
        using var unpublished = await client.GetAsync(PublishedUrl, ct);
        unpublished.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SharedContent_InvalidSaveAndRetry_ValidatesSchemaAndReusesRevision()
    {
        using var client = fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var registration = new { Singleton = true, JsonSchema = ValidSchema };
        using var registered = await client.PutAsJsonAsync(SchemaUrl, registration, ct);
        registered.EnsureSuccessStatusCode();
        using var duplicate = await client.PutAsJsonAsync(SchemaUrl, registration, ct);
        duplicate.EnsureSuccessStatusCode();

        using var invalidShape = await client.PutAsJsonAsync(RecordUrl, new { SchemaVersion = 1, Data = "{}" }, ct);
        invalidShape.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var invalidKey = await client.PutAsJsonAsync("/content/notice/second", new { SchemaVersion = 1, Data = """{"title":"Open"}""" }, ct);
        invalidKey.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var save = new { SchemaVersion = 1, Data = """{"title":"Open"}""", IdempotencyKey = "test-replay" };
        using var first = await client.PutAsJsonAsync(RecordUrl, save, ct);
        using var replay = await client.PutAsJsonAsync(RecordUrl, save, ct);
        first.EnsureSuccessStatusCode();
        replay.EnsureSuccessStatusCode();

        var revisions = await client.GetFromJsonAsync<ContentListResponse<ContentRevisionResponse>>($"{RecordUrl}/history", ct);
        revisions!.Items.Should().ContainSingle();
    }
}
