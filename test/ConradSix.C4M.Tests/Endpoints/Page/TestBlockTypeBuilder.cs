namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Base;

public sealed class TestBlockTypeBuilder
{
    private string _name = "test-block";

    private int _version = 1;

    private string _jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"content": {"type": "string"}}}""";

    public static TestBlockTypeBuilder Create() => new();

    public static TestBlockTypeBuilder CreateTextBlock(string name) =>
        new TestBlockTypeBuilder()
            .WithName(name)
            .WithSimpleTextSchema();

    public static TestBlockTypeBuilder CreateImageBlock(string name) =>
        new TestBlockTypeBuilder()
            .WithName(name)
            .WithImageSchema();

    public static TestBlockTypeBuilder CreateRichTextBlock(string name) =>
        new TestBlockTypeBuilder()
            .WithName(name)
            .WithRichTextSchema();

    public static TestBlockTypeBuilder CreateButtonBlock(string name) =>
        new TestBlockTypeBuilder()
            .WithName(name)
            .WithButtonSchema();

    public static TestBlockTypeBuilder CreateSectionBlock(string name) =>
        new TestBlockTypeBuilder()
            .WithName(name)
            .WithSectionSchema();

    public static TestBlockTypeBuilder CreateHeroBlock(string name) =>
        new TestBlockTypeBuilder()
            .WithName(name)
            .WithHeroSchema();

    public static TestBlockTypeBuilder CreatePageLinkButtonBlock(string name) =>
        new TestBlockTypeBuilder()
            .WithName(name)
            .WithPageLinkButtonSchema();

    public static TestBlockTypeBuilder CreateStrictTextBlock(string name) =>
        new TestBlockTypeBuilder()
            .WithName(name)
            .WithStrictTextSchema();

    public static string GenerateTextBlockJson(string text) =>
        JsonSerializer.Serialize(new { text });

    public static string GenerateImageBlockJson(string url, string alt) =>
        JsonSerializer.Serialize(new { url, alt });

    public static string GenerateRichTextBlockJson(string html, string? plainText = null) =>
        JsonSerializer.Serialize(new { html, plainText = plainText ?? html });

    public static string GenerateButtonBlockJson(string text, string url, string style = "primary") =>
        JsonSerializer.Serialize(new { text, url, style });

    public static string GenerateSectionBlockJson(string title, string subtitle = "") =>
        JsonSerializer.Serialize(new { title, subtitle });

    public static string GenerateHeroBlockJson(string title, Guid imageId) =>
        JsonSerializer.Serialize(new
        {
            title,
            image = new { __typename = "image-ref", id = imageId },
        });

    public static string GeneratePageLinkButtonJson(string text, Guid pageId) =>
        JsonSerializer.Serialize(new
        {
            text,
            pageLink = new { __typename = "page-ref", id = pageId },
        });

    public static string GenerateInvalidTextBlockJson() =>
        JsonSerializer.Serialize(new { text = "X" }); // Too short for strict schema

    public static string GenerateEmptyTextBlockJson() =>
        JsonSerializer.Serialize(new { }); // Missing required text field

    public TestBlockTypeBuilder WithName(string name)
    {
        this._name = name;
        return this;
    }

    public TestBlockTypeBuilder WithVersion(int version)
    {
        this._version = version;
        return this;
    }

    public TestBlockTypeBuilder WithJsonSchema(string schema)
    {
        this._jsonSchema = schema;
        return this;
    }

    public TestBlockTypeBuilder WithSimpleTextSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"text": {"type": "string"}}}""";
        return this;
    }

    public TestBlockTypeBuilder WithImageSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"url": {"type": "string"}, "alt": {"type": "string"}}}""";
        return this;
    }

    public TestBlockTypeBuilder WithRichTextSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"html": {"type": "string"}, "plainText": {"type": "string"}}}""";
        return this;
    }

    public TestBlockTypeBuilder WithButtonSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"text": {"type": "string"}, "url": {"type": "string"}, "style": {"type": "string"}}}""";
        return this;
    }

    public TestBlockTypeBuilder WithSectionSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"title": {"type": "string"}, "subtitle": {"type": "string"}}}""";
        return this;
    }

    public TestBlockTypeBuilder WithHeroSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"title": {"type": "string"}, "image": {"type": "object", "properties": {"__typename": {"type": "string"}, "id": {"type": "string"}}}}}""";
        return this;
    }

    public TestBlockTypeBuilder WithPageLinkButtonSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"text": {"type": "string"}, "pageLink": {"type": "object", "properties": {"__typename": {"type": "string"}, "id": {"type": "string"}}}}}""";
        return this;
    }

    public TestBlockTypeBuilder WithStrictTextSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"text": {"type": "string", "minLength": 5, "maxLength": 20}}, "required": ["text"]}""";
        return this;
    }

    public string GenerateJsonFor(string blockTypeName, object data) =>
        blockTypeName switch
        {
            _ when this._jsonSchema.Contains("\"text\"", StringComparison.Ordinal) => JsonSerializer.Serialize(new { text = data }),
            _ when this._jsonSchema.Contains("\"url\"", StringComparison.Ordinal) && this._jsonSchema.Contains("\"alt\"", StringComparison.Ordinal) =>
                data switch
                {
                    (string url, string alt) => JsonSerializer.Serialize(new { url, alt }),
                    _ => throw new ArgumentException($"Image block requires (url, alt) tuple, got {data.GetType()}"),
                },
            _ when this._jsonSchema.Contains("\"html\"", StringComparison.Ordinal) =>
                data switch
                {
                    (string html, string plainText) => JsonSerializer.Serialize(new { html, plainText }),
                    string html => JsonSerializer.Serialize(new { html, plainText = html }),
                    _ => throw new ArgumentException($"Rich text block requires string or (html, plainText) tuple, got {data.GetType()}"),
                },
            _ when this._jsonSchema.Contains("\"style\"", StringComparison.Ordinal) =>
                data switch
                {
                    (string text, string url, string style) => JsonSerializer.Serialize(new { text, url, style }),
                    (string text, string url) => JsonSerializer.Serialize(new { text, url, style = "primary" }),
                    _ => throw new ArgumentException($"Button block requires (text, url) or (text, url, style) tuple, got {data.GetType()}"),
                },
            _ when this._jsonSchema.Contains("\"title\"", StringComparison.Ordinal) && this._jsonSchema.Contains("\"subtitle\"", StringComparison.Ordinal) =>
                data switch
                {
                    (string title, string subtitle) => JsonSerializer.Serialize(new { title, subtitle }),
                    string title => JsonSerializer.Serialize(new { title, subtitle = string.Empty }),
                    _ => throw new ArgumentException($"Section block requires string or (title, subtitle) tuple, got {data.GetType()}"),
                },
            _ when this._jsonSchema.Contains("\"image\"", StringComparison.Ordinal) && this._jsonSchema.Contains("\"__typename\"", StringComparison.Ordinal) =>
                data switch
                {
                    (string title, Guid imageId) => JsonSerializer.Serialize(new { title, image = new { __typename = "image-ref", id = imageId } }),
                    _ => throw new ArgumentException($"Hero block requires (title, imageId) tuple, got {data.GetType()}"),
                },
            _ => JsonSerializer.Serialize(data),
        };

    public async Task<CreateCmsTypeDescriptorResponse> CreateAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        var request = new CreateCmsTypeDescriptorRequest(
            Name: this._name,
            Version: this._version,
            JsonSchema: this._jsonSchema);

        var response = await client.PostAsJsonAsync("/block-types", request, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var blockType = await response.Content.ReadFromJsonAsync<CreateCmsTypeDescriptorResponse>(cancellationToken);
        blockType.Should().NotBeNull();

        return blockType;
    }
}
