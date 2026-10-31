#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

using Microsoft.AspNetCore.WebUtilities;

public class TestListPagesRequestBuilder
{
    private int _limit = 3;
    private int _offset;
    private string? _search;
    private ListPagesSort _sort = ListPagesSort.SlugAsc;
    private string? _slugFilter;
    private ListPagesStringFilterType _slugFilterType = ListPagesStringFilterType.Contains;
    private string? _pathFilter;
    private ListPagesStringFilterType _pathFilterType = ListPagesStringFilterType.Contains;
    private DateTimeOffset? _createdAfter;
    private DateTimeOffset? _createdBefore;
    private DateTimeOffset? _updatedAfter;
    private DateTimeOffset? _updatedBefore;

    public static TestListPagesRequestBuilder ForRealtor(Guid realtorId) =>
        new TestListPagesRequestBuilder().WithRealtorId(realtorId);

    public TestListPagesRequestBuilder WithRealtorId(Guid realtorId)
    {
        _ = realtorId;
        return this;
    }

    public TestListPagesRequestBuilder WithLimit(int limit)
    {
        this._limit = limit;
        return this;
    }

    public TestListPagesRequestBuilder WithOffset(int offset)
    {
        this._offset = offset;
        return this;
    }

    public TestListPagesRequestBuilder WithSearch(string search)
    {
        this._search = search;
        return this;
    }

    public TestListPagesRequestBuilder WithSort(ListPagesSort sort)
    {
        this._sort = sort;
        return this;
    }

    public TestListPagesRequestBuilder WithSlugFilter(
        string? filter,
        ListPagesStringFilterType filterType = ListPagesStringFilterType.Contains)
    {
        this._slugFilter = filter;
        this._slugFilterType = filterType;
        return this;
    }

    public TestListPagesRequestBuilder WithSlugContains(string filter)
    {
        this._slugFilter = filter;
        this._slugFilterType = ListPagesStringFilterType.Contains;
        return this;
    }

    public TestListPagesRequestBuilder WithSlugStartsWith(string filter)
    {
        this._slugFilter = filter;
        this._slugFilterType = ListPagesStringFilterType.StartsWith;
        return this;
    }

    public TestListPagesRequestBuilder WithSlugEndsWith(string filter)
    {
        this._slugFilter = filter;
        this._slugFilterType = ListPagesStringFilterType.EndsWith;
        return this;
    }

    public TestListPagesRequestBuilder WithSlugRegex(string pattern)
    {
        this._slugFilter = pattern;
        this._slugFilterType = ListPagesStringFilterType.Regex;
        return this;
    }

    public TestListPagesRequestBuilder WithPathFilter(
        string? filter,
        ListPagesStringFilterType filterType = ListPagesStringFilterType.Contains)
    {
        this._pathFilter = filter;
        this._pathFilterType = filterType;
        return this;
    }

    public TestListPagesRequestBuilder WithPathContains(string filter)
    {
        this._pathFilter = filter;
        this._pathFilterType = ListPagesStringFilterType.Contains;
        return this;
    }

    public TestListPagesRequestBuilder WithPathStartsWith(string filter)
    {
        this._pathFilter = filter;
        this._pathFilterType = ListPagesStringFilterType.StartsWith;
        return this;
    }

    public TestListPagesRequestBuilder WithPathEndsWith(string filter)
    {
        this._pathFilter = filter;
        this._pathFilterType = ListPagesStringFilterType.EndsWith;
        return this;
    }

    public TestListPagesRequestBuilder WithPathRegex(string pattern)
    {
        this._pathFilter = pattern;
        this._pathFilterType = ListPagesStringFilterType.Regex;
        return this;
    }

    public TestListPagesRequestBuilder WithCreatedAfter(DateTimeOffset? createdAfter)
    {
        this._createdAfter = createdAfter;
        return this;
    }

    public TestListPagesRequestBuilder WithCreatedBefore(DateTimeOffset? createdBefore)
    {
        this._createdBefore = createdBefore;
        return this;
    }

