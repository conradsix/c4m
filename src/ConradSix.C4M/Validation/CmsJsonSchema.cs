namespace ConradSix.C4M.Validation;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

// A JSON Schema 2020-12 validator for the keywords zod 4's toJSONSchema emits. Compiling refuses every other
// validation keyword, so data is never accepted because a keyword went unchecked. Unknown keywords are annotations, as
// the specification says. So are formats other than uri: zod emits a pattern beside every other format it uses.
internal sealed partial class CmsJsonSchema
{
    private const string Dialect = "https://json-schema.org/draft/2020-12/schema";

    private const int MaxDepth = 512;

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(1);

    private static readonly HashSet<string> Unsupported = new(StringComparer.Ordinal)
    {
        "$anchor", "$dynamicRef", "$dynamicAnchor", "$vocabulary", "$recursiveRef", "$recursiveAnchor",
        "contains", "minContains", "maxContains", "uniqueItems", "minProperties", "maxProperties",
        "dependentRequired", "dependentSchemas", "dependencies", "if", "then", "else",
        "unevaluatedItems", "unevaluatedProperties", "additionalItems",
    };

    private static readonly Dictionary<string, string> TypeErrors = new(StringComparer.Ordinal)
    {
        ["string"] = "StringExpected",
        ["number"] = "NumberExpected",
        ["integer"] = "IntegerExpected",
        ["boolean"] = "BooleanExpected",
        ["object"] = "ObjectExpected",
        ["array"] = "ArrayExpected",
        ["null"] = "NullExpected",
    };

    private readonly Node _root;

    private CmsJsonSchema(Node root) => this._root = root;

    public static bool TryCompile(
        string json,
        [NotNullWhen(true)] out CmsJsonSchema? schema,
        [NotNullWhen(false)] out string? error)
    {
        schema = null;
        JsonElement document;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            document = parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            error = "The schema isn't valid JSON.";
            return false;
        }

        try
        {
            if (document.ValueKind == JsonValueKind.Object &&
                document.TryGetProperty("$schema", out var dialect) &&
                (dialect.ValueKind != JsonValueKind.String || dialect.GetString()!.TrimEnd('#') != Dialect))
            {
                throw new UnsupportedSchemaException($"The schema must be JSON Schema 2020-12 ({Dialect}).");
            }

            schema = new CmsJsonSchema(new Compiler(document).Compile());
            error = null;
            return true;
        }
        catch (UnsupportedSchemaException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // Validates JSON text. Throws JsonException when the text isn't JSON.
    public IReadOnlyList<CmsJsonSchemaError> Validate(string json)
    {
        using var document = JsonDocument.Parse(json);
        return this.Validate(document.RootElement);
    }

    public IReadOnlyList<CmsJsonSchemaError> Validate(JsonElement instance)
    {
        var errors = new List<CmsJsonSchemaError>();
        Evaluate(this._root, instance, "#", errors, 0);
        return errors;
    }

    private static bool IsValid(Node node, JsonElement instance, int depth)
    {
        var errors = new List<CmsJsonSchemaError>();
        Evaluate(node, instance, "#", errors, depth);
        return errors.Count == 0;
    }

    private static void Evaluate(Node node, JsonElement instance, string path, List<CmsJsonSchemaError> errors, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException("The schema refers to itself without end.");
        }

        if (node.Boolean is { } allowed)
        {
            if (!allowed)
            {
                errors.Add(new(path, "NotAllowed"));
            }

            return;
        }

        if (node.Ref is { } target)
        {
            Evaluate(target, instance, path, errors, depth + 1);
        }

        if (node.Types is { } types && !types.Any(type => HasType(instance, type)))
        {
            errors.Add(new(path, types.Length == 1 ? TypeErrors[types[0]] : "NoTypeValidates"));
            return;
        }

        if (node.Const is { } constant && !JsonEquals(instance, constant))
        {
            errors.Add(new(path, "ConstMismatch"));
        }

        if (node.Enum is { } values && !values.Any(value => JsonEquals(instance, value)))
        {
            errors.Add(new(path, "NotInEnumeration"));
        }

        switch (instance.ValueKind)
        {
            case JsonValueKind.String:
                EvaluateString(node, instance.GetString()!, path, errors);
                break;
            case JsonValueKind.Number:
                EvaluateNumber(node, instance, path, errors);
                break;
            case JsonValueKind.Object:
                EvaluateObject(node, instance, path, errors, depth);
                break;
            case JsonValueKind.Array:
                EvaluateArray(node, instance, path, errors, depth);
                break;
            default:
                break;
        }

        foreach (var schema in node.AllOf ?? [])
        {
            Evaluate(schema, instance, path, errors, depth + 1);
        }

        if (node.AnyOf is { } anyOf && !anyOf.Any(schema => IsValid(schema, instance, depth + 1)))
        {
            errors.Add(new(path, "NotAnyOf"));
        }

        if (node.OneOf is { } oneOf && oneOf.Count(schema => IsValid(schema, instance, depth + 1)) != 1)
        {
            errors.Add(new(path, "NotOneOf"));
        }

        if (node.Not is { } not && IsValid(not, instance, depth + 1))
        {
            errors.Add(new(path, "ExcludedSchemaValidates"));
        }
    }

