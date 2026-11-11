using System.Net;
using System.Reflection;
using System.Text.Json;

using ConradSix.C4M;
using ConradSix.C4M.Testing;

using FastEndpoints;

using Microsoft.AspNetCore.TestHost;

using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("C4M_CONNECTION_STRING")
    ?? throw new InvalidOperationException("C4M_CONNECTION_STRING is required.");
var expectedVersion = Environment.GetEnvironmentVariable("C4M_PACKAGE_VERSION");
foreach (var assembly in new[] { typeof(C4MEndpoints).Assembly, typeof(C4MTestData).Assembly })
{
    Require(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] == expectedVersion,
        $"The installed {assembly.GetName().Name} assembly does not carry version {expectedVersion}.");
}

C4MTestPage page;
await using (var first = CreateHost())
{
    await first.MigrateC4MAsync();
    await first.StartAsync();
    var data = first.Services.GetRequiredService<C4MTestData>();
    await data.AddBlockTypeAsync("package-consumer");
    page = await data.AddPageAsync("package-consumer", published: true, withVersion: true);
    await CheckPageAsync(first);

    var assets = first.Services.GetRequiredService<FakeAssetResolver>();
    var image = assets.Add();
    var resolved = await first.Services.GetRequiredService<IAssetResolver>()
        .ResolveImagesAsync([image.Id], CancellationToken.None);
    Require(resolved.ContainsKey(image.Id), "The packaged asset resolver was not registered.");
    await first.StopAsync();
}

// A new host over the same database must find the original page without applying the baseline again.
await using (var second = CreateHost())
{
    await second.MigrateC4MAsync();
    await second.StartAsync();
    await CheckPageAsync(second);
    using var client = second.GetTestClient();
    using var unpublished = await client.PostAsJsonAsync($"/pages/{page.Id}/unpublish", new
    {
        expectedCurrentPageVersionId = page.CurrentVersionId,
        expectedIsPublished = true,
    });
    Require(unpublished.StatusCode == HttpStatusCode.NoContent, $"Unpublish failed: {await unpublished.Content.ReadAsStringAsync()}");
    Require(second.Services.GetRequiredService<RecordingChangeHandler>().Changes.Count > 0, "The change handler received no notification.");
    using var hidden = await client.GetAsync("/pages/by-path?path=/package-consumer");
    Require(hidden.StatusCode == HttpStatusCode.NotFound, "An unpublished page was delivered.");

    await second.Services.GetRequiredService<C4MTestData>().ResetAsync();
    using var pages = await client.GetAsync("/pages");
    pages.EnsureSuccessStatusCode();
    using var document = JsonDocument.Parse(await pages.Content.ReadAsStringAsync());
    Require(document.RootElement.GetProperty("total").GetInt32() == 0, "The testing package did not clear the engine's tables.");
    await second.StopAsync();
}

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
await using var history = new NpgsqlCommand(
    """
    SELECT string_agg(migration_id, ',' ORDER BY migration_id) FROM cms."__EFMigrationsHistory"
    """, connection);
Require((string?)await history.ExecuteScalarAsync() == "20261029180539_InitialCreate", "The packaged migration history changed.");
Console.WriteLine($"Installed C4M packages passed on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}.");

WebApplication CreateHost()
{
    var builder = WebApplication.CreateSlimBuilder();
    builder.WebHost.UseTestServer();
    builder.Logging.SetMinimumLevel(LogLevel.Critical);
    builder.Services
        .AddC4M(c4m => c4m.UsePostgres(_ => connectionString).UseFakeAssetResolver().UseRecordingChangeHandler())
        .AddC4MTesting()
        .AddAuthorization()
        .AddFastEndpoints(options =>
        {
            options.DisableAutoDiscovery = true;
            options.Assemblies = [C4MEndpoints.Assembly];
        });
    var app = builder.Build();
    app.UseAuthorization().UseFastEndpoints(options => options.Endpoints.Configurator = endpoint => endpoint.ConfigureCmsEndpoint(
        editor: definition => definition.AllowAnonymous(),
        delivery: definition => definition.AllowAnonymous()));
    return app;
}

async Task CheckPageAsync(WebApplication app)
{
    using var client = app.GetTestClient();
    using var response = await client.GetAsync("/pages/by-path?path=/package-consumer");
    response.EnsureSuccessStatusCode();
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Require(document.RootElement.GetProperty("pageId").GetGuid() == page.Id, "The installed engine did not return the seeded page.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
