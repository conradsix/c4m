namespace ConradSix.C4M.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class C4MTestingBuilderExtensions
{
    // Resolves images from a FakeAssetResolver singleton, which tests add images to.
    public static C4MBuilder UseFakeAssetResolver(this C4MBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<FakeAssetResolver>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IAssetResolver>(provider => provider.GetRequiredService<FakeAssetResolver>()));
        return builder;
    }

    // Reports changes to a RecordingChangeHandler singleton, which tests read them from.
    public static C4MBuilder UseRecordingChangeHandler(this C4MBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton<RecordingChangeHandler>();
        builder.Services.Replace(ServiceDescriptor.Singleton<ICmsChangeHandler>(provider => provider.GetRequiredService<RecordingChangeHandler>()));
        return builder;
    }
}
