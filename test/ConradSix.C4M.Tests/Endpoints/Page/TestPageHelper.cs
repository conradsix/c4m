#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

using Microsoft.AspNetCore.WebUtilities;

public static class TestPageHelper
{
    public static async Task AssertValidationErrorFieldAsync(
        HttpResponseMessage response,
        string expectedField)
    {
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);
        document.RootElement.TryGetProperty("errors", out var errors).Should().BeTrue();
        errors.EnumerateObject()
            .Any(error => string.Equals(error.Name, expectedField, StringComparison.OrdinalIgnoreCase))
            .Should()
            .BeTrue($"the validation response should contain an error for '{expectedField}'");
    }

    public static async Task<UpdateCurrentPageVersionResponse> SetCurrentVersionAsync(
        HttpClient client,
        Guid pageVersionId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await client.GetAsync($"/pages/by-version/{pageVersionId}", cancellationToken);
        var page = loaded.IsSuccessStatusCode ? await loaded.Content.ReadFromJsonAsync<GetPageResponse>(cancellationToken) : null;
        var response = await client.PutAsJsonAsync($"/pages/by-version/{pageVersionId}", new PagePublicationRequest(page?.CurrentPageVersionId, page?.IsPublished ?? false), cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<UpdateCurrentPageVersionResponse>(cancellationToken);
        result.Should().NotBeNull();

        return result;
    }

    public static async Task<HttpResponseMessage> SetCurrentVersionExpectingAsync(
        HttpClient client,
        Guid pageVersionId,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken = default)
    {
        var loaded = await client.GetAsync($"/pages/by-version/{pageVersionId}", cancellationToken);
        var page = loaded.IsSuccessStatusCode ? await loaded.Content.ReadFromJsonAsync<GetPageResponse>(cancellationToken) : null;
        var response = await client.PutAsJsonAsync($"/pages/by-version/{pageVersionId}", new PagePublicationRequest(page?.CurrentPageVersionId, page?.IsPublished ?? false), cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }

    public static async Task<GetPageResponse> GetPageAsync(
        HttpClient client,
        Guid pageId,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync($"/pages/{pageId}", cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetPageResponse>(cancellationToken);
        result.Should().NotBeNull();

        return result;
    }

    public static async Task<GetPageResponse> GetPageAsync(
        HttpClient client,
        Guid pageId,
        bool? latest,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string?>
        {
            ["latest"] = latest.HasValue ? latest.Value ? "true" : "false" : null,
        };

        var url = QueryHelpers.AddQueryString($"/pages/{pageId}", queryParams);

        var response = await client.GetAsync(url, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetPageResponse>(cancellationToken);
        result.Should().NotBeNull();

        return result;
    }

    public static async Task<HttpResponseMessage> GetPageExpectingAsync(
        HttpClient client,
        Guid pageId,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync($"/pages/{pageId}", cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }

    public static async Task<GetPageResponse> GetPageVersionAsync(
        HttpClient client,
        Guid pageVersionId,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync($"/pages/by-version/{pageVersionId}", cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetPageResponse>(cancellationToken);
        result.Should().NotBeNull();

        return result;
    }

    public static async Task<HttpResponseMessage> GetPageVersionExpectingAsync(
        HttpClient client,
        Guid pageVersionId,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync($"/pages/by-version/{pageVersionId}", cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }

    public static async Task<GetPageHistoryResponse> GetPageHistoryAsync(
        HttpClient client,
        Guid pageId,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync($"/pages/{pageId}/history", cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetPageHistoryResponse>(cancellationToken);
        result.Should().NotBeNull();

        return result;
    }

    public static async Task<HttpResponseMessage> GetPageHistoryExpectingAsync(
        HttpClient client,
        Guid pageId,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync($"/pages/{pageId}/history", cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }

    public static async Task<GetPagePathResponse> GetPagePathAsync(
        HttpClient client,
        Guid pageId,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync($"/pages/{pageId}/path", cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetPagePathResponse>(cancellationToken);
        result.Should().NotBeNull();

        return result;
    }

    public static async Task<HttpResponseMessage> GetPagePathExpectingAsync(
        HttpClient client,
        Guid pageId,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken = default)
    {
        var response = await client.GetAsync($"/pages/{pageId}/path", cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }

    public static async Task<GetPagePathAvailabilityResponse> GetPagePathAvailabilityAsync(
        HttpClient client,
        string slug,
        Guid? parentId = null,
        Guid? pageId = null,
        CancellationToken cancellationToken = default)
    {
        var response = await GetPagePathAvailabilityExpectingAsync(client, slug, HttpStatusCode.OK, parentId, pageId, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<GetPagePathAvailabilityResponse>(cancellationToken);
        result.Should().NotBeNull();

        return result;
    }

    public static async Task<HttpResponseMessage> GetPagePathAvailabilityExpectingAsync(
        HttpClient client,
        string slug,
        HttpStatusCode expectedStatusCode,
        Guid? parentId = null,
        Guid? pageId = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string?> { ["slug"] = slug };
        if (parentId.HasValue)
        {
            queryParams["parentId"] = parentId.Value.ToString();
        }

        if (pageId.HasValue)
        {
            queryParams["pageId"] = pageId.Value.ToString();
        }

        var response = await client.GetAsync(QueryHelpers.AddQueryString("/pages/path-availability", queryParams), cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }
}
