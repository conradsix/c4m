namespace ConradSix.C4M.Tests.Validation;

using System.Text.Json;

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
    [InlineData(/*lang=json,strict*/ """{"pattern": "(?i)none"}""", "pattern")]
    [InlineData(/*lang=json,strict*/ """{"pattern": "^[^\\S]$"}""", "pattern")]
    [InlineData(/*lang=json,strict*/ """{"pattern": "[]"}""", "pattern")]
    [InlineData(/*lang=json,strict*/ """{"properties": []}""", "properties at #/properties must be an object")]
    [InlineData(/*lang=json,strict*/ """{"patternProperties": 1}""", "patternProperties at #/patternProperties must be an object")]
    [InlineData(/*lang=json,strict*/ """{"$defs": "a"}""", "$defs at #/$defs must be an object")]
    [InlineData(/*lang=json,strict*/ """{"maxLength": 1e-29}""", "non-negative integer")]
    [InlineData(/*lang=json,strict*/ """{"$ref": "#"}""", "comes back to itself")]
    [InlineData(/*lang=json,strict*/ """{"$defs": {"a": {"anyOf": [{"$ref": "#/$defs/b"}]}, "b": {"$ref": "#/$defs/a"}}}""", "comes back to itself")]
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

    // Patterns mean what they mean in JavaScript, where zod checks them: \s takes in Unicode spaces such as U+00A0 and
    // U+3000, . stops at every line terminator, and $ is the end of the input.
    [Theory]
    [InlineData("^\\S+$", "a\u00a0b")]
    [InlineData("^[^\\s]+$", "a\u3000")]
    [InlineData("^(?!\\s*$).+$", "\u00a0\u2028")]
    [InlineData("^.+$", "a\rb")]
    [InlineData("^[a-z]+$", "abc\n")]
    public void Validate_ShouldMatchPatternsAsJavaScriptDoes(string pattern, string data) =>
        Compile($$"""{"type": "string", "pattern": {{JsonSerializer.Serialize(pattern)}}}""").Validate(JsonSerializer.Serialize(data))
            .Should().Equal(new CmsJsonSchemaError("#", "PatternMismatch"));

    [Theory]
    [InlineData("^\\s$", "\u00a0")]
    [InlineData("^[\\s,]+$", "\u3000,\t")]
    [InlineData("^a.c$", "a\u00e9c")]
    [InlineData("^\\x41\\u0042\\cJ[\\b]\\.$", "AB\n\b.")]
    [InlineData("^[a[]+$", "[a[")]
    public void Validate_ShouldAcceptWhatJavaScriptPatternsMatch(string pattern, string data) =>
        Compile($$"""{"type": "string", "pattern": {{JsonSerializer.Serialize(pattern)}}}""").Validate(JsonSerializer.Serialize(data))
            .Should().BeEmpty();

    // Numbers are the doubles JSON.parse makes of them, as zod judges them. A decimal would read 1e-29 as 0, and keep
    // digits that make 0.10000000000000000001 greater than 0.1.
    [Theory]
    [InlineData(/*lang=json,strict*/ """{"enum": [0, 1]}""", "1e-29", "NotInEnumeration")]
    [InlineData(/*lang=json,strict*/ """{"const": 0}""", "1e-29", "ConstMismatch")]
    [InlineData(/*lang=json,strict*/ """{"type": "integer"}""", "1e-29", "IntegerExpected")]
    [InlineData(/*lang=json,strict*/ """{"exclusiveMinimum": 0.1}""", "0.10000000000000000001", "NumberTooSmall")]
    [InlineData(/*lang=json,strict*/ """{"maximum": 0}""", "1e-29", "NumberTooBig")]
    [InlineData(/*lang=json,strict*/ """{"type": "number"}""", "1e400", "NumberExpected")]
    public void Validate_ShouldReadNumbersAsJavaScriptDoes(string schema, string data, string kind) =>
        Compile(schema).Validate(data).Should().Equal(new CmsJsonSchemaError("#", kind));

    [Theory]
    [InlineData(/*lang=json,strict*/ """{"exclusiveMinimum": 0}""", "1e-29")]
    [InlineData(/*lang=json,strict*/ """{"const": 0.1}""", "0.10000000000000000001")]
    [InlineData(/*lang=json,strict*/ """{"type": "integer"}""", "2.0")]
    [InlineData(/*lang=json,strict*/ """{"multipleOf": 0.1}""", "0.3")]
    [InlineData(/*lang=json,strict*/ """{"multipleOf": 0.01}""", "19.99")]
    public void Validate_ShouldAcceptNumbersJavaScriptAccepts(string schema, string data) =>
        Compile(schema).Validate(data).Should().BeEmpty();

    // zod's own patterns, as block schemas carry them.
    [Theory]
    [InlineData("^([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-8][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}|00000000-0000-0000-0000-000000000000|ffffffff-ffff-ffff-ffff-ffffffffffff)$", "019a3a3c-0b8e-7c7d-9d1e-2f3a4b5c6d7e", "not-a-uuid")]
    [InlineData("^(?!\\s*$)(?!\\s*(?:none|standard)\\s*$).+$", "premium", " none ")]
    public void Validate_ShouldRunZodsPatterns(string pattern, string matching, string other)
    {
        var schema = Compile($$"""{"type": "string", "pattern": {{JsonSerializer.Serialize(pattern)}}}""");

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
