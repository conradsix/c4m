#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Menu;
using ConradSix.C4M.Model;

public sealed class TestMenuBuilder
{
    private string _name = "test-menu";
    private IReadOnlyList<MenuItem>? _rootItems;

    public static TestMenuBuilder Create() => new();

    public TestMenuBuilder WithRealtorId(Guid realtorId)
    {
        _ = realtorId;
        return this;
    }

    public TestMenuBuilder WithName(string name)
    {
        this._name = name;
        return this;
    }

    public TestMenuBuilder WithRootItems(IReadOnlyList<MenuItem>? rootItems)
    {
        this._rootItems = rootItems;
        return this;
    }

    public TestMenuBuilder WithEmptyMenu()
    {
        this._rootItems = null;
        return this;
    }

    public TestMenuBuilder WithSingleMenuItem(Guid actionTypeId, string title = "Home", string actionData = "{}")
    {
        this._rootItems = [
            new MenuItem(
                Title: title,
                ActionTypeId: actionTypeId,
                ActionData: actionData,
                Children: null),
        ];
        return this;
    }

    public TestMenuBuilder WithNestedMenuItems(Guid actionTypeId)
    {
        this._rootItems = [
            new MenuItem(
                Title: "Products",
                ActionTypeId: actionTypeId,
                ActionData: "{}",
                Children: [
                    new MenuItem(
                        Title: "Category 1",
                        ActionTypeId: actionTypeId,
                        ActionData: "{}",
                        Children: [
                            new MenuItem(
                                Title: "Subcategory 1.1",
                                ActionTypeId: actionTypeId,
                                ActionData: "{}",
                                Children: null),
                            new MenuItem(
                                Title: "Subcategory 1.2",
                                ActionTypeId: actionTypeId,
                                ActionData: "{}",
                                Children: null),
                        ]),
                    new MenuItem(
                        Title: "Category 2",
                        ActionTypeId: actionTypeId,
                        ActionData: "{}",
                        Children: null),
                ]),
        ];
        return this;
    }

    public TestMenuBuilder WithInvalidActionData(Guid actionTypeId)
    {
        this._rootItems = [
            new MenuItem(
                Title: "Invalid Item",
                ActionTypeId: actionTypeId,
                ActionData: "not-json",
                Children: null),
        ];
        return this;
    }

    public async Task<TestMenuResult> CreateAsync(
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateMenuRequest(
            Name: this._name,
            RootItems: this._rootItems);

        var response = await client.PostAsJsonAsync("/menus", request, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var menu = await response.Content.ReadFromJsonAsync<CreateMenuResponse>(cancellationToken);
        menu.Should().NotBeNull();

        // Fetch the menu details to get the version
        var getResponse = await client.GetAsync($"/menus/{menu.Id}", cancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var menuDetails = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(cancellationToken);
        menuDetails.Should().NotBeNull();

        return new TestMenuResult(menu.Id, menuDetails.Version);
    }

    public async Task CreateExpectingAsync(
        HttpClient client,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateMenuRequest(
            Name: this._name,
            RootItems: this._rootItems);

        var response = await client.PostAsJsonAsync("/menus", request, cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);
    }
}

public sealed record TestMenuResult(Guid Id, uint Version);
