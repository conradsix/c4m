#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Services;
using ConradSix.C4M.Validation;

using Microsoft.Extensions.DependencyInjection.Extensions;

public static class C4MServiceCollectionExtensions
{
    // The host must supply a database with UsePostgres. The other hooks have defaults.
    public static IServiceCollection AddC4M(this IServiceCollection services, Action<C4MBuilder>? configure = null)
    {
        services.AddOptions<C4MOptions>();
        services.AddDbContext<CmsEngineDbContext>((provider, options) => CmsEngineDbContext.Configure(
            options,
            (provider.GetService<CmsEngineDataSource>()
                ?? throw new InvalidOperationException("The CMS engine has no database. Call UsePostgres when adding it.")).DataSource));
        services.TryAddSingleton<CmsPathPolicy>();
        services.TryAddScoped<IAssetResolver, NullAssetResolver>();
        services.TryAddSingleton<ICmsChangeHandler, NullCmsChangeHandler>();
        services.TryAddSingleton<IBlockChangeDescriber, BlockChangeDescriber>();
        services.TryAddSingleton<ILinkedPageImageSelector, NullLinkedPageImageSelector>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReferenceExtractor, TypedReferenceExtractor>());
        services.TryAddSingleton<BlockReferenceIndexer>();
        services.TryAddSingleton<CmsJsonSchemaCache>();
        services.TryAddScoped<IBlockReferenceQuery, BlockReferenceQuery>();
        services.TryAddScoped<IBlockReferenceReindexer, BlockReferenceReindexer>();
        services.TryAddScoped<PageVersionChangeSummaryService>();
        services.TryAddScoped<PageVersionServices>();
        services.TryAddScoped<IPageVersionServices>(provider => provider.GetRequiredService<PageVersionServices>());
        services.TryAddScoped<IPageService, PageService>();
        services.TryAddScoped<ICmsPageReader, CmsPageReader>();
        services.TryAddScoped<IPageQueryService, PageQueryService>();
        services.TryAddScoped<IMenuService, MenuService>();

        configure?.Invoke(new C4MBuilder(services));
        return services;
    }
}
