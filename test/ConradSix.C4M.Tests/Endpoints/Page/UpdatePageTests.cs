namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

[Collection(C4MAppCollection.Name)]
public sealed class UpdatePageTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task UpdatePage_ShouldReplaceSingleBlock_WhenValidRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create block types
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("original-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page with one block
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Original content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update page using TestUpdatePageRequestBuilder
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Updated content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/test-page");
        updateResult.AssertBlock(
            0,
            (Name: "original-text", Version: "1"),
            TestBlockTypeBuilder.GenerateTextBlockJson("Updated content"));
        updateResult.AssertBlockNotPresent(initialPage.PageDetails.Blocks[0].Id);
    }

    [Fact]
    public async Task UpdatePage_ShouldAdvanceLatestWithoutChangingDetailsToken_WhenSuccessfulUpdate()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create block type
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("version-test-page")
            .WithSingleTextBlock(blockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var initialVersion = initialPage.PageDetails.LatestPageVersionId!.Value;

        // Act - Update page with only block changes (no page property changes)
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("version-test-page") // Same slug
            .WithLatestVersion(initialVersion)
            .WithSingleNewTextBlock(blockType.Id, "Updated content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert - Version should be incremented (may be more than 1 due to EF Core behavior)
        updateResult.PageDetails.LatestPageVersionId.Should().NotBe(initialVersion);
        updateResult.PageDetails.Version.Should().Be(initialPage.PageDetails.Version);

        // Act - Try another update with the new version
        var secondUpdateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("version-test-page")
            .WithLatestVersion(updateResult.PageDetails.LatestPageVersionId!.Value) // Use the new version
            .WithSingleNewTextBlock(blockType.Id, "Second update")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert - Version should be incremented again
        secondUpdateResult.PageDetails.LatestPageVersionId.Should().NotBe(updateResult.PageDetails.LatestPageVersionId!.Value);
        secondUpdateResult.PageDetails.Version.Should().Be(updateResult.PageDetails.Version);
    }

    [Fact]
    public async Task UpdatePage_ShouldReturn409Conflict_WhenVersionMismatch()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create block type
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("conflict-test-page")
            .WithSingleTextBlock(blockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var initialVersion = initialPage.PageDetails.LatestPageVersionId!.Value;

        // First update - should succeed
        _ = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("conflict-test-page")
            .WithLatestVersion(initialVersion)
            .WithSingleNewTextBlock(blockType.Id, "First update")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Act - Try to update with the old version (should fail with 409)
        var response = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("conflict-test-page")
            .WithLatestVersion(initialVersion) // Using old version
            .WithSingleNewTextBlock(blockType.Id, "Conflicting update")
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.Conflict, TestContext.Current.CancellationToken);

        // Assert - Response should contain current page data with updated version
        var conflictDataJson = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        conflictDataJson.Should().NotBeNullOrEmpty();
        using var conflictData = JsonDocument.Parse(conflictDataJson);
        conflictData.RootElement.GetProperty("latestPageVersionId").GetGuid().Should().NotBe(initialVersion);
    }

    [Fact]
    public async Task UpdatePage_ShouldAddMultipleDifferentBlockTypes_WhenValidRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create different block types using standardized factory methods
        var textBlockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var imageBlockType = await TestBlockTypeBuilder
            .CreateImageBlock("hero-image")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var buttonBlockType = await TestBlockTypeBuilder
            .CreateButtonBlock("call-to-action")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("multi-type-page")
            .WithSingleTextBlock(textBlockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Add multiple different block types
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("multi-type-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithNewBlocks([
                new(textBlockType.Id, 0, TestBlockTypeBuilder.GenerateTextBlockJson("Main heading")),
                new(imageBlockType.Id, 1, TestBlockTypeBuilder.GenerateImageBlockJson("https://example.com/hero.jpg", "Hero image")),
                new(buttonBlockType.Id, 2, TestBlockTypeBuilder.GenerateButtonBlockJson("Learn More", "https://example.com/learn", "primary")),
            ])
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/multi-type-page");

        // Verify all three new blocks were added correctly
        updateResult.AssertBlock(0, ("text-content", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Main heading"));
        updateResult.AssertBlock(1, ("hero-image", "1"), TestBlockTypeBuilder.GenerateImageBlockJson("https://example.com/hero.jpg", "Hero image"));
        updateResult.AssertBlock(2, ("call-to-action", "1"), TestBlockTypeBuilder.GenerateButtonBlockJson("Learn More", "https://example.com/learn", "primary"));

        // Verify the original block was replaced
        updateResult.AssertBlockNotPresent(initialPage.PageDetails.Blocks[0].Id);
    }

    [Fact]
    public async Task UpdatePage_ShouldReturnNotFound_WhenPageIdDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var nonExistentPageId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert
        await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(Guid.NewGuid()) // Non-existent page, so any version
            .WithSingleNewTextBlock(blockType.Id, "Some content")
            .UpdateExpectingAsync(client, nonExistentPageId, HttpStatusCode.NotFound, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePage_ShouldReturnBadRequest_WhenBlockTypeIdDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var nonExistentBlockTypeId = Guid.NewGuid();

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
        await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewBlock(nonExistentBlockTypeId, TestBlockTypeBuilder.GenerateTextBlockJson("Updated content"))
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePage_ShouldReturnBadRequest_WhenExistingBlockIdDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var nonExistentBlockId = Guid.NewGuid();

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
        await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleExistingBlock(nonExistentBlockId, 0)
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePage_ShouldReturnBadRequest_WhenDuplicateSortOrdersProvided()
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

        // Act & Assert - Create blocks with duplicate sort orders
        await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithNewBlocks([
                new(blockType.Id, 0, TestBlockTypeBuilder.GenerateTextBlockJson("First block")),
                new(blockType.Id, 0, TestBlockTypeBuilder.GenerateTextBlockJson("Second block")), // Duplicate sort order
            ])
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePage_ShouldReturnBadRequest_WhenNegativeSortOrderProvided()
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
        await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithNewBlocks([
                new(blockType.Id, -1, TestBlockTypeBuilder.GenerateTextBlockJson("Invalid sort order block")),
            ])
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePage_ShouldReturnConflict_WhenVersionMismatch()
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
            .WithSlug("version-test")
            .WithSingleTextBlock(blockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Try to update with wrong version
        await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("version-test")
            .WithLatestVersion(Guid.NewGuid()) // Wrong version
            .WithSingleNewTextBlock(blockType.Id, "Updated content")
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.Conflict, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePage_ShouldPreserveExistingBlocks_WhenReorderingOnly()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var textBlockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var imageBlockType = await TestBlockTypeBuilder
            .CreateImageBlock("image-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page with multiple blocks
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("reorder-test")
            .WithMultipleBlocks(
                new(textBlockType.Id, TestBlockTypeBuilder.GenerateTextBlockJson("First block")),
                new(imageBlockType.Id, TestBlockTypeBuilder.GenerateImageBlockJson("https://example.com/image.jpg", "Test image")))
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var firstBlockId = initialPage.PageDetails.Blocks[0].Id;
        var secondBlockId = initialPage.PageDetails.Blocks[1].Id;

        // Act - Reorder existing blocks (reverse order)
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("reorder-test")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithExistingBlocks([
                new(secondBlockId, 0), // Image block first
                new(firstBlockId, 1), // Text block second
            ])
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/reorder-test");
        updateResult.PageDetails.Blocks.Should().HaveCount(2);

        // Verify blocks are reordered
        updateResult.PageDetails.Blocks[0].Id.Should().Be(secondBlockId);
        updateResult.PageDetails.Blocks[1].Id.Should().Be(firstBlockId);

        // Verify block types are preserved (content may have different JSON formatting)
        updateResult.PageDetails.Blocks[0].BlockName.Should().Be("image-block");
        updateResult.PageDetails.Blocks[0].BlockVersion.Should().Be("1");
        updateResult.PageDetails.Blocks[1].BlockName.Should().Be("text-block");
        updateResult.PageDetails.Blocks[1].BlockVersion.Should().Be("1");
    }

    [Fact]
    public async Task UpdatePage_ShouldMixNewAndExistingBlocks_WhenValidSortOrders()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var textBlockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var buttonBlockType = await TestBlockTypeBuilder
            .CreateButtonBlock("button-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page with one block
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("mixed-blocks")
            .WithSingleTextBlock(textBlockType.Id, "Existing content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var existingBlockId = initialPage.PageDetails.Blocks[0].Id;

        // Act - Mix new and existing blocks
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("mixed-blocks")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithNewBlocks([
                new(textBlockType.Id, 0, TestBlockTypeBuilder.GenerateTextBlockJson("New first block")),
                new(buttonBlockType.Id, 2, TestBlockTypeBuilder.GenerateButtonBlockJson("Click me", "https://example.com", "primary")),
            ])
            .WithExistingBlocks([
                new(existingBlockId, 1), // Existing block in middle
            ])
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/mixed-blocks");
        updateResult.PageDetails.Blocks.Should().HaveCount(3);

        // Verify correct ordering: new text, existing text, new button
        updateResult.AssertBlock(0, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("New first block"));
        updateResult.AssertBlock(1, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Existing content"));
        updateResult.AssertBlock(2, ("button-block", "1"), TestBlockTypeBuilder.GenerateButtonBlockJson("Click me", "https://example.com", "primary"));
    }

    [Fact]
    public async Task UpdatePage_ShouldReturnBadRequest_WhenInvalidJsonBlockData()
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

        // Act & Assert - Try to update with invalid JSON
        // Note: This causes an internal server error because JSON parsing fails during validation
        await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewBlock(blockType.Id, "{ invalid json without closing brace")
            .UpdateExpectingAsync(client, initialPage.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdatePage_ShouldSucceedWithEmptyBlocks_WhenRemovingAllContent()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page with content
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("empty-page")
            .WithSingleTextBlock(blockType.Id, "Content to be removed")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Update with no blocks (empty lists)
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("empty-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithNewBlocks([])
            .WithExistingBlocks([])
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/empty-page");
        updateResult.PageDetails.Blocks.Should().BeEmpty();
        updateResult.AssertBlockNotPresent(initialPage.PageDetails.Blocks[0].Id);
    }

    [Fact]
    public async Task UpdatePage_ShouldHandleNonSequentialSortOrders_WhenValid()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var textBlockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("non-sequential")
            .WithSingleTextBlock(textBlockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Use non-sequential sort orders: 0, 5, 10, 100
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("non-sequential")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithNewBlocks([
                new(textBlockType.Id, 0, TestBlockTypeBuilder.GenerateTextBlockJson("First block")),
                new(textBlockType.Id, 5, TestBlockTypeBuilder.GenerateTextBlockJson("Second block")),
                new(textBlockType.Id, 10, TestBlockTypeBuilder.GenerateTextBlockJson("Third block")),
                new(textBlockType.Id, 100, TestBlockTypeBuilder.GenerateTextBlockJson("Fourth block")),
            ])
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/non-sequential");
        updateResult.PageDetails.Blocks.Should().HaveCount(4);

        // Verify correct ordering despite non-sequential sort orders
        updateResult.AssertBlock(0, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("First block"));
        updateResult.AssertBlock(1, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Second block"));
        updateResult.AssertBlock(2, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Third block"));
        updateResult.AssertBlock(3, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Fourth block"));
    }

    [Fact]
    public async Task UpdatePage_ShouldRemoveUnlistedBlocks_WhenNotInRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var textBlockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var imageBlockType = await TestBlockTypeBuilder
            .CreateImageBlock("image-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page with multiple blocks
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("remove-test")
            .WithMultipleBlocks(
                new(textBlockType.Id, TestBlockTypeBuilder.GenerateTextBlockJson("Keep this block")),
                new(imageBlockType.Id, TestBlockTypeBuilder.GenerateImageBlockJson("https://example.com/remove.jpg", "Remove this")),
                new(textBlockType.Id, TestBlockTypeBuilder.GenerateTextBlockJson("Remove this too")))
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var keepBlockId = initialPage.PageDetails.Blocks[0].Id;
        var removeBlockId1 = initialPage.PageDetails.Blocks[1].Id;
        var removeBlockId2 = initialPage.PageDetails.Blocks[2].Id;

        // Act - Only include first block in ExistingBlocks, others should be removed
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("remove-test")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithNewBlocks([
                new(textBlockType.Id, 1, TestBlockTypeBuilder.GenerateTextBlockJson("New block")),
            ])
            .WithExistingBlocks([
                new(keepBlockId, 0), // Only keep the first block
            ])
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/remove-test");
        updateResult.PageDetails.Blocks.Should().HaveCount(2);

        // Verify kept block is preserved
        updateResult.PageDetails.Blocks[0].Id.Should().Be(keepBlockId);
        updateResult.AssertBlock(0, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Keep this block"));

        // Verify new block is added
        updateResult.AssertBlock(1, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("New block"));

        // Verify removed blocks are not present
        updateResult.AssertBlockNotPresent(removeBlockId1);
        updateResult.AssertBlockNotPresent(removeBlockId2);
    }

    [Fact]
    public async Task UpdatePage_ShouldSucceed_WhenNoChangesRequested()
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
            .WithSlug("idempotent-test")
            .WithSingleTextBlock(blockType.Id, "Unchanged content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var originalBlockId = initialPage.PageDetails.Blocks[0].Id;
        var originalUpdatedAt = initialPage.PageDetails.UpdatedAt;

        // Act - Update with identical data (idempotent update)
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("idempotent-test") // Same slug
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithExistingBlocks([
                new(originalBlockId, 0), // Same block, same order
            ])
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/idempotent-test");
        updateResult.PageDetails.Blocks.Should().HaveCount(1);
        updateResult.PageDetails.Blocks[0].Id.Should().Be(originalBlockId);

        // Even though no content changed, UpdatedAt should still be updated due to versioning
        updateResult.PageDetails.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task UpdatePage_ShouldUpdateTimestamps_WhenContentChanges()
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
            .WithSlug("timestamp-test")
            .WithSingleTextBlock(blockType.Id, "Original content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var originalCreatedAt = initialPage.PageDetails.CreatedAt;
        var originalUpdatedAt = initialPage.PageDetails.UpdatedAt;

        // Wait a small amount to ensure timestamp differences
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act - Update with new content
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("timestamp-test")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Updated content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/timestamp-test");

        // CreatedAt should remain unchanged
        updateResult.PageDetails.CreatedAt.Should().Be(originalCreatedAt);

        // UpdatedAt should be updated
        updateResult.PageDetails.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        updateResult.PageDetails.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task UpdatePage_ShouldHandleLargeNumberOfBlocks_WhenValid()
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
            .WithSlug("large-page")
            .WithSingleTextBlock(blockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Create a large number of blocks (50 blocks)
        var newBlocks = new List<UpdatePageNewBlock>();
        for (var i = 0; i < 50; i++)
        {
            newBlocks.Add(new(blockType.Id, i, TestBlockTypeBuilder.GenerateTextBlockJson($"Block {i + 1} content")));
        }

        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("large-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithNewBlocks(newBlocks)
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        updateResult.Assert("/large-page");
        updateResult.PageDetails.Blocks.Should().HaveCount(50);

        // Verify first and last blocks to ensure ordering is correct
        updateResult.AssertBlock(0, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Block 1 content"));
        updateResult.AssertBlock(49, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Block 50 content"));

        // Verify original block was replaced
        updateResult.AssertBlockNotPresent(initialPage.PageDetails.Blocks[0].Id);
    }

    [Fact]
    public async Task UpdatePage_ShouldMaintainPageHistory_WhenMultipleUpdates()
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
            .WithSlug("versioned-page")
            .WithSingleTextBlock(blockType.Id, "Version 1 content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var originalCreatedAt = initialPage.PageDetails.CreatedAt;

        // Act - Perform multiple sequential updates
        var firstUpdate = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("versioned-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version 2 content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken); // Ensure timestamp difference

        var secondUpdate = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("versioned-page-updated")
            .WithLatestVersion(firstUpdate.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version 3 content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken); // Ensure timestamp difference

        var thirdUpdate = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("versioned-page")
            .WithLatestVersion(secondUpdate.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Final version content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert
        thirdUpdate.Assert("/versioned-page");

        // Verify final state
        thirdUpdate.PageDetails.Slug.Should().Be("versioned-page");
        thirdUpdate.AssertBlock(0, ("text-block", "1"), TestBlockTypeBuilder.GenerateTextBlockJson("Final version content"));

        // Verify timestamps show progression
        thirdUpdate.PageDetails.CreatedAt.Should().Be(originalCreatedAt); // CreatedAt never changes
        thirdUpdate.PageDetails.UpdatedAt.Should().BeAfter(secondUpdate.PageDetails.UpdatedAt);
        secondUpdate.PageDetails.UpdatedAt.Should().BeAfter(firstUpdate.PageDetails.UpdatedAt);
        firstUpdate.PageDetails.UpdatedAt.Should().BeAfter(originalCreatedAt);

        // Verify all previous blocks have been replaced
        thirdUpdate.AssertBlockNotPresent(initialPage.PageDetails.Blocks[0].Id);
        thirdUpdate.AssertBlockNotPresent(firstUpdate.PageDetails.Blocks[0].Id);
        thirdUpdate.AssertBlockNotPresent(secondUpdate.PageDetails.Blocks[0].Id);
    }

    [Fact]
    public async Task UpdatePage_ShouldUpdatePageWithHeroBlock_WhenImageReferenceProvided()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create initial block type and page
        var textBlockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("hero-update-page")
            .WithSingleTextBlock(textBlockType.Id, "Original content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create a hero block type
        var heroBlockType = await TestBlockTypeBuilder
            .CreateHeroBlock("hero-section")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create an image to reference
        var image = fixture.Assets.Add(600, 400, "Updated hero image");

        // Act - Update page to use a hero block that references the image
        var updateResult = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("hero-update-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewBlock(heroBlockType.Id, TestBlockTypeBuilder.GenerateHeroBlockJson("Welcome to our updated site", image.Id))
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Assert - The page should be updated successfully
        updateResult.Assert("/hero-update-page");
        updateResult.AssertBlock(0, ("hero-section", "1"), TestBlockTypeBuilder.GenerateHeroBlockJson("Welcome to our updated site", image.Id));

        // Verify the image reference is populated in the response
        updateResult.PageDetails.Blocks.Should().HaveCount(1);
        var block = updateResult.PageDetails.Blocks[0];
        block.Images.Should().HaveCount(1);

        var imageRef = block.Images[0];
        imageRef.Id.Should().Be(image.Id);
        imageRef.AltText.Should().Be("Updated hero image");
        imageRef.ImageUrl.Should().Be(
            image.Url);
        imageRef.Width.Should().Be(600); // From the test image
        imageRef.Height.Should().Be(400); // From the test image

        // Verify the original block was replaced
        updateResult.AssertBlockNotPresent(initialPage.PageDetails.Blocks[0].Id);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
