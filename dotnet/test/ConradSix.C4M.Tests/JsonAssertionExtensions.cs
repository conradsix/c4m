#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace AwesomeAssertions;

using System.Text.Json.Nodes;

using AwesomeAssertions.Primitives;

public static class JsonAssertionExtensions
{
    // Compares two JSON texts as JSON: property order and whitespace don't matter.
    public static AndConstraint<TAssertions> BeJsonEqual<TAssertions>(this StringAssertions<TAssertions> assertions, string expected)
        where TAssertions : StringAssertions<TAssertions>
    {
        ArgumentNullException.ThrowIfNull(assertions);
        JsonNode.DeepEquals(JsonNode.Parse(assertions.Subject ?? "null"), JsonNode.Parse(expected))
            .Should().BeTrue($"{assertions.Subject} should be the same JSON as {expected}");
        return new AndConstraint<TAssertions>((TAssertions)assertions);
    }
}
