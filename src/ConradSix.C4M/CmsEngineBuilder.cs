namespace Parapet.CMS.Engine;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Parapet.CMS.Engine.Data;

public sealed class CmsEngineBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    // The engine opens its own connections to this database and keeps its tables in the cms schema.
    public CmsEngineBuilder UsePostgres(Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        this.Services.Replace(ServiceDescriptor.Singleton(provider => new CmsEngineDataSource(connectionString(provider))));
        return this;
    }

    public CmsEngineBuilder ConfigureOptions(Action<CmsEngineOptions> configure)
    {
        this.Services.Configure(configure);
        return this;
    }

    public CmsEngineBuilder UseChangeHandler<T>()
        where T : class, ICmsChangeHandler
    {
        this.Services.Replace(ServiceDescriptor.Singleton<ICmsChangeHandler, T>());
        return this;
    }

    public CmsEngineBuilder UseAssetResolver<T>()
        where T : class, IAssetResolver
    {
        this.Services.Replace(ServiceDescriptor.Scoped<IAssetResolver, T>());
        return this;
    }

    public CmsEngineBuilder UseBlockChangeDescriber<T>()
        where T : class, IBlockChangeDescriber
    {
        this.Services.Replace(ServiceDescriptor.Singleton<IBlockChangeDescriber, T>());
        return this;
    }

    public CmsEngineBuilder UseLinkedPageImageSelector<T>()
        where T : class, ILinkedPageImageSelector
    {
        this.Services.Replace(ServiceDescriptor.Singleton<ILinkedPageImageSelector, T>());
        return this;
    }

    public CmsEngineBuilder AddBlockValidator<T>()
        where T : class, IBlockValidator
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IBlockValidator, T>());
        return this;
    }

    public CmsEngineBuilder AddReferenceExtractor<T>()
        where T : class, IReferenceExtractor
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IReferenceExtractor, T>());
        return this;
    }

    public CmsEngineBuilder AddPublicationRule<T>()
        where T : class, IPublicationRule
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IPublicationRule, T>());
        return this;
    }
}
