#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;

using ConradSix.C4M.Testing;

using Microsoft.Extensions.DependencyInjection.Extensions;

public static class C4MTestingServiceCollectionExtensions
{
    // Adds C4MTestData, which seeds and clears the engine's tables.
    public static IServiceCollection AddC4MTesting(this IServiceCollection services)
    {
        services.TryAddSingleton<C4MTestData>();
        return services;
    }
}