    private static void EvaluateString(Node node, string value, string path, List<CmsJsonSchemaError> errors)
    {
        // Lengths count code points, not UTF-16 units.
        var length = node.MinLength.HasValue || node.MaxLength.HasValue ? value.EnumerateRunes().Count() : 0;
        if (length < node.MinLength)
        {
            errors.Add(new(path, "StringTooShort"));
        }

        if (length > node.MaxLength)
        {
            errors.Add(new(path, "StringTooLong"));
        }

        if (node.Pattern is { } pattern && !Matches(pattern, value))
        {
            errors.Add(new(path, "PatternMismatch"));
        }

        if (node.UriFormat && !IsUri(value))
        {
            errors.Add(new(path, "UriExpected"));
        }
    }

    private static void EvaluateNumber(Node node, JsonElement value, string path, List<CmsJsonSchemaError> errors)
    {
        if ((node.Minimum is { } minimum && Compare(value, minimum) < 0) ||
            (node.ExclusiveMinimum is { } exclusiveMinimum && Compare(value, exclusiveMinimum) <= 0))
        {
            errors.Add(new(path, "NumberTooSmall"));
        }

        if ((node.Maximum is { } maximum && Compare(value, maximum) > 0) ||
            (node.ExclusiveMaximum is { } exclusiveMaximum && Compare(value, exclusiveMaximum) >= 0))
        {
            errors.Add(new(path, "NumberTooBig"));
        }

        if (node.MultipleOf is { } multipleOf && !IsMultipleOf(value, multipleOf))
        {
            errors.Add(new(path, "NumberNotMultipleOf"));
        }
    }

    private static void EvaluateObject(Node node, JsonElement value, string path, List<CmsJsonSchemaError> errors, int depth)
    {
        foreach (var name in node.Required ?? [])
        {
            if (!value.TryGetProperty(name, out _))
            {
                errors.Add(new(Child(path, name), "PropertyRequired"));
            }
        }

        foreach (var property in value.EnumerateObject())
        {
            var propertyPath = Child(path, property.Name);
            var matched = false;
            if (node.Properties is { } properties && properties.TryGetValue(property.Name, out var schema))
            {
                matched = true;
                Evaluate(schema, property.Value, propertyPath, errors, depth + 1);
            }

            foreach (var (pattern, patternSchema) in node.PatternProperties ?? [])
            {
                if (Matches(pattern, property.Name))
                {
                    matched = true;
                    Evaluate(patternSchema, property.Value, propertyPath, errors, depth + 1);
                }
            }

            if (!matched && node.AdditionalProperties is { } additional)
            {
                if (additional.Boolean == false)
                {
                    errors.Add(new(propertyPath, "NoAdditionalPropertiesAllowed"));
                }
                else
                {
                    Evaluate(additional, property.Value, propertyPath, errors, depth + 1);
                }
            }

            if (node.PropertyNames is { } names && !IsValid(names, JsonSerializer.SerializeToElement(property.Name), depth + 1))
            {
                errors.Add(new(propertyPath, "PropertyNameNotValid"));
            }
        }
    }