    public TestListPagesRequestBuilder WithCreatedDateRange(DateTimeOffset after, DateTimeOffset before)
    {
        this._createdAfter = after;
        this._createdBefore = before;
        return this;
    }

    public TestListPagesRequestBuilder WithUpdatedAfter(DateTimeOffset? updatedAfter)
    {
        this._updatedAfter = updatedAfter;
        return this;
    }

    public TestListPagesRequestBuilder WithUpdatedBefore(DateTimeOffset? updatedBefore)
    {
        this._updatedBefore = updatedBefore;
        return this;
    }

    public TestListPagesRequestBuilder WithUpdatedDateRange(DateTimeOffset after, DateTimeOffset before)
    {
        this._updatedAfter = after;
        this._updatedBefore = before;
        return this;
    }

    public TestListPagesRequestBuilder WithPagination(int limit, int offset)
    {
        this._limit = limit;
        this._offset = offset;
        return this;
    }

    public TestListPagesRequestBuilder ForFirstPage(int pageSize = 20)
    {
        this._limit = pageSize;
        this._offset = 0;
        return this;
    }

    public TestListPagesRequestBuilder ForPage(int pageNumber, int pageSize = 20)
    {
        this._limit = pageSize;
        this._offset = (pageNumber - 1) * pageSize;
        return this;
    }

    public TestListPagesRequestBuilder SortedBySlug(bool ascending = true)
    {
        this._sort = ascending ? ListPagesSort.SlugAsc : ListPagesSort.SlugDesc;
        return this;
    }

    public TestListPagesRequestBuilder SortedByPath(bool ascending = true)
    {
        this._sort = ascending ? ListPagesSort.PathAsc : ListPagesSort.PathDesc;
        return this;
    }

    public TestListPagesRequestBuilder SortedByCreatedAt(bool ascending = true)
    {
        this._sort = ascending ? ListPagesSort.CreatedAtAsc : ListPagesSort.CreatedAtDesc;
        return this;
    }

    public TestListPagesRequestBuilder SortedByUpdatedAt(bool ascending = true)
    {
        this._sort = ascending ? ListPagesSort.UpdatedAtAsc : ListPagesSort.UpdatedAtDesc;
        return this;
    }

    public async Task<ListPagesResponse> ExecuteAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        var queryString = this.BuildQueryString();
        var response = await client.GetAsync($"/pages?{queryString}", cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ListPagesResponse>(cancellationToken);
        result.Should().NotBeNull();

        return result;
    }

