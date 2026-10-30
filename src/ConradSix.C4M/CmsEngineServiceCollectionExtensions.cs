#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Services;

public static class CmsEngineServiceCollectionExtensions
{
    // The host must supply a database with UsePostgres and an asset resolver with UseAssetResolver.
    public static IServiceCollection AddCmsEngine(this IServiceCollection services, Action<CmsEngineBuilder>? configure = null)
    {
        services.AddOptions<CmsEngineOptions>();
        services.AddDbContext<CmsEngineDbContext>((provider, options) => CmsEngineDbContext.Configure(
            options,
            (provider.GetService<CmsEngineDataSource>()
                ?? throw new InvalidOperationException("The CMS engine has no database. Call UsePostgres when adding it.")).DataSource));
        services.TryAddSingleton<CmsPathPolicy>();
        services.TryAddSingleton<ICmsChangeHandler, NullCmsChangeHandler>();
        services.TryAddSingleton<IBlockChangeDescriber, BlockChangeDescriber>();
        services.TryAddSingleton<ILinkedPageImageSelector, NullLinkedPageImageSelector>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReferenceExtractor, TypedReferenceExtractor>());
        services.TryAddSingleton<BlockReferenceIndexer>();
        services.TryAddScoped<IBlockReferenceQuery, BlockReferenceQuery>();
        services.TryAddScoped<IBlockReferenceReindexer, BlockReferenceReindexer>();
        services.TryAddScoped<PageVersionChangeSummaryService>();
        services.TryAddScoped<PageVersionServices>();
        services.TryAddScoped<IPageVersionServices>(provider => provider.GetRequiredService<PageVersionServices>());
        services.TryAddScoped<IPageService, PageService>();
        services.TryAddScoped<ICmsPageReader, CmsPageReader>();
        services.TryAddScoped<IPageQueryService, PageQueryService>();
        services.TryAddScoped<IMenuService, MenuService>();

        configure?.Invoke(new CmsEngineBuilder(services));
        return services;
    }
}
