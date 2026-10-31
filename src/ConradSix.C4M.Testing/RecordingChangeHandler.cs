namespace ConradSix.C4M.Testing;

using System.Collections.Concurrent;

// A change handler that records the changes the engine reports, in order.
public sealed class RecordingChangeHandler : ICmsChangeHandler
{
    private readonly ConcurrentQueue<CmsChange> _changes = new();

    public IReadOnlyList<CmsChange> Changes => [.. this._changes];

    public void Clear() => this._changes.Clear();

    public Task PageChangedAsync(string path) => this.RecordAsync(CmsChange.Page(path));

    public Task ContentChangedAsync() => this.RecordAsync(CmsChange.Content);

    public Task NavigationChangedAsync() => this.RecordAsync(CmsChange.Navigation);

    private Task RecordAsync(CmsChange change)
    {
        this._changes.Enqueue(change);
        return Task.CompletedTask;
    }
}
