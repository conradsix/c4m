namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Menu;
using ConradSix.C4M.Model;

[Collection(C4MAppCollection.Name)]
public sealed class MultipleRootItemsTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task CreateMenu_ShouldSupportMultipleRootItems()
    {
        // Arrange
        var client = fixture.CreateClient();
        var menuName = $"multi-root-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("multi-root-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Create menu with multiple root items
        var request = new CreateMenuRequest(
            Name: menuName,
            RootItems: [
                new MenuItem(
                    Title: "Home",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: null),
                new MenuItem(
                    Title: "Products",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: [
                        new MenuItem(
                            Title: "Category 1",
                            ActionTypeId: actionType.Id,
                            ActionData: "{}",
                            Children: null),
                        new MenuItem(
                            Title: "Category 2",
                            ActionTypeId: actionType.Id,
                            ActionData: "{}",
                            Children: null),
                    ]),
                new MenuItem(
                    Title: "About",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: null),
                new MenuItem(
                    Title: "Contact",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: null),
            ]);

        var createResponse = await client.PostAsJsonAsync("/menus", request, TestContext.Current.CancellationToken);

        // Assert
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdMenu = await createResponse.Content.ReadFromJsonAsync<CreateMenuResponse>(TestContext.Current.CancellationToken);
        createdMenu.Should().NotBeNull();

        // Verify the menu was created with all root items
        var getResponse = await client.GetAsync($"/menus/{createdMenu.Id}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var menu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);
        menu.Should().NotBeNull();
        menu.RootItems.Should().HaveCount(4);

        // Verify root items are in correct order
        menu.RootItems[0].Title.Should().Be("Home");
        menu.RootItems[1].Title.Should().Be("Products");
        menu.RootItems[2].Title.Should().Be("About");
        menu.RootItems[3].Title.Should().Be("Contact");

        // Verify nested structure is preserved
        menu.RootItems[1].Children.Should().HaveCount(2);
        menu.RootItems[1].Children[0].Title.Should().Be("Category 1");
        menu.RootItems[1].Children[1].Title.Should().Be("Category 2");
    }

    [Fact]
    public async Task UpdateMenu_ShouldReplaceWithMultipleRootItems()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"update-multi-root-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("update-multi-root")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu with single root
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Initial Single Root")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update with multiple root items
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [
                new MenuItem(
                    Title: "First",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: null),
                new MenuItem(
                    Title: "Second",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: null),
                new MenuItem(
                    Title: "Third",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: null),
            ]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the menu was updated
        var getResponse = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        var updatedMenu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);

        updatedMenu!.RootItems.Should().HaveCount(3);
        updatedMenu.RootItems[0].Title.Should().Be("First");
        updatedMenu.RootItems[1].Title.Should().Be("Second");
        updatedMenu.RootItems[2].Title.Should().Be("Third");
    }

    [Fact]
    public async Task GetMenu_ShouldReturnEmptyArrayWhenNoRootItems()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"empty-roots-{Guid.NewGuid()}";

        // Create menu with no root items
        var menu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var getResponse = await client.GetAsync($"/menus/{menu.Id}", TestContext.Current.CancellationToken);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var menuResponse = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);

        menuResponse.Should().NotBeNull();
        menuResponse.RootItems.Should().NotBeNull();
        menuResponse.RootItems.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateMenu_ShouldMaintainSortOrderForMultipleRoots()
    {
        // Arrange
        var client = fixture.CreateClient();
        var menuName = $"sort-order-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("sort-order-test")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Create menu with specific order
        var request = new CreateMenuRequest(
            Name: menuName,
            RootItems: [
                new MenuItem(Title: "Z-Last", ActionTypeId: actionType.Id, ActionData: "{}", Children: null),
                new MenuItem(Title: "A-First", ActionTypeId: actionType.Id, ActionData: "{}", Children: null),
                new MenuItem(Title: "M-Middle", ActionTypeId: actionType.Id, ActionData: "{}", Children: null),
            ]);

        var createResponse = await client.PostAsJsonAsync("/menus", request, TestContext.Current.CancellationToken);
        var createdMenu = await createResponse.Content.ReadFromJsonAsync<CreateMenuResponse>(TestContext.Current.CancellationToken);

        // Assert - Order should be preserved as submitted, not alphabetical
        var getResponse = await client.GetAsync($"/menus/{createdMenu!.Id}", TestContext.Current.CancellationToken);
        var menu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);

        menu!.RootItems.Should().HaveCount(3);
        menu.RootItems[0].Title.Should().Be("Z-Last");
        menu.RootItems[1].Title.Should().Be("A-First");
        menu.RootItems[2].Title.Should().Be("M-Middle");
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
