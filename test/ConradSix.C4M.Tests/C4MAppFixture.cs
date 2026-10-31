#pragma warning disable CA1711 // xunit names collection definitions Collection.
#pragma warning disable SA1402 // The collection is kept with its fixture.
namespace ConradSix.C4M.Tests;

using ConradSix.C4M.Testing;

using FastEndpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Npgsql;

using Testcontainers.PostgreSql;

// The engine on its own: a slim web host with only the engine's endpoints, over a database built by the engine's
// migrations alone, as a new host would have it. Hooks are the Testing package's fakes.
public sealed class C4MAppFixture : IAsyncLifetime
{
    // Root segments the engine refuses for pages, standing in for a host's own routes.
    public static readonly IReadOnlyList<string> ReservedRootSegments = ["api", "reserved"];

    // Block types the engine refuses to save.
    public static readonly IReadOnlyList<string> RetiredBlockTypes = ["retired-block", "retired-gallery"];

    private PostgreSqlContainer? _database;

    private WebApplication? _app;

    public IServiceProvider Services => this.App.Services;

    public FakeAssetResolver Assets => this.Services.GetRequiredService<FakeAssetResolver>();

    public RecordingChangeHandler Changes => this.Services.GetRequiredService<RecordingChangeHandler>();

    public C4MTestData Data => this.Services.GetRequiredService<C4MTestData>();

    public string ConnectionString { get; private set; } = string.Empty;

    private WebApplication App => this._app ?? throw new InvalidOperationException("The fixture hasn't started.");

    public HttpClient CreateClient() => this.App.GetTestClient();

    public async ValueTask InitializeAsync()
    {
        this._database = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("c4m")
            .WithTmpfsMount("/var/lib/postgresql")
            .Build();
        await this._database.StartAsync();
        this.ConnectionString = new NpgsqlConnectionStringBuilder(this._database.GetConnectionString())
        {
            MaxPoolSize = 10,
            IncludeErrorDetail = true,
        }.ConnectionString;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services
            .AddC4M(c4m => c4m
                .UsePostgres(_ => this.ConnectionString)
                .UseFakeAssetResolver()
                .UseRecordingChangeHandler()
                .UseLinkedPageImageSelector<FirstImageSelector>()
                .ConfigureOptions(options =>
                {
                    options.ReservedRootSegments.UnionWith(ReservedRootSegments);
                    options.RetiredBlockTypes.UnionWith(RetiredBlockTypes);
                }))
            .AddC4MTesting()
            .AddAuthorization()
            .AddFastEndpoints(endpoints =>
            {
                endpoints.DisableAutoDiscovery = true;
                endpoints.Assemblies = [C4MEndpoints.Assembly];
            });

        this._app = builder.Build();
        this._app
            .UseAuthorization()
            .UseFastEndpoints(endpoints => endpoints.Endpoints.Configurator = endpoint => endpoint.AllowAnonymous());
        await this._app.MigrateC4MAsync();
        await this._app.StartAsync();
    }

    // Clears the engine's tables and the fakes between tests.
    public async ValueTask TearDownTestAsync()
    {
        this.Assets.Clear();
        this.Changes.Clear();
        await this.Data.ResetAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (this._app is not null)
        {
            await this._app.DisposeAsync();
        }

        if (this._database is not null)
        {
            await this._database.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class C4MAppCollection : ICollectionFixture<C4MAppFixture>
{
    public const string Name = nameof(C4MAppCollection);
}
