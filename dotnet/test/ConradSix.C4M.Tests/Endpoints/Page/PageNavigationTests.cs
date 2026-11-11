namespace ConradSix.C4M.Tests.Endpoints.Page;

using AwesomeAssertions;

using ConradSix.C4M.Tests.Endpoints.Menu;

[Collection(C4MAppCollection.Name)]
public sealed class PageNavigationTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task GetPage_ShouldInheritNavigationMenuFromNearestAncestor()
    {
        var client = fixture.CreateClient();
        var menu = await TestMenuBuilder
            .Create()
            .WithName($"parent-navigation-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var parent = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug($"parent-{Guid.NewGuid()}")
            .WithNavigationMenuId(menu.Id)
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var child = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug($"child-{Guid.NewGuid()}")
            .WithParentId(parent.Id)
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var result = await TestPageHelper.GetPageAsync(
            client,
            child.Id,
            latest: false,
            TestContext.Current.CancellationToken);

        result.NavigationMenuId.Should().BeNull();
        result.EffectiveNavigationMenuId.Should().Be(menu.Id);
    }

    [Fact]
    public async Task GetPage_ShouldPreferDirectNavigationMenuOverAncestor()
    {
        var client = fixture.CreateClient();
        var parentMenu = await TestMenuBuilder
            .Create()
            .WithName($"parent-navigation-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var childMenu = await TestMenuBuilder
            .Create()
            .WithName($"child-navigation-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var parent = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug($"parent-{Guid.NewGuid()}")
            .WithNavigationMenuId(parentMenu.Id)
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var child = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug($"child-{Guid.NewGuid()}")
            .WithParentId(parent.Id)
            .WithNavigationMenuId(childMenu.Id)
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var result = await TestPageHelper.GetPageAsync(
            client,
            child.Id,
            latest: false,
            TestContext.Current.CancellationToken);

        result.NavigationMenuId.Should().Be(childMenu.Id);
        result.EffectiveNavigationMenuId.Should().Be(childMenu.Id);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
