namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

[Collection(C4MAppCollection.Name)]
public sealed class CreatePageTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Theory]
    [InlineData("api")]
    [InlineData("reserved")]
    public async Task CreatePage_ShouldRejectReservedRootPaths(string slug)
    {
        var response = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug(slug)
            .CreateExpectingAsync(
                fixture.CreateClient(),
                HttpStatusCode.BadRequest,
                TestContext.Current.CancellationToken);

        await TestPageHelper.AssertValidationErrorFieldAsync(response, "slug");
    }

    [Fact]
    public async Task CreatePage_ShouldPersistVersionSeo_WhenProvided()
    {
        var seo = new PageSeoRequest(
            Title: "Homes in Example Resort",
            Description: "Explore vacation homes for sale in Example Resort near Orlando.",
            SocialImageId: null,
            Index: false,
            Follow: false);

        var page = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("seo-page")
            .WithSeo(seo)
            .CreateAsync(fixture.CreateClient(), TestContext.Current.CancellationToken);

        page.PageDetails.Seo.Title.Should().Be(seo.Title);
        page.PageDetails.Seo.Description.Should().Be(seo.Description);
        page.PageDetails.Seo.SocialImage.Should().BeNull();
        page.PageDetails.Seo.Index.Should().BeFalse();
        page.PageDetails.Seo.Follow.Should().BeFalse();
    }

    [Fact]
    public Task CreatePage_ShouldRejectPublication_WhenSeoIsIncomplete() =>
        TestCreatePageRequestBuilder
            .Create()
            .WithSlug("missing-seo")
            .WithSeo(new PageSeoRequest(null, null, null))
            .CreateExpectingAsync(
                fixture.CreateClient(),
                HttpStatusCode.BadRequest,
                TestContext.Current.CancellationToken);

    [Fact]
    public async Task CreatePage_ShouldLeaveInitialVersionAsDraft_WhenPublishIsFalse()
    {
        var client = fixture.CreateClient();
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("draft-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync(
            "/pages",
            new CreatePageRequest(
                ParentId: null,
                Slug: "ai-draft",
                Blocks: [new CreatePageBlock(blockType.Id, TestBlockTypeBuilder.GenerateTextBlockJson("Draft content"))],
                Publish: false),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPage = await response.Content.ReadFromJsonAsync<CreatePageResponse>(TestContext.Current.CancellationToken);
        createdPage.Should().NotBeNull();

        var currentResponse = await client.GetAsync($"/pages/{createdPage.Id}", TestContext.Current.CancellationToken);
        currentResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var latestResponse = await client.GetAsync($"/pages/{createdPage.Id}?latest=true", TestContext.Current.CancellationToken);
        latestResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pathResponse = await client.GetAsync("/pages/by-path?path=%2Fai-draft", TestContext.Current.CancellationToken);
        pathResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var pages = await TestListPagesRequestBuilder
            .ForRealtor(Guid.NewGuid())
            .ExecuteAsync(client, TestContext.Current.CancellationToken);
        pages.Pages.Should().ContainSingle();
        pages.Pages[0].CurrentPageVersionId.Should().NotBeNull();
        pages.Pages[0].VersionCount.Should().Be(1);
    }

    [Fact]
    public async Task CreatePage_ShouldCreatePageWithOneBlock_WhenValidRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // First create a block type
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Create page using TestPageBuilder
        var pageResult = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Hello World")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert - Use the comprehensive assertion method
        pageResult.Assert("/test-page");
        pageResult.AssertBlockType(0, "simple-text", "1");
    }

    [Fact]
    public async Task CreatePage_ShouldReturnBadRequest_WhenSlugIsEmpty()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert
        await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug(string.Empty)
            .WithSingleTextBlock(blockType.Id, "Hello World")
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreatePage_ShouldReturnBadRequest_WhenSlugContainsInvalidCharacters()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert
        await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("Invalid Slug With Spaces!")
            .WithSingleTextBlock(blockType.Id, "Hello World")
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreatePage_ShouldReturnBadRequest_WhenBlockTypeIdDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var nonExistentBlockTypeId = Guid.NewGuid();

        // Act & Assert
        await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithNonExistentBlockType(nonExistentBlockTypeId)
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreatePage_ShouldCreatePageWithMultipleBlocks_WhenValidRequest()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create multiple block types
        var textBlockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var imageBlockType = await TestBlockTypeBuilder
            .CreateImageBlock("image-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var pageResult = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("multi-block-page")
            .WithMultipleBlocks(
                new(
                    BlockTypeId: textBlockType.Id,
                    BlockData: TestBlockTypeBuilder.GenerateTextBlockJson("First block")),
                new(
                    BlockTypeId: imageBlockType.Id,
                    BlockData: TestBlockTypeBuilder.GenerateImageBlockJson("https://example.com/image.jpg", "Test image")),
                new(
                    BlockTypeId: textBlockType.Id,
                    BlockData: TestBlockTypeBuilder.GenerateTextBlockJson("Third block")))
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert
        pageResult.Assert("/multi-block-page");
        pageResult.AssertBlockType(0, "text-block", "1");
        pageResult.AssertBlockType(1, "image-block", "1");
        pageResult.AssertBlockType(2, "text-block", "1");
    }

    [Fact]
    public async Task CreatePage_ShouldCreateChildPage_WhenParentIdProvided()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .Create()
            .WithSimpleTextSchema()
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // First create a parent page
        var parentPageResult = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("parent-page")
            .WithSingleTextBlock(blockType.Id, "Parent content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Create child page
        var childPageResult = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithParentId(parentPageResult.Id)
            .WithSlug("child-page")
            .WithSingleTextBlock(blockType.Id, "Child content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert
        childPageResult.Assert("/parent-page/child-page");
    }

    [Fact]
    public async Task CreatePage_ShouldReturnConflict_WhenSlugAlreadyExistsForSameParent()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create first page successfully
        await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("duplicate-slug")
            .WithSingleTextBlock(blockType.Id, "First page")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Try to create second page with same slug and parent
        var response = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithParentId(null) // Same parent (null)
            .WithSlug("duplicate-slug") // Same slug
            .WithSingleTextBlock(blockType.Id, "Second page")
            .CreateExpectingAsync(client, HttpStatusCode.Conflict, TestContext.Current.CancellationToken);

        await TestPageHelper.AssertValidationErrorFieldAsync(response, "slug");
    }

    [Fact]
    public async Task CreatePage_ShouldCreatePageWithHeroBlock_WhenImageReferenceProvided()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create a hero block type
        var heroBlockType = await TestBlockTypeBuilder
            .CreateHeroBlock("hero-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create an image first
        var image = fixture.Assets.Add(600, 400, "Hero image");

        // Act - Create page with hero block that references the image
        var pageResult = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("hero-page")
            .WithSingleBlock(heroBlockType.Id, TestBlockTypeBuilder.GenerateHeroBlockJson("Welcome to our site", image.Id))
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert - The page should be created successfully
        pageResult.Assert("/hero-page");
        pageResult.AssertBlockType(0, "hero-block", "1");

        // Verify the image reference is populated in the response
        pageResult.PageDetails.Blocks.Should().HaveCount(1);
        var block = pageResult.PageDetails.Blocks[0];
        block.Images.Should().HaveCount(1);

        var imageRef = block.Images[0];
        imageRef.Id.Should().Be(image.Id);
        imageRef.AltText.Should().Be("Hero image");
        imageRef.ImageUrl.Should().Be(
            image.Url);
        imageRef.Width.Should().Be(600); // From the test image
        imageRef.Height.Should().Be(400); // From the test image
    }

    [Theory]
    [InlineData("hero")]
    [InlineData("search-hero")]
    public async Task CreatePage_ShouldCreatePageWithPageLinkButton_WhenPageReferenceProvided(string heroTypeName)
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create a page link button block type
        var pageLinkButtonBlockType = await TestBlockTypeBuilder
            .CreatePageLinkButtonBlock("page-link-button")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create a target page first
        var heroImage = fixture.Assets.Add(600, 400, "Target page hero");
        var heroBlockType = await TestBlockTypeBuilder
            .CreateHeroBlock(heroTypeName)
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var targetPageResult = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("target-page")
            .WithSingleBlock(
                heroBlockType.Id,
                TestBlockTypeBuilder.GenerateHeroBlockJson("Target page", heroImage.Id))
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Create page with page link button that references the target page
        var pageResult = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("source-page")
            .WithSingleBlock(
                pageLinkButtonBlockType.Id,
                TestBlockTypeBuilder.GeneratePageLinkButtonJson("Click to visit target", targetPageResult.Id))
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Assert - The page should be created successfully
        pageResult.Assert("/source-page");
        pageResult.AssertBlockType(0, "page-link-button", "1");

        // Verify the page reference is populated in the response
        pageResult.PageDetails.Blocks.Should().HaveCount(1);
        var block = pageResult.PageDetails.Blocks[0];
        block.Pages.Should().HaveCount(1);

        var pageRef = block.Pages[0];
        pageRef.Id.Should().Be(targetPageResult.Id);
        pageRef.Slug.Should().Be("target-page");
        pageRef.Path.Should().Be("/target-page");
        pageRef.Title.Should().NotBeNullOrEmpty();
        pageRef.HeroImage.Should().NotBeNull();
        pageRef.HeroImage.Id.Should().Be(heroImage.Id);
        pageRef.HeroImage.AltText.Should().Be("Target page hero");
    }

    [Fact]
    public async Task CreatePage_ShouldReturnBadRequestWithBlockErrors_WhenBlockDataIsInvalid()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create a strict text block type that requires text between 5-20 characters
        var strictTextBlockType = await TestBlockTypeBuilder
            .CreateStrictTextBlock("strict-text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Try to create page with invalid block data (text too short)
        var response = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("invalid-block-page")
            .WithSingleBlock(strictTextBlockType.Id, TestBlockTypeBuilder.GenerateInvalidTextBlockJson())
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);

        // Assert error response format
        await AssertBlockValidationErrorsAsync(response);
    }

    [Fact]
    public async Task CreatePage_ShouldRejectABlock_WhoseVersionIsNotItsTypes()
    {
        var client = fixture.CreateClient();
        var blockType = await TestBlockTypeBuilder
            .Create()
            .WithName("versioned-text")
            .WithJsonSchema(/*lang=json,strict*/ """{"type": "object", "properties": {"__typename": {"const": "versioned-text"}, "__version": {"const": 1}, "text": {"type": "string"}}, "required": ["__typename", "__version", "text"]}""")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var response = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("wrong-version")
            .WithSingleBlock(blockType.Id, /*lang=json,strict*/ """{"__typename": "versioned-text", "__version": 2, "text": "Hello"}""")
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);

        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("errors");
        errors.GetProperty("blocks[0].versioned-text.#/__version").EnumerateArray().Select(error => error.GetString())
            .Should().Equal("ConstMismatch");
    }

    [Fact]
    public async Task CreatePage_ShouldReturnBadRequestWithBlockErrors_WhenRequiredFieldMissing()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create a strict text block type that requires the text field
        var strictTextBlockType = await TestBlockTypeBuilder
            .CreateStrictTextBlock("strict-text-block")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act & Assert - Try to create page with missing required field
        var response = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("missing-field-page")
            .WithSingleBlock(strictTextBlockType.Id, TestBlockTypeBuilder.GenerateEmptyTextBlockJson())
            .CreateExpectingAsync(client, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);

        // Assert error response format
        await AssertBlockValidationErrorsAsync(response);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();

    private static async Task AssertBlockValidationErrorsAsync(HttpResponseMessage response)
    {
        // Verify we have an error response body
        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().NotBeNullOrEmpty();

        // Parse the error response
        var errorResponse = JsonSerializer.Deserialize<JsonElement>(errorContent);

        // Verify error structure
        errorResponse.TryGetProperty("errors", out var errorsProperty).Should().BeTrue();
        var errors = errorsProperty;

        // Look for block validation errors in the format: blocks[0].(blockType).#/fieldName
        var hasBlockErrors = false;
        foreach (var errorProperty in errors.EnumerateObject())
        {
            var errorKey = errorProperty.Name;
            if (errorKey.StartsWith("blocks[0].", StringComparison.Ordinal) && errorKey.Contains(".#/", StringComparison.Ordinal))
            {
                hasBlockErrors = true;

                // Verify error key format: blocks[0].(blockType).#/fieldName
                errorKey.Should().MatchRegex(@"^blocks\[0\]\..*\.#\/.*$");

                // Verify error values are ValidationErrorKind enum values
                var errorValues = errorProperty.Value.EnumerateArray().Select(v => v.GetString()).ToList();
                errorValues.Should().NotBeEmpty();
                errorValues.Should().AllSatisfy(errorCode =>
                {
                    errorCode.Should().NotBeNullOrEmpty();

                    // Verify these are valid ValidationErrorKind values
                    errorCode.Should().BeOneOf(
                        "StringTooShort",
                        "PropertyRequired",
                        "StringExpected",
                        "NumberExpected",
                        "BooleanExpected",
                        "PatternMismatch",
                        "StringTooLong");
                });
            }
        }

        hasBlockErrors.Should().BeTrue("Should have block validation errors in the correct format");
    }
}
