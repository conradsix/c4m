namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Model;

[Collection(C4MAppCollection.Name)]
public sealed class GetMenuActionTypeByNameTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task GetMenuActionTypeByName_ShouldReturnMenuActionType_WhenExists()
    {
        // Arrange
        var client = fixture.CreateClient();
        var typeName = $"test-action-{Guid.NewGuid()}";

        // Create a menu action type
        var createdType = await TestMenuActionTypeBuilder
            .Create()
            .WithName(typeName)
            .WithEmptySchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await client.GetFromJsonAsync<CmsTypeDescriptor>(
            $"/menu-action-types/by-name/{typeName}",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.Id.Should().Be(createdType.Id);
        response.Name.Should().Be(typeName);
        response.Version.Should().Be(1);
        response.JsonSchema.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetMenuActionTypeByName_ShouldReturnLatestVersion_WhenMultipleVersionsExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var typeName = $"versioned-action-{Guid.NewGuid()}";

        // Create version 1
        await TestMenuActionTypeBuilder
            .Create()
            .WithName(typeName)
            .WithVersion(1)
            .WithEmptySchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create version 2 with different schema
        var v2 = await TestMenuActionTypeBuilder
            .Create()
            .WithName(typeName)
            .WithVersion(2)
            .WithUrlSchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Get without specifying version
        var response = await client.GetFromJsonAsync<CmsTypeDescriptor>(
            $"/menu-action-types/by-name/{typeName}",
            TestContext.Current.CancellationToken);

        // Assert - Should return version 2 (latest)
        response.Should().NotBeNull();
        response.Id.Should().Be(v2.Id);
        response.Name.Should().Be(typeName);
        response.Version.Should().Be(2);
        response.JsonSchema.Should().Contain("url"); // v2 has url schema
    }

    [Fact]
    public async Task GetMenuActionTypeByName_ShouldReturnSpecificVersion_WhenVersionSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var typeName = $"multi-version-action-{Guid.NewGuid()}";

        // Create version 1
        var v1 = await TestMenuActionTypeBuilder
            .Create()
            .WithName(typeName)
            .WithVersion(1)
            .WithPageRefSchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create version 2
        await TestMenuActionTypeBuilder
            .Create()
            .WithName(typeName)
            .WithVersion(2)
            .WithUrlSchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Get version 1 specifically
        var response = await client.GetFromJsonAsync<CmsTypeDescriptor>(
            $"/menu-action-types/by-name/{typeName}?version=1",
            TestContext.Current.CancellationToken);

        // Assert - Should return version 1
        response.Should().NotBeNull();
        response.Id.Should().Be(v1.Id);
        response.Name.Should().Be(typeName);
        response.Version.Should().Be(1);
        response.JsonSchema.Should().Contain("pageRef"); // v1 has pageRef schema
    }

    [Fact]
    public async Task GetMenuActionTypeByName_ShouldReturnNotFound_WhenTypeDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var nonExistentName = $"non-existent-{Guid.NewGuid()}";

        // Act
        var response = await client.GetAsync(
            $"/menu-action-types/by-name/{nonExistentName}",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMenuActionTypeByName_ShouldReturnNotFound_WhenVersionDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var typeName = $"single-version-action-{Guid.NewGuid()}";

        // Create version 1 only
        await TestMenuActionTypeBuilder
            .Create()
            .WithName(typeName)
            .WithVersion(1)
            .WithEmptySchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Try to get non-existent version 999
        var response = await client.GetAsync(
            $"/menu-action-types/by-name/{typeName}?version=999",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMenuActionTypeByName_ShouldBeCaseSensitive()
    {
        // Arrange
        var client = fixture.CreateClient();
        var typeName = $"case-sensitive-action-{Guid.NewGuid()}";

        // Create with lowercase name
        await TestMenuActionTypeBuilder
            .Create()
            .WithName(typeName)
            .WithEmptySchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Try to get with uppercase
        var response = await client.GetAsync(
            $"/menu-action-types/by-name/{typeName.ToUpperInvariant()}",
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMenuActionTypeByName_ShouldReturnCorrectSchema()
    {
        // Arrange
        var client = fixture.CreateClient();
        var typeName = $"schema-test-action-{Guid.NewGuid()}";
        var fieldName = "customField";

        // Create with a required field schema
        await TestMenuActionTypeBuilder
            .Create()
            .WithName(typeName)
            .WithRequiredFieldSchema(fieldName)
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await client.GetFromJsonAsync<CmsTypeDescriptor>(
            $"/menu-action-types/by-name/{typeName}",
            TestContext.Current.CancellationToken);

        // Assert
        response.Should().NotBeNull();
        response.JsonSchema.Should().NotBeNullOrEmpty();
        response.JsonSchema.Should().Contain(fieldName);
        response.JsonSchema.Should().Contain("required");
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
