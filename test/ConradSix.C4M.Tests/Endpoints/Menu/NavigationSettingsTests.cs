namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Menu;
using ConradSix.C4M.Endpoints.NavigationSettings;

[Collection(C4MAppCollection.Name)]
public sealed class NavigationSettingsTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task NavigationSettings_ShouldConfigureAndReturnDefaultMenu()
    {
        var client = fixture.CreateClient();
        var menu = await TestMenuBuilder
            .Create()
            .WithName($"default-navigation-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var initialResponse = await client.GetAsync(
            "/navigation-settings",
            TestContext.Current.CancellationToken);
        var initial = await initialResponse.Content.ReadFromJsonAsync<NavigationSettingsResponse>(
            TestContext.Current.CancellationToken);
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        initial.Should().NotBeNull();
        initial.DefaultMenuId.Should().BeNull();
        initial.FooterMenuId.Should().BeNull();
        initial.Version.Should().Be(0);

        var updateResponse = await client.PutAsJsonAsync(
            "/navigation-settings",
            new UpdateNavigationSettingsRequest(menu.Id, null, initial.Version),
            TestContext.Current.CancellationToken);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NavigationSettingsResponse>(
            TestContext.Current.CancellationToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Should().NotBeNull();
        updated.DefaultMenuId.Should().Be(menu.Id);
        updated.Version.Should().NotBe(0);

        var defaultResponse = await client.GetAsync(
            "/menus/default",
            TestContext.Current.CancellationToken);
        var defaultMenu = await defaultResponse.Content.ReadFromJsonAsync<GetMenuResponse>(
            TestContext.Current.CancellationToken);
        defaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        defaultMenu.Should().NotBeNull();
        defaultMenu.Id.Should().Be(menu.Id);
    }

    [Fact]
    public async Task NavigationSettings_ShouldConfigureAndReturnFooterMenu()
    {
        var client = fixture.CreateClient();
        var headerMenu = await TestMenuBuilder
            .Create()
            .WithName($"header-navigation-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var footerMenu = await TestMenuBuilder
            .Create()
            .WithName($"footer-navigation-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var unsetResponse = await client.GetAsync(
            "/menus/footer",
            TestContext.Current.CancellationToken);
        unsetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var updateResponse = await client.PutAsJsonAsync(
            "/navigation-settings",
            new UpdateNavigationSettingsRequest(headerMenu.Id, footerMenu.Id, 0),
            TestContext.Current.CancellationToken);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NavigationSettingsResponse>(
            TestContext.Current.CancellationToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        updated.Should().NotBeNull();
        updated.DefaultMenuId.Should().Be(headerMenu.Id);
        updated.FooterMenuId.Should().Be(footerMenu.Id);

        var footerResponse = await client.GetAsync(
            "/menus/footer",
            TestContext.Current.CancellationToken);
        var footer = await footerResponse.Content.ReadFromJsonAsync<GetMenuResponse>(
            TestContext.Current.CancellationToken);
        footerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        footer.Should().NotBeNull();
        footer.Id.Should().Be(footerMenu.Id);
    }

    [Fact]
    public async Task NavigationSettings_ShouldRejectMissingFooterMenu()
    {
        var client = fixture.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/navigation-settings",
            new UpdateNavigationSettingsRequest(null, Guid.NewGuid(), 0),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("footer menu");
    }

    [Fact]
    public async Task NavigationSettings_ShouldRejectStaleVersion()
    {
        var client = fixture.CreateClient();
        var menu = await TestMenuBuilder
            .Create()
            .WithName($"stale-navigation-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var firstUpdate = await client.PutAsJsonAsync(
            "/navigation-settings",
            new UpdateNavigationSettingsRequest(menu.Id, null, 0),
            TestContext.Current.CancellationToken);
        firstUpdate.StatusCode.Should().Be(HttpStatusCode.OK);

        var staleUpdate = await client.PutAsJsonAsync(
            "/navigation-settings",
            new UpdateNavigationSettingsRequest(null, null, 0),
            TestContext.Current.CancellationToken);

        staleUpdate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
