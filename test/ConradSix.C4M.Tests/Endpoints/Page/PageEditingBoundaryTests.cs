#pragma warning disable CA2025 // Both HTTP tasks are awaited together before disposing the client.
namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

[Collection(C4MAppCollection.Name)]
public sealed class PageEditingBoundaryTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task DetailsAndDrafts_SaveConcurrently_WithoutChangingEachOthersState()
    {
        using var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder.Create().CreateAsync(client, TestContext.Current.CancellationToken);
        var detailsTask = client.PutAsJsonAsync(
            $"/pages/{page.Id}/details",
            new UpdatePageDetailsRequest(null, "renamed", page.PageDetails.Version),
            TestContext.Current.CancellationToken);
        var draftTask = client.PutAsJsonAsync(
            $"/pages/{page.Id}",
            new UpdatePageRequest(page.PageDetails.PageVersionId, [], [], new("New draft", "New description", null)),
            TestContext.Current.CancellationToken);
        await Task.WhenAll(detailsTask, draftTask);
        (await detailsTask).StatusCode.Should().Be(HttpStatusCode.OK);
        (await draftTask).StatusCode.Should().Be(HttpStatusCode.Created);
        var latest = await TestPageHelper.GetPageAsync(client, page.Id, true, TestContext.Current.CancellationToken);
        latest.Slug.Should().Be("renamed");
        latest.Seo.Title.Should().Be("New draft");
        latest.CurrentPageVersionId.Should().Be(page.PageDetails.PageVersionId);
        latest.IsPublished.Should().BeTrue();
        (await TestPageHelper.GetPageHistoryAsync(client, page.Id, TestContext.Current.CancellationToken)).Versions.Should().HaveCount(2);
        var published = await TestPageHelper.GetPageAsync(client, page.Id, TestContext.Current.CancellationToken);
        published.Slug.Should().Be("renamed");
        published.Seo.Title.Should().Be(page.PageDetails.Seo.Title);
    }

    [Fact]
    public async Task DetailsSave_CreatesNoVersion_AndRejectsStaleDetailsToken()
    {
        using var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder.Create().CreateAsync(client, TestContext.Current.CancellationToken);
        var request = new UpdatePageDetailsRequest(null, "renamed", page.PageDetails.Version);
        (await client.PutAsJsonAsync($"/pages/{page.Id}/details", request, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/pages/{page.Id}/details", request with { Slug = "stale" }, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var history = await TestPageHelper.GetPageHistoryAsync(client, page.Id, TestContext.Current.CancellationToken);
        history.Versions.Should().ContainSingle().Which.PageVersionId.Should().Be(page.PageDetails.PageVersionId);
        var historical = await TestPageHelper.GetPageVersionAsync(client, page.PageDetails.PageVersionId, TestContext.Current.CancellationToken);
        historical.Slug.Should().Be("renamed");
        historical.Seo.Should().BeEquivalentTo(page.PageDetails.Seo);
        historical.Blocks.Should().BeEquivalentTo(page.PageDetails.Blocks);
    }

    [Fact]
    public async Task UnpublishedCurrent_IsPrivate_AndSavingDraftDoesNotSelectOrPublishIt()
    {
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/pages", new CreatePageRequest(null, "private", [], new("First", "First description", null), Publish: false), TestContext.Current.CancellationToken);
        var created = (await response.Content.ReadFromJsonAsync<CreatePageResponse>(TestContext.Current.CancellationToken))!;
        var initial = await TestPageHelper.GetPageAsync(client, created.Id, true, TestContext.Current.CancellationToken);
        initial.CurrentPageVersionId.Should().Be(initial.PageVersionId);
        initial.IsPublished.Should().BeFalse();
        var draft = await TestUpdatePageRequestBuilder.Create().WithLatestVersion(initial.PageVersionId).WithSeo(new("Second", "Second description", null)).UpdateAsync(client, created.Id, TestContext.Current.CancellationToken);
        draft.PageDetails.CurrentPageVersionId.Should().Be(initial.PageVersionId);
        draft.PageDetails.Version.Should().Be(initial.Version);
        (await client.GetAsync($"/pages/{created.Id}", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/pages/by-path?path=/private", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var list = (await client.GetFromJsonAsync<ListPagesResponse>("/pages", TestContext.Current.CancellationToken))!;
        list.Pages.Should().ContainSingle().Which.IsPublished.Should().BeFalse();
        list.Pages[0].CurrentPageVersionId.Should().Be(initial.PageVersionId);
        (await client.PostAsJsonAsync($"/pages/{created.Id}/publish", new PagePublicationRequest(initial.PageVersionId, false), TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TestPageHelper.GetPageAsync(client, created.Id, TestContext.Current.CancellationToken)).PageVersionId.Should().Be(initial.PageVersionId);
    }

    [Fact]
    public async Task SelectingCurrentWhileUnpublished_AllowsIncompleteSeo_ButPublishValidatesIt()
    {
        using var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder.Create().CreateAsync(client, TestContext.Current.CancellationToken);
        (await client.PostAsJsonAsync($"/pages/{page.Id}/unpublish", new PagePublicationRequest(page.PageDetails.PageVersionId, true), TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var draft = await TestUpdatePageRequestBuilder.Create().WithLatestVersion(page.PageDetails.PageVersionId).WithSeo(new(null, null, null)).UpdateAsync(client, page.Id, TestContext.Current.CancellationToken);
        await TestPageHelper.SetCurrentVersionAsync(client, draft.PageDetails.PageVersionId, TestContext.Current.CancellationToken);
        var selected = await TestPageHelper.GetPageAsync(client, page.Id, true, TestContext.Current.CancellationToken);
        selected.IsPublished.Should().BeFalse();
        selected.CurrentPageVersionId.Should().Be(draft.PageDetails.PageVersionId);
        (await client.PostAsJsonAsync($"/pages/{page.Id}/publish", new PagePublicationRequest(selected.CurrentPageVersionId, false), TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PublicationAndSelection_RejectStaleExpectedState()
    {
        using var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder.Create().CreateAsync(client, TestContext.Current.CancellationToken);
        var draft = await TestUpdatePageRequestBuilder.Create().WithLatestVersion(page.PageDetails.PageVersionId).UpdateAsync(client, page.Id, TestContext.Current.CancellationToken);
        var state = new PagePublicationRequest(page.PageDetails.PageVersionId, true);
        (await client.PostAsJsonAsync($"/pages/{page.Id}/unpublish", state, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync($"/pages/by-version/{draft.PageDetails.PageVersionId}", state, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await TestPageHelper.SetCurrentVersionAsync(client, draft.PageDetails.PageVersionId, TestContext.Current.CancellationToken);
        (await client.PostAsJsonAsync($"/pages/{page.Id}/publish", state with { ExpectedIsPublished = false }, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var selected = await TestPageHelper.GetPageAsync(client, page.Id, true, TestContext.Current.CancellationToken);
        selected.IsPublished.Should().BeFalse();
        selected.CurrentPageVersionId.Should().Be(draft.PageDetails.PageVersionId);
    }

    public async ValueTask DisposeAsync() => await fixture.TearDownTestAsync();
}
