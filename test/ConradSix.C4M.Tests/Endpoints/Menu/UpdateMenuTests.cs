namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Menu;
using ConradSix.C4M.Model;

[Collection(C4MAppCollection.Name)]
public sealed class UpdateMenuTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task UpdateMenu_ShouldReturnNoContent_WhenValidRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"update-test-{Guid.NewGuid()}";

        // Create initial menu with a single item
        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("update-test")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Initial Item")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update with different content
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [new MenuItem(
                Title: "Updated Item",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null),]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the menu was updated by fetching it
        var getResponse = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedMenu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);
        updatedMenu.Should().NotBeNull();
        updatedMenu.RootItems.Should().HaveCount(1);
        updatedMenu.RootItems[0].Title.Should().Be("Updated Item");
    }

    [Fact]
    public async Task UpdateMenu_ShouldIncrementVersion_WhenSuccessfulUpdate()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"version-test-{Guid.NewGuid()}";

        // Create action type
        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("version-test")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Initial Item")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var initialVersion = initialMenu.Version;

        // Act - First update
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialVersion,
            RootItems: [new MenuItem(
                Title: "Updated Item",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null),]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Get updated menu to check version
        var getResponse = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        var updatedMenu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);

        // Assert - Version should be incremented (may be more than 1 due to EF Core behavior)
        updatedMenu!.Version.Should().BeGreaterThan(initialVersion);

        // Act - Second update with new version
        var secondUpdateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: updatedMenu.Version,
            RootItems: [new MenuItem(
                Title: "Second Update",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null),]);

        var secondResponse = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", secondUpdateRequest, TestContext.Current.CancellationToken);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Get menu again
        var getResponse2 = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        var updatedMenu2 = await getResponse2.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);

        // Assert - Version should be incremented again
        updatedMenu2!.Version.Should().BeGreaterThan(updatedMenu.Version);
    }

    [Fact]
    public async Task UpdateMenu_ShouldReturn409Conflict_WhenVersionMismatch()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"conflict-test-{Guid.NewGuid()}";

        // Create action type
        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("conflict-test")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Initial Item")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var initialVersion = initialMenu.Version;

        // First update - should succeed
        var firstUpdateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialVersion,
            RootItems: [new MenuItem(
                Title: "First Update",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null),]);

        var firstResponse = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", firstUpdateRequest, TestContext.Current.CancellationToken);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act - Try to update with old version (should fail with 409)
        var conflictUpdateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialVersion, // Using old version
            RootItems: [new MenuItem(
                Title: "Conflicting Update",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null),]);

        var conflictResponse = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", conflictUpdateRequest, TestContext.Current.CancellationToken);

        // Assert - Should return 409 Conflict
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Response should contain current menu data with updated version
        var conflictDataJson = await conflictResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        conflictDataJson.Should().NotBeNullOrEmpty();
        using var conflictData = System.Text.Json.JsonDocument.Parse(conflictDataJson);

        // The version should be greater than initial after first update
        conflictData.RootElement.GetProperty("version").GetUInt32().Should().BeGreaterThan(initialVersion);
    }

    [Fact]
    public async Task UpdateMenu_ShouldClearMenu_WhenRootItemIsNull()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"clear-test-{Guid.NewGuid()}";

        // Create initial menu with items
        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("clear-test")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithNestedMenuItems(actionType.Id)
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update with null root item to clear menu
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: null);
        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the menu was cleared
        var getResponse = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedMenu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);
        updatedMenu.Should().NotBeNull();
        updatedMenu.RootItems.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateMenu_ShouldReplaceNestedItems_WhenValidRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"nested-update-{Guid.NewGuid()}";

        // Create initial menu with simple structure
        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("nested-update")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Simple Item")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update with nested structure
        var newRootItem = new MenuItem(
            Title: "New Root",
            ActionTypeId: actionType.Id,
            ActionData: "{}",
            Children: [
                new MenuItem(
                    Title: "Child 1",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: [
                        new MenuItem(
                            Title: "Grandchild 1.1",
                            ActionTypeId: actionType.Id,
                            ActionData: "{}",
                            Children: null),
                        new MenuItem(
                            Title: "Grandchild 1.2",
                            ActionTypeId: actionType.Id,
                            ActionData: "{}",
                            Children: null),
                    ]),
                new MenuItem(
                    Title: "Child 2",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: null),
            ]);

        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [newRootItem]);
        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the structure was updated
        var getResponse = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        var updatedMenu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);

        updatedMenu!.RootItems.Should().HaveCount(1);
        updatedMenu.RootItems[0].Title.Should().Be("New Root");
        updatedMenu.RootItems[0].Children.Should().HaveCount(2);
        updatedMenu.RootItems[0].Children![0].Title.Should().Be("Child 1");
        updatedMenu.RootItems[0].Children[0].Children.Should().HaveCount(2);
        updatedMenu.RootItems[0].Children[1].Title.Should().Be("Child 2");
        updatedMenu.RootItems[0].Children[1].Children.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateMenu_ShouldReturnConflict_WhenVersionMismatch()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"version-test-{Guid.NewGuid()}";

        // Create initial menu
        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("version-test")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithSingleMenuItem(actionType.Id, "Initial Item")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Try to update with wrong version
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: 1, // Wrong version (should be 0 for new menu)
            RootItems: [new MenuItem(
                Title: "Updated Item",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null),]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateMenu_ShouldReturnNotFound_WhenMenuDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var nonExistentMenuId = Guid.NewGuid();

        // Act
        var updateRequest = new UpdateMenuRequest(
            MenuId: nonExistentMenuId,
            Version: 0, // Non-existent menu
            RootItems: null);
        var response = await client.PutAsJsonAsync($"/menus/{nonExistentMenuId}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateMenu_ShouldReturnBadRequest_WhenActionTypeDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"invalid-action-{Guid.NewGuid()}";

        // Create initial menu
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update with non-existent action type
        var nonExistentActionTypeId = Guid.NewGuid();
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [new MenuItem(
                Title: "Invalid Item",
                ActionTypeId: nonExistentActionTypeId,
                ActionData: "{}",
                Children: null),]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMenu_ShouldReturnBadRequest_WhenActionDataInvalid()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"invalid-data-{Guid.NewGuid()}";

        // Create action type with required field
        var actionType = await TestMenuActionTypeBuilder
            .CreateWithRequiredField("required-field-update", "requiredField")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update with invalid action data (missing required field)
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [new MenuItem(
                Title: "Invalid Data Item",
                ActionTypeId: actionType.Id,
                ActionData: "{}", // Missing required field
                Children: null),]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMenu_ShouldReturnBadRequest_WhenTitleTooLong()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"long-title-update-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("long-title-update")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var veryLongTitle = new string('a', 201); // MaxLength is 200

        // Act
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [new MenuItem(
                Title: veryLongTitle,
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: null),]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMenu_ShouldHandleMultipleActionTypes()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"multi-action-update-{Guid.NewGuid()}";

        var actionType1 = await TestMenuActionTypeBuilder
            .CreateSimple("action-type-update-1")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var actionType2 = await TestMenuActionTypeBuilder
            .CreateSimple("action-type-update-2")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update with multiple action types
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [new MenuItem(
                Title: "Root with Type 1",
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
                ]),]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify both action types are present
        var getResponse = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        var updatedMenu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);

        updatedMenu!.RootItems.Should().HaveCount(1);
        updatedMenu.RootItems[0].ActionTypeName.Should().NotBeNullOrEmpty();
        updatedMenu.RootItems[0].Children!.Should().HaveCount(2);
        updatedMenu.RootItems[0].Children[0].ActionTypeName.Should().NotBeNullOrEmpty();
        updatedMenu.RootItems[0].Children[1].ActionTypeName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateMenu_ShouldCompletelyReplaceContent()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"complete-replace-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("complete-replace")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu with complex nested structure
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithNestedMenuItems(actionType.Id)
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Verify initial structure
        var initialGetResponse = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        var initialMenuData = await initialGetResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);
        initialMenuData!.RootItems.Should().HaveCount(1);
        initialMenuData.RootItems[0].Title.Should().Be("Products");
        initialMenuData.RootItems[0].Children.Should().HaveCount(2);

        // Act - Replace with completely different structure
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [new MenuItem(
                Title: "Completely Different",
                ActionTypeId: actionType.Id,
                ActionData: /*lang=json,strict*/ """{"custom": "data"}""",
                Children: null),]);

        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify complete replacement
        var getResponse = await client.GetAsync($"/menus/{initialMenu.Id}", TestContext.Current.CancellationToken);
        var updatedMenu = await getResponse.Content.ReadFromJsonAsync<GetMenuResponse>(TestContext.Current.CancellationToken);

        updatedMenu!.RootItems.Should().HaveCount(1);
        updatedMenu.RootItems[0].Title.Should().Be("Completely Different");
        updatedMenu.RootItems[0].Children.Should().BeEmpty();
        updatedMenu.RootItems[0].ActionData.Should().Be(/*lang=json,strict*/ """{"custom": "data"}""");

        // Note: SortOrder is not exposed in GetMenuRequestMenuItem
    }

    [Fact]
    public async Task UpdateMenu_ShouldReturnBadRequest_WhenExceedsMaxDepth()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"update-deep-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("update-deep")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create menu structure with 4 levels deep (exceeds max of 3)
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [new MenuItem(
                Title: "Level 1",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: [new MenuItem(
                    Title: "Level 2",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: [new MenuItem(
                        Title: "Level 3",
                        ActionTypeId: actionType.Id,
                        ActionData: "{}",
                        Children: [new MenuItem(
                            Title: "Level 4 - Too Deep",
                            ActionTypeId: actionType.Id,
                            ActionData: "{}",
                            Children: null),]),]),]),]);

        // Act
        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMenu_ShouldSucceed_WhenExactlyAtMaxDepth()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"update-max-depth-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("update-max-depth")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create menu structure with exactly 3 levels deep (at max)
        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: [new MenuItem(
                Title: "Level 1",
                ActionTypeId: actionType.Id,
                ActionData: "{}",
                Children: [new MenuItem(
                    Title: "Level 2",
                    ActionTypeId: actionType.Id,
                    ActionData: "{}",
                    Children: [new MenuItem(
                        Title: "Level 3 - Max Depth",
                        ActionTypeId: actionType.Id,
                        ActionData: "{}",
                        Children: null),]),]),]);

        // Act
        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateMenu_ShouldReturnBadRequest_WhenExceedsMaxItemCount()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"update-many-items-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("update-many-items")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
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

        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: rootItems);

        // Act
        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMenu_ShouldSucceed_WhenExactlyAtMaxItemCount()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuName = $"update-max-items-{Guid.NewGuid()}";

        var actionType = await TestMenuActionTypeBuilder
            .CreateSimple("update-max-items")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial menu
        var initialMenu = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName(menuName)
            .WithEmptyMenu()
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

        var updateRequest = new UpdateMenuRequest(
            MenuId: initialMenu.Id,
            Version: initialMenu.Version,
            RootItems: rootItems);

        // Act
        var response = await client.PutAsJsonAsync($"/menus/{initialMenu.Id}", updateRequest, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
