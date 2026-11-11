namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;

using AwesomeAssertions;

using ConradSix.C4M.Model;

[Collection(C4MAppCollection.Name)]
public sealed class CreateMenuTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task CreateMenu_ShouldReturnCreated_WhenValidRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"main-navigation-{Guid.NewGuid()}";

        // Act - Create menu with no items
        var menuResponse = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert
        menuResponse.Should().NotBeNull();
        menuResponse.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnCreated_WhenValidRequestWithRootItem()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"header-menu-{Guid.NewGuid()}";

        // First, create a simple menu action type that doesn't require external references
        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("null-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Create menu with single root item
        var menuResponse = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Home", "{}")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert
        menuResponse.Should().NotBeNull();
        menuResponse.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnCreated_WhenValidRequestWithNestedItems()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"nested-menu-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("nested-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Create menu with nested items (3 levels deep)
        var menuResponse = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithNestedMenuItems(actionType.Id)
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert
        menuResponse.Should().NotBeNull();
        menuResponse.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenNonNavigatingItemHasNoChildren()
    {
        var client = fixture.CreateClient();
        var actionType = await TestMenuActionTypeBuilder
            .Create()
            .WithName("empty")
            .WithEmptySchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestMenuBuilder
            .Create()
            .WithName($"empty-leaf-{Guid.NewGuid()}")
            .WithSingleMenuItem(actionType.Id, "Empty leaf")
            .CreateExpectingAsync(
                client,
                HttpStatusCode.BadRequest,
                TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenNameIsInvalid()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Act & Assert - Name with invalid characters
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName("invalid-name!") // Contains invalid character
            .WithEmptyMenu()
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);

        // Act & Assert - Name starting with hyphen
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName("-invalid-name")
            .WithEmptyMenu()
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);

        // Act & Assert - Empty name
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(string.Empty)
            .WithEmptyMenu()
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenActionTypeIdDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"menu-{Guid.NewGuid()}";
        var nonExistentActionTypeId = Guid.NewGuid();

        // Act & Assert
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(nonExistentActionTypeId)
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenActionDataInvalid()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"menu-{Guid.NewGuid()}";

        // Create action type with required field
        var actionType = await TestMenuActionTypeBuilder
            .CreateWithRequiredField("required-field", "requiredField")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Missing required field
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Item", "{}") // Missing required field
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenTitleTooLong()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"menu-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("long-title")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var veryLongTitle = new string('a', 201); // MaxLength is 200

        // Act & Assert
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, veryLongTitle)
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnConflict_WhenNameAlreadyExistsForRealtor()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"duplicate-menu-{Guid.NewGuid()}";

        // Create first menu
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Try to create second menu with same name for same realtor
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateExpectingAsync(client, HttpStatusCode.Conflict, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldHandleMultipleActionTypes()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"multi-action-menu-{Guid.NewGuid()}";

        var actionType1 = await TestMenuActionTypeBuilder
            .CreateSimple("action-type-1")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var actionType2 = await TestMenuActionTypeBuilder
            .CreateSimple("action-type-2")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var rootItem = new MenuItem(
            Title: "Root",
            ActionTypeId: actionType1.Id,
            ActionData: "{}",
            Children: [
                new MenuItem(
                    Title: "Child with Type 1",
                    ActionTypeId: actionType1.Id,
                    ActionData: "{}",
                    Children: null),
                new MenuItem(
                    Title: "Child with Type 2",
                    ActionTypeId: actionType2.Id,
                    ActionData: "{}",
                    Children: null),
            ]);

        // Act
        var menuResponse = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithRootItems([rootItem])
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert
        menuResponse.Should().NotBeNull();
        menuResponse.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenActionDataContainsNonExistentPageRefs()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"page-ref-menu-{Guid.NewGuid()}";

        // Create action type that supports page references
        var actionType = await TestMenuActionTypeBuilder
            .CreateWithPageRef("page-ref")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create action data with a reference to a non-existent page
        var nonExistentPageId = Guid.NewGuid();
        var actionDataWithPageRef = System.Text.Json.JsonSerializer.Serialize(new
        {
            pageRef = new
            {
                __typename = "page-ref",
                id = nonExistentPageId,
            },
        });

        // Act & Assert - Should fail due to FK constraint violation
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Page Link", actionDataWithPageRef)
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenExceedsMaxDepth()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"deep-menu-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("deep-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create menu with 4 levels deep (exceeds max of 3)
        var rootItem = new MenuItem(
            Title: "Level 1",
            ActionTypeId: actionType.Id,
            ActionData: "{}",
            Children: [
                new MenuItem(
                    Title: "Level 2",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: [
                        new MenuItem(
                            Title: "Level 3",
                            ActionTypeId: actionType.Id,
                            ActionData: "{}",
                            Children: [
                                new MenuItem(
                                    Title: "Level 4 - Too Deep",
                                    ActionTypeId: actionType.Id,
                                    ActionData: "{}",
                                    Children: null),
                            ]),
                    ]),
            ]);

        // Act & Assert
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithRootItems([rootItem])
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldSucceed_WhenExactlyAtMaxDepth()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"max-depth-menu-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("max-depth-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create menu with exactly 3 levels deep (at max)
        var rootItem = new MenuItem(
            Title: "Level 1",
            ActionTypeId: actionType.Id,
            ActionData: "{}",
            Children: [
                new MenuItem(
                    Title: "Level 2",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: [
                        new MenuItem(
                            Title: "Level 3 - Max Depth",
                            ActionTypeId: actionType.Id,
                            ActionData: "{}",
                            Children: null),
                    ]),
            ]);

        // Act
        var menuResponse = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithRootItems([rootItem])
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert
        menuResponse.Should().NotBeNull();
        menuResponse.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenExceedsMaxItemCount()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"too-many-items-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("many-items-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create 21 items (exceeds max of 20)
        var rootItems = new List<MenuItem>();
        for (var i = 0; i < 21; i++)
        {
            rootItems.Add(new MenuItem(
                Title: $"Item {i + 1}",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null));
        }

        // Act & Assert
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithRootItems(rootItems)
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateMenu_ShouldSucceed_WhenExactlyAtMaxItemCount()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"max-items-menu-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("max-items-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create exactly 20 items (at max)
        var rootItems = new List<MenuItem>();
        for (var i = 0; i < 20; i++)
        {
            rootItems.Add(new MenuItem(
                Title: $"Item {i + 1}",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null));
        }

        // Act
        var menuResponse = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithRootItems(rootItems)
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert
        menuResponse.Should().NotBeNull();
        menuResponse.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMenu_ShouldReturnBadRequest_WhenNestedItemsExceedTotalCount()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"nested-too-many-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("nested-many-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create nested structure that totals to 21 items
        // Root (1) + 10 children + 10 grandchildren = 21 total
        var childrenList = new List<MenuItem>();
        for (var i = 0; i < 10; i++)
        {
            childrenList.Add(new MenuItem(
                Title: $"Child {i + 1}",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: [
                    new MenuItem(
                        Title: $"Grandchild {i + 1}",
                        ActionTypeId: actionType.Id,
                        ActionData: "{}",
                        Children: null),
                ]));
        }

        var rootItem = new MenuItem(
            Title: "Root",
            ActionTypeId: actionType.Id,
            ActionData: "{}",
            Children: childrenList);

        // Act & Assert
        await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithRootItems([rootItem])
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
