namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Menu;

[Collection(C4MAppCollection.Name)]
public sealed class ListMenusTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task ListMenus_ShouldReturnEmptyList_WhenNoMenusExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Act
        var response = await client.GetFromJsonAsync<ListMenusResponse>(
            $"/menus?realtorId={realtorId}&limit=20&offset=0",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
        response.Limit.Should().Be(20);
        response.Offset.Should().Be(0);
    }

    [Fact]
    public async Task ListMenus_ShouldReturnMenus_WhenMenusExistForRealtor()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create multiple menus for the realtor
        var menu1 = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName($"menu-1-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var menu2 = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName($"menu-2-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var menu3 = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithName($"menu-3-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await client.GetFromJsonAsync<ListMenusResponse>(
            $"/menus?realtorId={realtorId}&limit=20&offset=0",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().HaveCount(3);
        response.TotalCount.Should().Be(3);
        response.Items.Select(m => m.Id).Should().Contain([menu1.Id, menu2.Id, menu3.Id]);
    }

    [Fact]
    public async Task ListMenus_ShouldRespectPaginationLimit()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create 5 menus
        for (var i = 0; i < 5; i++)
        {
            await TestMenuBuilder
                .Create()
                .WithRealtorId(realtorId)
                .WithName($"menu-{i}-{Guid.NewGuid()}")
                .WithEmptyMenu()
                .CreateAsync(client, TestContext.Current.CancellationToken);
        }

        // Act - Request only 2 items
        var response = await client.GetFromJsonAsync<ListMenusResponse>(
            $"/menus?realtorId={realtorId}&limit=2&offset=0",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().HaveCount(2);
        response.TotalCount.Should().Be(5);
        response.Limit.Should().Be(2);
        response.Offset.Should().Be(0);
    }

    [Fact]
    public async Task ListMenus_ShouldRespectPaginationOffset()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuIds = new List<Guid>();

        // Create 5 menus with small delays to ensure ordering
        for (var i = 0; i < 5; i++)
        {
            var menu = await TestMenuBuilder
                .Create()
                .WithRealtorId(realtorId)
                .WithName($"menu-{i}-{Guid.NewGuid()}")
                .WithEmptyMenu()
                .CreateAsync(client, TestContext.Current.CancellationToken);
            menuIds.Add(menu.Id);
            await Task.Delay(10, TestContext.Current.CancellationToken); // Small delay to ensure different CreatedAt times
        }

        // Act - Skip first 2 items
        var response = await client.GetFromJsonAsync<ListMenusResponse>(
            $"/menus?realtorId={realtorId}&limit=10&offset=2",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().HaveCount(3);
        response.TotalCount.Should().Be(5);
        response.Offset.Should().Be(2);

        // The first 2 menus should not be in the response
        response.Items.Select(m => m.Id).Should().NotContain(menuIds.Take(2));
    }

    [Fact]
    public async Task ListMenus_ShouldReturnAllMenus()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtor1 = Guid.NewGuid();
        var realtor2 = Guid.NewGuid();

        // Create menus for realtor1
        var menu1 = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtor1)
            .WithName($"realtor1-menu-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create menus for realtor2
        var menu2 = await TestMenuBuilder
            .Create()
            .WithRealtorId(realtor2)
            .WithName($"realtor2-menu-{Guid.NewGuid()}")
            .WithEmptyMenu()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await client.GetFromJsonAsync<ListMenusResponse>(
            $"/menus?realtorId={realtor1}&limit=20&offset=0",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().HaveCount(2);
        response.Items.Select(m => m.Id).Should().Contain([menu1.Id, menu2.Id]);
    }

    [Fact]
    public async Task ListMenus_ShouldOrderByCreatedAtAscending()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var menuNames = new List<string>();

        // Create menus with delays to ensure different CreatedAt times
        for (var i = 0; i < 3; i++)
        {
            var name = $"menu-{i}-{Guid.NewGuid()}";
            menuNames.Add(name);
            await TestMenuBuilder
                .Create()
                .WithRealtorId(realtorId)
                .WithName(name)
                .WithEmptyMenu()
                .CreateAsync(client, TestContext.Current.CancellationToken);
            await Task.Delay(50, TestContext.Current.CancellationToken); // Ensure different CreatedAt timestamps
        }

        // Act
        var response = await client.GetFromJsonAsync<ListMenusResponse>(
            $"/menus?realtorId={realtorId}&limit=20&offset=0",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().HaveCount(3);

        // Verify ordering by CreatedAt (ascending)
        for (var i = 0; i < response.Items.Count - 1; i++)
        {
            response.Items[i].CreatedAt.Should().BeBefore(response.Items[i + 1].CreatedAt);
        }
    }

    [Fact]
    public async Task ListMenus_ShouldReturnBadRequest_WhenLimitIsNegative()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Act & Assert
        var response = await client.GetAsync(
            $"/menus?realtorId={realtorId}&limit=-1&offset=0",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListMenus_ShouldReturnBadRequest_WhenOffsetIsNegative()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Act & Assert
        var response = await client.GetAsync(
            $"/menus?realtorId={realtorId}&limit=20&offset=-1",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListMenus_ShouldReturnBadRequest_WhenLimitIsTooLarge()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Act & Assert - Assuming there's a max limit validation (e.g., 100)
        var response = await client.GetAsync(
            $"/menus?realtorId={realtorId}&limit=1001&offset=0",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListMenus_ShouldUseDefaultValues_WhenLimitAndOffsetNotProvided()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Act
        var response = await client.GetFromJsonAsync<ListMenusResponse>(
            $"/menus?realtorId={realtorId}",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Limit.Should().Be(20); // Default limit
        response.Offset.Should().Be(0);   // Default offset
    }

    [Fact]
    public async Task ListMenus_ShouldReturnEmptyList_WhenOffsetExceedsTotalCount()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create 2 menus
        for (var i = 0; i < 2; i++)
        {
            await TestMenuBuilder
                .Create()
                .WithRealtorId(realtorId)
                .WithName($"menu-{i}-{Guid.NewGuid()}")
                .WithEmptyMenu()
                .CreateAsync(client, TestContext.Current.CancellationToken);
        }

        // Act - Request with offset beyond total count
        var response = await client.GetFromJsonAsync<ListMenusResponse>(
            $"/menus?realtorId={realtorId}&limit=10&offset=10",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(2);
        response.Offset.Should().Be(10);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
