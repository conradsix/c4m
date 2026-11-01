namespace ConradSix.C4M.Tests.Data;

using AwesomeAssertions;

using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class EntityNavigationTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task Navigations_ShouldLazyLoadReferencesAndCollections()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var page = new PageEntity { Slug = "lazy-page" };
        var version = new PageVersionEntity { Page = page };

        context.PageVersions.Add(version);
        await context.SaveChangesAsync(cancellationToken);

        var pageId = page.Id;
        var versionId = version.Id;
        context.ChangeTracker.Clear();

        var loadedVersion = await context.PageVersions
            .SingleAsync(v => v.Id == versionId, cancellationToken);

        context.Entry(loadedVersion).Reference(v => v.Page).IsLoaded.Should().BeFalse();
        loadedVersion.Page.Id.Should().Be(pageId);
        context.Entry(loadedVersion).Reference(v => v.Page).IsLoaded.Should().BeTrue();

        context.ChangeTracker.Clear();

        var loadedPage = await context.Pages
            .SingleAsync(p => p.Id == pageId, cancellationToken);

        context.Entry(loadedPage).Collection(p => p.Versions).IsLoaded.Should().BeFalse();
        loadedPage.Versions.Should().ContainSingle(v => v.Id == versionId);
        context.Entry(loadedPage).Collection(p => p.Versions).IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task Navigations_ShouldFixUpEagerlyLoadedRelationships()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var page = new PageEntity { Slug = "eager-page" };
        var version = new PageVersionEntity { Page = page };

        context.PageVersions.Add(version);
        await context.SaveChangesAsync(cancellationToken);

        var pageId = page.Id;
        context.ChangeTracker.Clear();

        var loadedPage = await context.Pages
            .Include(p => p.Versions)
            .SingleAsync(p => p.Id == pageId, cancellationToken);

        context.Entry(loadedPage).Collection(p => p.Versions).IsLoaded.Should().BeTrue();
        var loadedVersion = loadedPage.Versions.Should().ContainSingle().Subject;
        loadedVersion.Page.Should().BeSameAs(loadedPage);
    }

    [Fact]
    public async Task NavigationSetter_ShouldPersistRelationshipChanges()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        var firstPage = new PageEntity { Slug = "first-page" };
        var secondPage = new PageEntity { Slug = "second-page" };
        var version = new PageVersionEntity { Page = firstPage };

        context.AddRange(firstPage, secondPage, version);
        await context.SaveChangesAsync(cancellationToken);

        version.Page = secondPage;
        await context.SaveChangesAsync(cancellationToken);

        var versionId = version.Id;
        var secondPageId = secondPage.Id;
        context.ChangeTracker.Clear();

        var persistedVersion = await context.PageVersions
            .SingleAsync(v => v.Id == versionId, cancellationToken);

        persistedVersion.PageId.Should().Be(secondPageId);
        persistedVersion.Page.Id.Should().Be(secondPageId);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
