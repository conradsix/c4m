namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;

using AwesomeAssertions;

[Collection(C4MAppCollection.Name)]
public sealed class UpdatePageDetailsTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task UpdatePageDetails_ShouldRejectReservedRootPath()
    {
        var client = fixture.CreateClient();
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("reserved-path")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var page = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("allowed-path")
            .WithSingleTextBlock(blockType.Id, "Content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var response = await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithSlug("reserved")
            .WithVersion(page.PageDetails.Version)
            .UpdateExpectingAsync(
                client,
                page.Id,
                HttpStatusCode.BadRequest,
                TestContext.Current.CancellationToken);

        await TestPageHelper.AssertValidationErrorFieldAsync(response, "slug");
        (await TestPageHelper.GetPagePathAsync(client, page.Id, TestContext.Current.CancellationToken))
            .Path.Should().Be("/allowed-path");
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldReturnBadRequest_WhenSlugIsEmpty()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert
        await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithSlug(string.Empty) // Empty slug should fail
            .WithVersion(initialPage.PageDetails.Version)
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldReturnBadRequest_WhenSlugContainsInvalidCharacters()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert
        await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithSlug("Invalid Slug With Spaces!") // Invalid characters
            .WithVersion(initialPage.PageDetails.Version)
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldReturnConflict_WhenUpdatedSlugAlreadyExists()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create first page
        await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("existing-slug")
            .WithSingleTextBlock(blockType.Id, "First page")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create second page with different slug
        var secondPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("different-slug")
            .WithSingleTextBlock(blockType.Id, "Second page")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Try to update second page to use existing slug
        var response = await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithSlug("existing-slug") // Conflicting slug
            .WithVersion(secondPage.PageDetails.Version)
            .UpdateExpectingAsync(client, secondPage.Id, HttpStatusCode.Conflict, TestContext.Current.CancellationToken);

        await TestPageHelper.AssertValidationErrorFieldAsync(response, "slug");
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldUpdateParentId_WhenMovingPageToNewParent()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create parent page
        var parentPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("parent")
            .WithSingleTextBlock(blockType.Id, "Parent content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create new parent page
        var newParentPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("new-parent")
            .WithSingleTextBlock(blockType.Id, "New parent content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create child page under first parent
        var childPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithParentId(parentPage.Id)
            .WithSlug("child")
            .WithSingleTextBlock(blockType.Id, "Child content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Move child to new parent
        var updateResult = await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithParentId(newParentPage.Id)
            .WithSlug("child")
            .WithVersion(childPage.PageDetails.Version)
            .UpdateAsync(client, childPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/new-parent/child");
        updateResult.PageDetails.ParentId.Should().Be(newParentPage.Id);
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldMovePageToRoot_WhenParentIdSetToNull()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create parent page
        var parentPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("parent")
            .WithSingleTextBlock(blockType.Id, "Parent content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create child page
        var childPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithParentId(parentPage.Id)
            .WithSlug("child")
            .WithSingleTextBlock(blockType.Id, "Child content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Move child to root
        var updateResult = await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithParentId(null) // Move to root
            .WithSlug("child")
            .WithVersion(childPage.PageDetails.Version)
            .UpdateAsync(client, childPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/child");
        updateResult.PageDetails.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldReturnConflict_WhenUpdatedPathAlreadyExists()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create parent page
        var parentPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("parent")
            .WithSingleTextBlock(blockType.Id, "Parent content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create existing child page that will conflict
        await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithParentId(parentPage.Id)
            .WithSlug("existing-child")
            .WithSingleTextBlock(blockType.Id, "Existing child content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create another page to move
        var pageToMove = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("page-to-move")
            .WithSingleTextBlock(blockType.Id, "Page to move content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Try to move page to create conflicting path
        var response = await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithParentId(parentPage.Id) // Move under parent
            .WithSlug("existing-child") // Use existing child's slug
            .WithVersion(pageToMove.PageDetails.Version)
            .UpdateExpectingAsync(client, pageToMove.Id, HttpStatusCode.Conflict, TestContext.Current.CancellationToken);

        await TestPageHelper.AssertValidationErrorFieldAsync(response, "slug");
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldRejectUnicodeInSlug_WhenInvalidCharacters()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("english-page")
            .WithSingleTextBlock(blockType.Id, "English content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Update with Unicode characters in slug (should be rejected by validation)
        await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithSlug("café-résumé-naïve") // Unicode characters are not allowed in slugs
            .WithVersion(initialPage.PageDetails.Version)
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldReturnBadRequest_WhenCreatingCyclicHierarchy()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create grandparent page
        var grandparentPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("grandparent")
            .WithSingleTextBlock(blockType.Id, "Grandparent content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create parent page under grandparent
        var parentPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithParentId(grandparentPage.Id)
            .WithSlug("parent")
            .WithSingleTextBlock(blockType.Id, "Parent content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create child page under parent
        var childPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithParentId(parentPage.Id)
            .WithSlug("child")
            .WithSingleTextBlock(blockType.Id, "Child content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Try to make grandparent a child of its own descendant (child)
        // This would create: child -> grandparent -> parent -> child (cycle)
        var response = await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithParentId(childPage.Id) // This would create a cycle
            .WithSlug("grandparent")
            .WithVersion(grandparentPage.PageDetails.Version)
            .UpdateExpectingAsync(client, grandparentPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);

        await TestPageHelper.AssertValidationErrorFieldAsync(response, "parentId");
    }

    [Fact]
    public async Task UpdatePageDetails_ShouldReturnBadRequest_WhenPageIsItsOwnParent()
    {
        // Arrange
        var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("self-parent")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestUpdatePageDetailsRequestBuilder
            .Create()
            .WithParentId(page.Id)
            .WithSlug("self-parent")
            .WithVersion(page.PageDetails.Version)
            .UpdateExpectingAsync(client, page.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);

        // Assert
        await TestPageHelper.AssertValidationErrorFieldAsync(response, "parentId");
        var path = await TestPageHelper.GetPagePathAsync(client, page.Id, TestContext.Current.CancellationToken);
        path.Path.Should().Be("/self-parent");
    }

    public async ValueTask DisposeAsync() => await fixture.TearDownTestAsync();
}
