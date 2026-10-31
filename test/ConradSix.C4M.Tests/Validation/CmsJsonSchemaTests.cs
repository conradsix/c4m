namespace ConradSix.C4M.Tests.Validation;

using AwesomeAssertions;

using ConradSix.C4M.Validation;

public sealed class CmsJsonSchemaTests
{
    private const string Hero = /*lang=json,strict*/ """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "__typename": { "type": "string", "const": "hero" },
            "__version": { "type": "number", "const": 2 },
            "title": { "type": "string", "minLength": 1, "maxLength": 10 },
            "links": {
              "type": "array",
              "maxItems": 2,
              "items": {
                "type": "object",
                "properties": { "label": { "type": "string" }, "url": { "type": "string", "format": "uri" } },
                "required": ["label", "url"],
                "additionalProperties": false
              }
            },
            "a/b": { "type": ["integer", "null"] }
          },
          "required": ["__typename", "__version", "title"],
          "additionalProperties": false
        }
        """;

    [Fact]
    public void Validate_ShouldNameEachFailureByItsJsonPointerAndKind()
    {
        var schema = Compile(Hero);

        var errors = schema.Validate(/*lang=json,strict*/ """
            {
              "__typename": "hero",
              "__version": 3,
              "links": [{ "label": 1, "url": "/relative" }, { "url": "https://example.com", "extra": true }],
              "a/b": 1.5,
              "subtitle": "x"
            }
            """);

        errors.Should().BeEquivalentTo([
            new CmsJsonSchemaError("#/title", "PropertyRequired"),
            new CmsJsonSchemaError("#/__version", "ConstMismatch"),
            new CmsJsonSchemaError("#/links/0/label", "StringExpected"),
            new CmsJsonSchemaError("#/links/0/url", "UriExpected"),
            new CmsJsonSchemaError("#/links/1/label", "PropertyRequired"),
            new CmsJsonSchemaError("#/links/1/extra", "NoAdditionalPropertiesAllowed"),
            new CmsJsonSchemaError("#/a~1b", "NoTypeValidates"),
            new CmsJsonSchemaError("#/subtitle", "NoAdditionalPropertiesAllowed"),
        ]);
    }

    [Fact]
    public void Validate_ShouldAcceptDataThatMatches() =>
        Compile(Hero).Validate(/*lang=json,strict*/ """{"__typename": "hero", "__version": 2, "title": "Hi", "a/b": null}""")
            .Should().BeEmpty();

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"if": {"type": "string"}, "then": {"minLength": 1}}""", "if")]
    [InlineData(/*lang=json,strict*/ """{"properties": {"a": {"unevaluatedProperties": false}}}""", "unevaluatedProperties")]
    [InlineData(/*lang=json,strict*/ """{"type": "array", "uniqueItems": true}""", "uniqueItems")]
    [InlineData(/*lang=json,strict*/ """{"$ref": "https://example.com/schema"}""", "inside the schema")]
    [InlineData(/*lang=json,strict*/ """{"$ref": "#name"}""", "anchors")]
    [InlineData(/*lang=json,strict*/ """{"properties": {"a": {"$id": "a"}}}""", "$id")]
    [InlineData(/*lang=json,strict*/ """{"$schema": "http://json-schema.org/draft-07/schema#"}""", "2020-12")]
    [InlineData(/*lang=json,strict*/ """{"items": [{"type": "string"}]}""", "prefixItems")]
    [InlineData(/*lang=json,strict*/ """{"pattern": "^\\p{Extended_Pictographic}$"}""", "pattern")]
    [InlineData("not json", "valid JSON")]
    public void TryCompile_ShouldRefuseWhatItCannotCheck(string json, string reason)
    {
        CmsJsonSchema.TryCompile(json, out _, out var error).Should().BeFalse();
        error.Should().Contain(reason);
    }

    [Fact]
    public void TryCompile_ShouldTreatUnknownKeywordsAsAnnotations() =>
        Compile(/*lang=json,strict*/ """{"type": "string", "label": "Title", "readOnly": true}""").Validate("\"x\"").Should().BeEmpty();

    [Fact]
    public void Validate_ShouldTreatFormatsOtherThanUriAsAnnotations() =>
        Compile(/*lang=json,strict*/ """{"type": "string", "format": "email"}""").Validate("\"not an email\"").Should().BeEmpty();

    [Fact]
    public void Validate_ShouldCountLengthsInCodePoints() =>
        Compile(/*lang=json,strict*/ """{"type": "string", "maxLength": 1}""").Validate("\"💩\"").Should().BeEmpty();

    // Patterns follow ECMA-262, where \d is an ASCII digit.
    [Fact]
    public void Validate_ShouldMatchPatternsAsEcmaScriptDoes() =>
        Compile(/*lang=json,strict*/ """{"type": "string", "pattern": "^\\d$"}""").Validate("\"٣\"")
            .Should().Equal(new CmsJsonSchemaError("#", "PatternMismatch"));

    // zod's own patterns, as realty's block schemas carry them.
    [Theory]
    [InlineData("^([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-8][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}|00000000-0000-0000-0000-000000000000|ffffffff-ffff-ffff-ffff-ffffffffffff)$", "019a3a3c-0b8e-7c7d-9d1e-2f3a4b5c6d7e", "not-a-uuid")]
    [InlineData("^(?!\\s*$)(?!\\s*(?:none|standard)\\s*$).+$", "premium", " none ")]
    public void Validate_ShouldRunZodsPatterns(string pattern, string matching, string other)
    {
        var schema = Compile($$"""{"type": "string", "pattern": {{System.Text.Json.JsonSerializer.Serialize(pattern)}}}""");

        schema.Validate($"\"{matching}\"").Should().BeEmpty();
        schema.Validate($"\"{other}\"").Should().Equal(new CmsJsonSchemaError("#", "PatternMismatch"));
    }

    [Fact]
    public void Validate_ShouldFollowRecursiveReferences()
    {
        var schema = Compile(/*lang=json,strict*/ """
            {
              "$defs": { "node": { "type": "object", "properties": { "children": { "type": "array", "items": { "$ref": "#/$defs/node" } } }, "required": ["name"] } },
              "$ref": "#/$defs/node"
            }
            """);

        schema.Validate(/*lang=json,strict*/ """{"name": "a", "children": [{"name": "b", "children": [{}]}]}""")
            .Should().Equal(new CmsJsonSchemaError("#/children/0/children/0/name", "PropertyRequired"));
    }

    private static CmsJsonSchema Compile(string json) =>
        CmsJsonSchema.TryCompile(json, out var schema, out var error) ? schema : throw new InvalidOperationException(error);
}
