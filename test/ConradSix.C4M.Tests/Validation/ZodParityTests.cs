namespace ConradSix.C4M.Tests.Validation;

using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M.Validation;

// zod's own schemas and verdicts, from assets/zod-parity.json, which c4m-schema's zodParity test writes. Each schema
// must compile, and the engine must accept exactly the samples zod takes as stored data.
public sealed class ZodParityTests
{
    private static readonly string Fixture = Path.Combine(AppContext.BaseDirectory, "zod-parity.json");

    public static TheoryData<string> Cases() => [.. Load().Select(testCase => testCase.GetProperty("name").GetString()!)];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case_ShouldGetZodsAnswer(string name)
    {
        var testCase = Load().Single(item => item.GetProperty("name").GetString() == name);

        CmsJsonSchema.TryCompile(testCase.GetProperty("schema").GetRawText(), out var schema, out var error).Should().BeTrue(error);

        testCase.GetProperty("samples").EnumerateArray()
            .Where(sample => schema!.Validate(sample.GetProperty("data")).Count == 0 != sample.GetProperty("valid").GetBoolean())
            .Select(sample => sample.GetProperty("data").GetRawText())
            .Should().BeEmpty();
    }

    private static JsonElement[] Load()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Fixture));
        return [.. document.RootElement.Clone().EnumerateArray()];
    }
}
