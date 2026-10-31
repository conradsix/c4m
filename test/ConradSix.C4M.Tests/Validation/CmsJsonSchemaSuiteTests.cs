namespace ConradSix.C4M.Tests.Validation;

using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M.Validation;

// The JSON Schema Test Suite's draft 2020-12 cases for the keywords the engine checks. A case whose schema uses
// something the engine refuses must fail to compile; every other case must get the suite's answer.
public sealed class CmsJsonSchemaSuiteTests
{
    private static readonly string Suite = Path.Combine(AppContext.BaseDirectory, "json-schema-test-suite");

    // Keywords and reference forms the engine refuses rather than ignores.
    private static readonly HashSet<string> Refused =
    [
        "$anchor", "$dynamicRef", "$dynamicAnchor", "$vocabulary", "$recursiveRef", "$recursiveAnchor",
        "contains", "minContains", "maxContains", "uniqueItems", "minProperties", "maxProperties",
        "dependentRequired", "dependentSchemas", "dependencies", "if", "then", "else",
        "unevaluatedItems", "unevaluatedProperties", "additionalItems",
    ];

    public static TheoryData<string, int> Cases()
    {
        var cases = new TheoryData<string, int>();
        foreach (var file in Directory.EnumerateFiles(Suite, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var name = Path.GetRelativePath(Suite, file);
            for (var index = 0; index < Load(name).Length; index++)
            {
                cases.Add(name, index);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case_ShouldGetTheSuitesAnswer(string file, int index)
    {
        var testCase = Load(file)[index];
        var description = testCase.GetProperty("description").GetString();
        var schemaText = testCase.GetProperty("schema").GetRawText();

        if (!CmsJsonSchema.TryCompile(schemaText, out var schema, out var error))
        {
            UsesSomethingRefused(testCase.GetProperty("schema"), isRoot: true)
                .Should().BeTrue($"'{description}' was refused ({error}), but it uses nothing the engine refuses");
            return;
        }

        var wrong = testCase.GetProperty("tests").EnumerateArray()
            .Where(test => schema.Validate(test.GetProperty("data")).Count == 0 != test.GetProperty("valid").GetBoolean())
            .Select(test => $"{test.GetProperty("description").GetString()}: {test.GetProperty("data").GetRawText()}");
        wrong.Should().BeEmpty(description);
    }

    private static JsonElement[] Load(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Suite, file)));
        return [.. document.RootElement.Clone().EnumerateArray()];
    }

    // ECMA-262 Unicode property escapes (\p{Letter}), which .NET's ECMAScript regular expressions can't run.
    private static bool IsUnicodePropertyEscape(string? pattern) =>
        pattern?.Contains("\\p{", StringComparison.Ordinal) == true || pattern?.Contains("\\P{", StringComparison.Ordinal) == true;

    private static bool UsesSomethingRefused(JsonElement schema, bool isRoot) => schema.ValueKind switch
    {
        JsonValueKind.Object => schema.EnumerateObject().Any(keyword =>
            Refused.Contains(keyword.Name) ||
            (keyword.Name == "$id" && !isRoot) ||
            (keyword.Name == "$schema" && keyword.Value.GetString() != "https://json-schema.org/draft/2020-12/schema") ||
            (keyword.Name == "$ref" && keyword.Value.GetString() is { } reference && reference != "#" && !reference.StartsWith("#/", StringComparison.Ordinal)) ||
            (keyword.Name is "pattern" && IsUnicodePropertyEscape(keyword.Value.GetString())) ||
            (keyword.Name is "patternProperties" && keyword.Value.EnumerateObject().Any(pattern => IsUnicodePropertyEscape(pattern.Name))) ||
            UsesSomethingRefused(keyword.Value, isRoot: false)),
        JsonValueKind.Array => schema.EnumerateArray().Any(item => UsesSomethingRefused(item, isRoot: false)),
        _ => false,
    };
}
