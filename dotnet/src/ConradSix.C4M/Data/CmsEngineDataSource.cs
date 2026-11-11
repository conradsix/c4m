namespace ConradSix.C4M.Data;

using Npgsql;

// The engine's own connections. They never enlist in a caller's ambient transaction: each engine operation commits on
// its own.
internal sealed class CmsEngineDataSource(string connectionString) : IAsyncDisposable, IDisposable
{
    public NpgsqlDataSource DataSource { get; } = NpgsqlDataSource.Create(
        new NpgsqlConnectionStringBuilder(connectionString) { Enlist = false });

    public ValueTask DisposeAsync() => this.DataSource.DisposeAsync();

    public void Dispose() => this.DataSource.Dispose();
}
