namespace ConradSix.C4M;

using ConradSix.C4M.Data;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class C4MBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    // The engine opens its own connections to this database and keeps its tables in the cms schema.
    public C4MBuilder UsePostgres(Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        this.Services.Replace(ServiceDescriptor.Singleton(provider => new CmsEngineDataSource(connectionString(provider))));
        return this;
    }

    public C4MBuilder ConfigureOptions(Action<C4MOptions> configure)
    {
        this.Services.Configure(configure);
        return this;
    }

    public C4MBuilder UseChangeHandler<T>()
        where T : class, ICmsChangeHandler
    {
        this.Services.Replace(ServiceDescriptor.Singleton<ICmsChangeHandler, T>());
        return this;
    }

    public C4MBuilder UseAssetResolver<T>()
        where T : class, IAssetResolver
    {
        this.Services.Replace(ServiceDescriptor.Scoped<IAssetResolver, T>());
        return this;
    }

    public C4MBuilder UseBlockChangeDescriber<T>()
        where T : class, IBlockChangeDescriber
    {
        this.Services.Replace(ServiceDescriptor.Singleton<IBlockChangeDescriber, T>());
        return this;
    }

    public C4MBuilder UseLinkedPageImageSelector<T>()
        where T : class, ILinkedPageImageSelector
    {
        this.Services.Replace(ServiceDescriptor.Singleton<ILinkedPageImageSelector, T>());
        return this;
    }

    public C4MBuilder AddBlockValidator<T>()
        where T : class, IBlockValidator
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IBlockValidator, T>());
        return this;
    }

    public C4MBuilder AddReferenceExtractor<T>()
        where T : class, IReferenceExtractor
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IReferenceExtractor, T>());
        return this;
    }

    public C4MBuilder AddPublicationRule<T>()
        where T : class, IPublicationRule
    {
        this.Services.TryAddEnumerable(ServiceDescriptor.Scoped<IPublicationRule, T>());
        return this;
    }
}
