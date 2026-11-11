namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

[Collection(C4MAppCollection.Name)]
public sealed class GetPagePathAvailabilityTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnAvailable_WhenTopLevelPathIsFree()
    {
        // Arrange
        var client = fixture.CreateClient();

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "fresh-page", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be("/fresh-page");
        response.Status.Should().Be(PagePathAvailability.Available);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnTaken_WhenTopLevelPathExists()
    {
        // Arrange
        var client = fixture.CreateClient();
        await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("existing")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "existing", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be("/existing");
        response.Status.Should().Be(PagePathAvailability.Taken);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnAvailable_WhenSlugIsUsedUnderAnotherParent()
    {
        // Arrange
        var client = fixture.CreateClient();
        var guides = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("guides")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var areas = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("areas")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        await TestCreatePageRequestBuilder
            .Create()
            .WithParentId(guides.Id)
            .WithSlug("marbella")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "marbella", areas.Id, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be("/areas/marbella");
        response.Status.Should().Be(PagePathAvailability.Available);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnTaken_WhenNestedPathExists()
    {
        // Arrange
        var client = fixture.CreateClient();
        var guides = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("guides")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        await TestCreatePageRequestBuilder
            .Create()
            .WithParentId(guides.Id)
            .WithSlug("marbella")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "marbella", guides.Id, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be("/guides/marbella");
        response.Status.Should().Be(PagePathAvailability.Taken);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnRestricted_WhenTopLevelSegmentIsReserved()
    {
        // Arrange
        var client = fixture.CreateClient();

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "api", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be("/api");
        response.Status.Should().Be(PagePathAvailability.Restricted);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnAvailable_WhenReservedSegmentIsNested()
    {
        // Arrange
        var client = fixture.CreateClient();
        var parent = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("parent")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "api", parent.Id, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be("/parent/api");
        response.Status.Should().Be(PagePathAvailability.Available);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnAvailable_WhenPathBelongsToExcludedPage()
    {
        // Arrange
        var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("existing")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "existing", pageId: page.Id, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be("/existing");
        response.Status.Should().Be(PagePathAvailability.Available);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnCircularParent_WhenParentIsThePage()
    {
        // Arrange
        var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("existing")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "existing", page.Id, page.Id, TestContext.Current.CancellationToken);

        // Assert
        response.Status.Should().Be(PagePathAvailability.CircularParent);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnCircularParent_WhenParentIsADescendant()
    {
        // Arrange
        var client = fixture.CreateClient();
        var root = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("root")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var child = await TestCreatePageRequestBuilder
            .Create()
            .WithParentId(root.Id)
            .WithSlug("child")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var grandchild = await TestCreatePageRequestBuilder
            .Create()
            .WithParentId(child.Id)
            .WithSlug("grandchild")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "root", grandchild.Id, root.Id, TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be("/root/child/grandchild/root");
        response.Status.Should().Be(PagePathAvailability.CircularParent);
    }

    [Fact]
    public async Task GetPagePathAvailability_ShouldReturnParentNotFound_WhenParentDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityAsync(
            client, "orphan", Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().BeNull();
        response.Status.Should().Be(PagePathAvailability.ParentNotFound);
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("has space")]
    [InlineData("trailing-")]
    public async Task GetPagePathAvailability_ShouldReturnBadRequest_WhenSlugIsInvalid(string slug)
    {
        // Arrange
        var client = fixture.CreateClient();

        // Act
        var response = await TestPageHelper.GetPagePathAvailabilityExpectingAsync(
            client, slug, HttpStatusCode.BadRequest, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await TestPageHelper.AssertValidationErrorFieldAsync(response, "slug");
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