    public async Task<HttpResponseMessage> ExecuteExpectingAsync(HttpClient client, HttpStatusCode expectedStatusCode, CancellationToken cancellationToken = default)
    {
        var queryString = this.BuildQueryString();
        var response = await client.GetAsync($"/pages?{queryString}", cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }

    public ListPagesRequest Build() => new(
        Limit: this._limit,
        Offset: this._offset,
        Sort: this._sort,
        SlugFilter: this._slugFilter,
        SlugFilterType: this._slugFilterType,
        PathFilter: this._pathFilter,
        PathFilterType: this._pathFilterType,
        CreatedAfter: this._createdAfter,
        CreatedBefore: this._createdBefore,
        UpdatedAfter: this._updatedAfter,
        UpdatedBefore: this._updatedBefore,
        Search: this._search);

    public string BuildQueryString()
    {
        var queryParams = new Dictionary<string, string?>
        {
            ["limit"] = this._limit.ToString(),
            ["offset"] = this._offset.ToString(),
            ["sort"] = this._sort.ToString(),
            ["search"] = this._search,
        };

        if (!string.IsNullOrEmpty(this._slugFilter))
        {
            queryParams["slugFilter"] = this._slugFilter;
            queryParams["slugFilterType"] = this._slugFilterType.ToString();
        }

        if (!string.IsNullOrEmpty(this._pathFilter))
        {
            queryParams["pathFilter"] = this._pathFilter;
            queryParams["pathFilterType"] = this._pathFilterType.ToString();
        }

        if (this._createdAfter.HasValue)
        {
            queryParams["createdAfter"] = this._createdAfter.Value.ToString("O");
        }

        if (this._createdBefore.HasValue)
        {
            queryParams["createdBefore"] = this._createdBefore.Value.ToString("O");
        }

        if (this._updatedAfter.HasValue)
        {
            queryParams["updatedAfter"] = this._updatedAfter.Value.ToString("O");
        }

        if (this._updatedBefore.HasValue)
        {
            queryParams["updatedBefore"] = this._updatedBefore.Value.ToString("O");
        }

        return QueryHelpers.AddQueryString(string.Empty, queryParams).TrimStart('?');
    }
}

public sealed record TestListPagesResult(
    ListPagesResponse Response,
    Guid ExpectedRealtorId)
{
    public int Total => this.Response.Total;

    public int Limit => this.Response.Limit;

    public int? NextOffset => this.Response.NextOffset;

    public IReadOnlyList<ListPagesResponseItem> Pages => this.Response.Pages;

    public void AssertPagination(int expectedTotal, int expectedLimit, int? expectedNextOffset = null)
    {
        this.Total.Should().Be(expectedTotal);
        this.Limit.Should().Be(expectedLimit);
        this.NextOffset.Should().Be(expectedNextOffset);
    }

    public void AssertPageCount(int expectedCount)
    {
        this.Pages.Should().HaveCount(expectedCount);
        this.Total.Should().Be(expectedCount);
    }

    public void AssertEmpty()
    {
        this.Total.Should().Be(0);
        this.Pages.Should().BeEmpty();
        this.NextOffset.Should().BeNull();
    }

    public void AssertContainsPage(Guid pageId) => this.Pages.Should().Contain(p => p.PageId == pageId);

    public void AssertContainsSlug(string slug) => this.Pages.Should().Contain(p => p.Slug == slug);

    public void AssertAllPagesHaveParent(Guid? expectedParentId) => this.Pages
        .Should()
        .AllSatisfy(p => p.ParentPageId.Should().Be(expectedParentId));

    public void AssertSortedBySlug(bool ascending = true)
    {
        var slugs = this.Pages.Select(p => p.Slug).ToList();
        var expectedOrder = ascending
            ? slugs.OrderBy(s => s).ToList()
            : [.. slugs.OrderByDescending(s => s)];
        slugs.Should().BeEquivalentTo(expectedOrder, options => options.WithStrictOrdering());
    }

    public void AssertSortedByPath(bool ascending = true)
    {
        var paths = this.Pages.Select(p => p.Path).ToList();
        var expectedOrder = ascending
            ? paths.OrderBy(p => p).ToList()
            : [.. paths.OrderByDescending(p => p)];
        paths.Should().BeEquivalentTo(expectedOrder, options => options.WithStrictOrdering());
    }

    public void AssertSortedByCreatedAt(bool ascending = true)
    {
        var dates = this.Pages.Select(p => p.CreatedAt).ToList();
        var expectedOrder = ascending
            ? dates.OrderBy(d => d).ToList()
            : [.. dates.OrderByDescending(d => d)];
        dates.Should().BeEquivalentTo(expectedOrder, options => options.WithStrictOrdering());
    }

    public void AssertSortedByUpdatedAt(bool ascending = true)
    {
        var dates = this.Pages.Select(p => p.UpdatedAt).ToList();
        var expectedOrder = ascending
            ? dates.OrderBy(d => d).ToList()
            : [.. dates.OrderByDescending(d => d)];
        dates.Should().BeEquivalentTo(expectedOrder, options => options.WithStrictOrdering());
    }

    public void AssertAllPagesValid() => this.Pages
        .Should()
        .AllSatisfy(page =>
            {
                page.PageId.Should().NotBe(Guid.Empty);
                page.CurrentPageVersionId.Should().NotBe(Guid.Empty);
                page.Slug.Should().NotBeNullOrEmpty();
                page.Path.Should().NotBeNullOrEmpty();
                page.Path.Should().StartWith("/");
                page.VersionCount.Should().BeGreaterThan(0);
                page.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
                page.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
            });
}
