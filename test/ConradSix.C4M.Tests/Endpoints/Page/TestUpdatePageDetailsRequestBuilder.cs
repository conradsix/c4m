namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

public sealed class TestUpdatePageDetailsRequestBuilder
{
    private Guid? _parentId;
    private string _slug = "test-page";
    private uint _version;

    public static TestUpdatePageDetailsRequestBuilder Create() => new();

    public TestUpdatePageDetailsRequestBuilder WithParentId(Guid? parentId)
    {
        this._parentId = parentId;
        return this;
    }

    public TestUpdatePageDetailsRequestBuilder WithSlug(string slug)
    {
        this._slug = slug;
        return this;
    }

    public TestUpdatePageDetailsRequestBuilder WithVersion(uint version)
    {
        this._version = version;
        return this;
    }

    public async Task<TestUpdatePageResult> UpdateAsync(HttpClient client, Guid pageId, CancellationToken cancellationToken = default)
    {
        var response = await this.UpdateExpectingAsync(client, pageId, HttpStatusCode.OK, cancellationToken);
        var page = await response.Content.ReadFromJsonAsync<GetPageResponse>(cancellationToken);
        page.Should().NotBeNull();
        return new(page, this._parentId, this._slug, [], [.. page.Blocks.Select((block, index) => new UpdatePageExistingBlock(block.Id, index))]);
    }

    public async Task<HttpResponseMessage> UpdateExpectingAsync(HttpClient client, Guid pageId, HttpStatusCode expectedStatusCode, CancellationToken cancellationToken = default)
    {
        var response = await client.PutAsJsonAsync($"/pages/{pageId}/details", new UpdatePageDetailsRequest(this._parentId, this._slug, this._version), cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);
        return response;
    }
}
