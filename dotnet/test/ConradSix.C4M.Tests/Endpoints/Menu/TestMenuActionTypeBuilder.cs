namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Base;

public sealed class TestMenuActionTypeBuilder
{
    private string _name = "test-action-type";
    private int _version = 1;
    private string _jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {}}""";

    public static TestMenuActionTypeBuilder Create() => new();

    public static TestMenuActionTypeBuilder CreateSimple(string nameSuffix) =>
        new TestMenuActionTypeBuilder()
            .WithName($"{nameSuffix}-{Guid.NewGuid()}")
            .WithEmptySchema();

    public static TestMenuActionTypeBuilder CreateWithUrl(string nameSuffix) =>
        new TestMenuActionTypeBuilder()
            .WithName($"{nameSuffix}-{Guid.NewGuid()}")
            .WithUrlSchema();

    public static TestMenuActionTypeBuilder CreateWithPageRef(string nameSuffix) =>
        new TestMenuActionTypeBuilder()
            .WithName($"{nameSuffix}-{Guid.NewGuid()}")
            .WithPageRefSchema();

    public static TestMenuActionTypeBuilder CreateWithRequiredField(string nameSuffix, string fieldName) =>
        new TestMenuActionTypeBuilder()
            .WithName($"{nameSuffix}-{Guid.NewGuid()}")
            .WithRequiredFieldSchema(fieldName);

    public TestMenuActionTypeBuilder WithName(string name)
    {
        this._name = name;
        return this;
    }

    public TestMenuActionTypeBuilder WithVersion(int version)
    {
        this._version = version;
        return this;
    }

    public TestMenuActionTypeBuilder WithJsonSchema(string jsonSchema)
    {
        this._jsonSchema = jsonSchema;
        return this;
    }

    public TestMenuActionTypeBuilder WithEmptySchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {}}""";
        return this;
    }

    public TestMenuActionTypeBuilder WithUrlSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"url": {"type": "string"}}}""";
        return this;
    }

    public TestMenuActionTypeBuilder WithPageRefSchema()
    {
        this._jsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"pageRef": {"type": "object"}}}""";
        return this;
    }

    public TestMenuActionTypeBuilder WithRequiredFieldSchema(string fieldName)
    {
        this._jsonSchema = $$$"""{"type": "object", "properties": {"{{{fieldName}}}": {"type": "string"}}, "required": ["{{{fieldName}}}"]}""";
        return this;
    }

    public async Task<CreateCmsTypeDescriptorResponse> CreateAsync(
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateCmsTypeDescriptorRequest(
            Name: this._name,
            Version: this._version,
            JsonSchema: this._jsonSchema);

        var response = await client.PostAsJsonAsync("/menu-action-types", request, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var actionType = await response.Content.ReadFromJsonAsync<CreateCmsTypeDescriptorResponse>(cancellationToken);
        actionType.Should().NotBeNull();

        return actionType;
    }

    public async Task<HttpResponseMessage> CreateExpectingAsync(
        HttpClient client,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateCmsTypeDescriptorRequest(
            Name: this._name,
            Version: this._version,
            JsonSchema: this._jsonSchema);

        var response = await client.PostAsJsonAsync("/menu-action-types", request, cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }
}