    private static void EvaluateArray(Node node, JsonElement value, string path, List<CmsJsonSchemaError> errors, int depth)
    {
        var count = value.GetArrayLength();
        if (count < node.MinItems)
        {
            errors.Add(new(path, "TooFewItems"));
        }

        if (count > node.MaxItems)
        {
            errors.Add(new(path, "TooManyItems"));
        }

        var prefix = node.PrefixItems ?? [];
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var schema = index < prefix.Length ? prefix[index] : node.Items;
            if (schema is not null)
            {
                if (index >= prefix.Length && schema.Boolean == false)
                {
                    errors.Add(new(path, "TooManyItemsInTuple"));
                    return;
                }

                Evaluate(schema, item, Child(path, index.ToString(CultureInfo.InvariantCulture)), errors, depth + 1);
            }

            index++;
        }
    }

    private static bool HasType(JsonElement instance, string type) => type switch
    {
        "string" => instance.ValueKind == JsonValueKind.String,
        "number" => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && IsInteger(instance),
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "null" => instance.ValueKind == JsonValueKind.Null,
        _ => false,
    };

    private static bool IsInteger(JsonElement number) => number.TryGetDecimal(out var value)
        ? value == decimal.Truncate(value)
        : double.IsFinite(number.GetDouble()) && Math.Floor(number.GetDouble()) == number.GetDouble();

    // Numbers compare as decimals when both fit, which keeps 0.1 and 0.3 exact, and as doubles otherwise.
    private static int Compare(JsonElement number, Bound bound) =>
        bound.Decimal is { } exact && number.TryGetDecimal(out var value)
            ? value.CompareTo(exact)
            : number.GetDouble().CompareTo(bound.Double);

    private static bool IsMultipleOf(JsonElement number, Bound divisor)
    {
        if (divisor.Decimal is { } exact && number.TryGetDecimal(out var value))
        {
            return value % exact == 0;
        }

        var quotient = number.GetDouble() / divisor.Double;
        return double.IsFinite(quotient) && Math.Abs(quotient - Math.Round(quotient)) < 1e-9;
    }

    private static bool JsonEquals(JsonElement left, JsonElement right)
    {
        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            return left.TryGetDecimal(out var a) && right.TryGetDecimal(out var b)
                ? a == b
                : left.GetDouble().Equals(right.GetDouble());
        }

        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.String:
                return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Array:
                return left.GetArrayLength() == right.GetArrayLength() &&
                    left.EnumerateArray().Zip(right.EnumerateArray()).All(pair => JsonEquals(pair.First, pair.Second));
            case JsonValueKind.Object:
                var properties = left.EnumerateObject().ToArray();
                return properties.Length == right.EnumerateObject().Count() &&
                    properties.All(property => right.TryGetProperty(property.Name, out var other) && JsonEquals(property.Value, other));
            default:
                return true;
        }
    }

    // A pattern matches anywhere in the string unless it anchors itself, as in ECMA-262.
    private static bool Matches(Regex pattern, string value)
    {
        try
        {
            return pattern.IsMatch(value);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    // An absolute URI as RFC 3986 defines it. IP literals hold an IPv6 address, whose embedded IPv4 octets have no leading
    // zeros, or an IPvFuture address.
    private static bool IsUri(string value)
    {
        var match = RfcUri().Match(value);
        if (!match.Success)
        {
            return false;
        }

        var literal = match.Groups["literal"];
        if (!literal.Success)
        {
            return true;
        }

        var address = literal.Value;
        if (IpFuture().IsMatch(address))
        {
            return true;
        }

        return !address.Contains('%', StringComparison.Ordinal) &&
            IPAddress.TryParse(address, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6 &&
            (!address.Contains('.', StringComparison.Ordinal) || address[(address.LastIndexOf(':') + 1)..].Split('.').All(octet => DecimalOctet().IsMatch(octet)));
    }

    [GeneratedRegex(
        """\A[A-Za-z][A-Za-z0-9+\-.]*:(?://(?:(?:[A-Za-z0-9\-._~!$&'()*+,;=:]|%[0-9A-Fa-f]{2})*@)?(?:\[(?<literal>[^\[\]]*)\]|(?:[A-Za-z0-9\-._~!$&'()*+,;=]|%[0-9A-Fa-f]{2})*)(?::[0-9]*)?(?:/(?:[A-Za-z0-9\-._~!$&'()*+,;=:@]|%[0-9A-Fa-f]{2})*)*|/?(?:(?:[A-Za-z0-9\-._~!$&'()*+,;=:@]|%[0-9A-Fa-f]{2})+(?:/(?:[A-Za-z0-9\-._~!$&'()*+,;=:@]|%[0-9A-Fa-f]{2})*)*)?)(?:\?(?:[A-Za-z0-9\-._~!$&'()*+,;=:@/?]|%[0-9A-Fa-f]{2})*)?(?:#(?:[A-Za-z0-9\-._~!$&'()*+,;=:@/?]|%[0-9A-Fa-f]{2})*)?\z""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RfcUri();

    [GeneratedRegex("""\Av[0-9A-Fa-f]+\.[A-Za-z0-9\-._~!$&'()*+,;=:]+\z""", RegexOptions.CultureInvariant)]
    private static partial Regex IpFuture();

    [GeneratedRegex("""\A(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\z""", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalOctet();

    private static string Child(string path, string segment) =>
        $"{path}/{segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)}";

    // A number bound: exact when it fits a decimal.
    private readonly record struct Bound(decimal? Decimal, double Double);

    private sealed class Node
    {
        public bool? Boolean { get; init; }

        public Node? Ref { get; set; }

        public string[]? Types { get; set; }

        public JsonElement? Const { get; set; }

        public JsonElement[]? Enum { get; set; }

        public int? MinLength { get; set; }

        public int? MaxLength { get; set; }

        public Regex? Pattern { get; set; }

        public bool UriFormat { get; set; }

        public Bound? Minimum { get; set; }

        public Bound? Maximum { get; set; }

        public Bound? ExclusiveMinimum { get; set; }

        public Bound? ExclusiveMaximum { get; set; }

        public Bound? MultipleOf { get; set; }

        public string[]? Required { get; set; }

        public Dictionary<string, Node>? Properties { get; set; }

        public (Regex Pattern, Node Schema)[]? PatternProperties { get; set; }

        public Node? AdditionalProperties { get; set; }

        public Node? PropertyNames { get; set; }

        public Node[]? PrefixItems { get; set; }

        public Node? Items { get; set; }

        public int? MinItems { get; set; }

        public int? MaxItems { get; set; }

        public Node[]? AllOf { get; set; }

        public Node[]? AnyOf { get; set; }

        public Node[]? OneOf { get; set; }

        public Node? Not { get; set; }
    }

#pragma warning disable CA1064 // Never leaves the compiler: TryCompile turns it into an error message.
#pragma warning disable CA1032 // It carries only the reason a schema can't be used.
    private sealed class UnsupportedSchemaException(string message) : Exception(message);
#pragma warning restore CA1032
#pragma warning restore CA1064

    // Compiles each subschema once, by its JSON Pointer, so references, including cycles, share their target.
    private sealed class Compiler(JsonElement document)
    {
        private readonly Dictionary<string, Node> _compiled = new(StringComparer.Ordinal);

        private readonly List<(Node Node, string Reference, string Path)> _references = [];

        public Node Compile()
        {
            var root = this.CompileAt(document, "#");
            for (var index = 0; index < this._references.Count; index++)
            {
                var (node, reference, path) = this._references[index];
                node.Ref = this._compiled.TryGetValue(reference, out var target)
                    ? target
                    : this.CompileAt(this.Resolve(reference, path), reference);
            }

            return root;
        }

        // A count. 2.0 is an integer as much as 2 is.
        private static int NonNegativeInteger(JsonElement value, string keyword, string path) =>
            value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) && number >= 0 && number <= int.MaxValue && number == decimal.Truncate(number)
                ? (int)number
                : throw new UnsupportedSchemaException($"{keyword} at {path} must be a non-negative integer.");

        private static Bound Number(JsonElement value, string keyword, string path) =>
            value.ValueKind == JsonValueKind.Number
                ? new Bound(value.TryGetDecimal(out var exact) ? exact : null, value.GetDouble())
                : throw new UnsupportedSchemaException($"{keyword} at {path} must be a number.");

        private static Regex Pattern(string pattern, string path)
        {
            try
            {
                return new Regex(pattern, RegexOptions.ECMAScript, PatternTimeout);
            }
            catch (ArgumentException ex)
            {
                throw new UnsupportedSchemaException($"The pattern at {path} isn't a supported regular expression: {ex.Message}");
            }
        }

        private static string Text(JsonElement value, string keyword, string path) =>
            value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : throw new UnsupportedSchemaException($"{keyword} at {path} must be a string.");

        private static string[] Strings(JsonElement value, string keyword, string path) =>
            value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String)
                ? [.. value.EnumerateArray().Select(item => item.GetString()!)]
                : throw new UnsupportedSchemaException($"{keyword} at {path} must be an array of strings.");

        private Node CompileAt(JsonElement schema, string path)
        {
            if (this._compiled.TryGetValue(path, out var existing))
            {
                return existing;
            }

            if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                var boolean = new Node { Boolean = schema.ValueKind == JsonValueKind.True };
                this._compiled[path] = boolean;
                return boolean;
            }

            if (schema.ValueKind != JsonValueKind.Object)
            {
                throw new UnsupportedSchemaException($"The schema at {path} must be an object or a boolean.");
            }

            var node = new Node();
            this._compiled[path] = node;
            foreach (var keyword in schema.EnumerateObject())
            {
                this.Apply(node, keyword, Child(path, keyword.Name));
            }

            return node;
        }

        private void Apply(Node node, JsonProperty keyword, string path)
        {
            var value = keyword.Value;
            switch (keyword.Name)
            {
                case "$ref":
                    var reference = Text(value, keyword.Name, path);
                    if (!reference.StartsWith('#'))
                    {
                        throw new UnsupportedSchemaException($"The reference at {path} must point inside the schema.");
                    }

                    this._references.Add((node, Uri.UnescapeDataString(reference), path));
                    break;
                case "$defs":
                    foreach (var definition in value.EnumerateObject())
                    {
                        this.CompileAt(definition.Value, Child(path, definition.Name));
                    }

                    break;
                case "$id":
                    if (path != "#/$id")
                    {
                        throw new UnsupportedSchemaException($"$id is supported only at the schema's root, not at {path}.");
                    }

                    break;
                case "type":
                    node.Types = value.ValueKind == JsonValueKind.String ? [value.GetString()!] : Strings(value, "type", path);
                    if (node.Types.FirstOrDefault(type => !TypeErrors.ContainsKey(type)) is { } unknown)
                    {
                        throw new UnsupportedSchemaException($"The type '{unknown}' at {path} isn't a JSON Schema type.");
                    }

                    break;
                case "const":
                    node.Const = value;
                    break;
                case "enum":
                    node.Enum = value.ValueKind == JsonValueKind.Array
                        ? [.. value.EnumerateArray()]
                        : throw new UnsupportedSchemaException($"enum at {path} must be an array.");
                    break;
                case "minLength":
                    node.MinLength = NonNegativeInteger(value, keyword.Name, path);
                    break;
                case "maxLength":
                    node.MaxLength = NonNegativeInteger(value, keyword.Name, path);
                    break;
                case "pattern":
                    node.Pattern = Pattern(Text(value, keyword.Name, path), path);
                    break;
                case "format":
                    node.UriFormat = Text(value, keyword.Name, path) == "uri";
                    break;
                case "minimum":
                    node.Minimum = Number(value, keyword.Name, path);
                    break;
                case "maximum":
                    node.Maximum = Number(value, keyword.Name, path);
                    break;
                case "exclusiveMinimum":
                    node.ExclusiveMinimum = Number(value, keyword.Name, path);
                    break;
                case "exclusiveMaximum":
                    node.ExclusiveMaximum = Number(value, keyword.Name, path);
                    break;
                case "multipleOf":
                    node.MultipleOf = Number(value, keyword.Name, path);
                    if (node.MultipleOf.Value.Double <= 0)
                    {
                        throw new UnsupportedSchemaException($"multipleOf at {path} must be greater than zero.");
                    }

                    break;
                case "required":
                    node.Required = Strings(value, keyword.Name, path);
                    break;
                case "properties":
                    node.Properties = value.EnumerateObject().ToDictionary(
                        property => property.Name,
                        property => this.CompileAt(property.Value, Child(path, property.Name)),
                        StringComparer.Ordinal);
                    break;
                case "patternProperties":
                    node.PatternProperties = [.. value.EnumerateObject().Select(property => (
                        Pattern(property.Name, path),
                        this.CompileAt(property.Value, Child(path, property.Name)))),];
                    break;
                case "additionalProperties":
                    node.AdditionalProperties = this.CompileAt(value, path);
                    break;
                case "propertyNames":
                    node.PropertyNames = this.CompileAt(value, path);
                    break;
                case "prefixItems":
                    node.PrefixItems = this.Schemas(value, path);
                    break;
                case "items":
                    node.Items = value.ValueKind == JsonValueKind.Array
                        ? throw new UnsupportedSchemaException($"items at {path} must be a schema; JSON Schema 2020-12 lists tuples in prefixItems.")
                        : this.CompileAt(value, path);
                    break;
                case "minItems":
                    node.MinItems = NonNegativeInteger(value, keyword.Name, path);
                    break;
                case "maxItems":
                    node.MaxItems = NonNegativeInteger(value, keyword.Name, path);
                    break;
                case "allOf":
                    node.AllOf = this.Schemas(value, path);
                    break;
                case "anyOf":
                    node.AnyOf = this.Schemas(value, path);
                    break;
                case "oneOf":
                    node.OneOf = this.Schemas(value, path);
                    break;
                case "not":
                    node.Not = this.CompileAt(value, path);
                    break;
                case var name when Unsupported.Contains(name):
                    throw new UnsupportedSchemaException($"The keyword {name} at {path} isn't supported.");
                default:
                    // Annotations, and keywords JSON Schema doesn't define, which it treats as annotations.
                    break;
            }
        }

        private Node[] Schemas(JsonElement value, string path) => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0
            ? [.. value.EnumerateArray().Select((schema, index) => this.CompileAt(schema, Child(path, index.ToString(CultureInfo.InvariantCulture))))]
            : throw new UnsupportedSchemaException($"The keyword at {path} must be a non-empty array of schemas.");

        // Follows a JSON Pointer fragment ("#/$defs/name") through the schema document. Anchors ("#name") aren't supported.
        private JsonElement Resolve(string reference, string path)
        {
            var pointer = reference[1..];
            if (pointer.Length > 0 && !pointer.StartsWith('/'))
            {
                throw new UnsupportedSchemaException($"The reference at {path} must be a JSON Pointer; anchors aren't supported.");
            }

            var target = document;
            foreach (var segment in pointer.Length == 0 ? [] : pointer[1..].Split('/'))
            {
                var name = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                target = target.ValueKind switch
                {
                    JsonValueKind.Object when target.TryGetProperty(name, out var child) => child,
                    JsonValueKind.Array when int.TryParse(name, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < target.GetArrayLength() => target[index],
                    _ => throw new UnsupportedSchemaException($"The reference at {path} points at nothing: {reference}."),
                };
            }

            return target;
        }
    }
}
