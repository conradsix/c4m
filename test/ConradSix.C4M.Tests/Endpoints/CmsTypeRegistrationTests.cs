namespace ConradSix.C4M.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Base;

// A block or menu action type whose schema the engine can't check is an invalid request, never a server error.
[Collection(C4MAppCollection.Name)]
public sealed class CmsTypeRegistrationTests(C4MAppFixture fixture) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();

    [Theory]
    [InlineData("/block-types", /*lang=json,strict*/ """{"properties": []}""", "must be an object")]
    [InlineData("/menu-action-types", /*lang=json,strict*/ """{"properties": []}""", "must be an object")]
    [InlineData("/block-types", /*lang=json,strict*/ """{"patternProperties": "a"}""", "must be an object")]
    [InlineData("/block-types", /*lang=json,strict*/ """{"$defs": []}""", "must be an object")]
    [InlineData("/block-types", /*lang=json,strict*/ """{"$ref": "#"}""", "comes back to itself")]
    [InlineData("/menu-action-types", /*lang=json,strict*/ """{"pattern": "(?i)none"}""", "supported regular expression")]
    public async Task Register_ShouldRefuseSchemasItCannotCheck(string route, string schema, string reason)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(route, new CreateCmsTypeDescriptorRequest("broken", 1, schema), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain(reason);
    }
}
