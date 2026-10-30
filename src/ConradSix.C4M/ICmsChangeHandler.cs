#pragma warning disable SA1402 // The default handler is kept with its contract.
namespace Parapet.CMS.Engine;

// Called after a committed edit. Implementations must not throw: the edit has already been saved.
public interface ICmsChangeHandler
{
    Task PageChangedAsync(string path);

    // Changes that can alter any page, such as paths, publication state or link targets.
    Task ContentChangedAsync();

    Task NavigationChangedAsync();
}

public sealed class NullCmsChangeHandler : ICmsChangeHandler
{
    public Task PageChangedAsync(string path) => Task.CompletedTask;

    public Task ContentChangedAsync() => Task.CompletedTask;

    public Task NavigationChangedAsync() => Task.CompletedTask;
}
