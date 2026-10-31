#pragma warning disable IDE0130 // Namespace does not match folder structure
#pragma warning disable RS0026 // The overloads extend different types, so a call can't be ambiguous.
namespace Microsoft.Extensions.Hosting;

using ConradSix.C4M.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public static class C4MHostExtensions
{
    // Brings the engine's schema up to date. Hosts run it at startup, after their own migrations.
    public static Task MigrateC4MAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.Services.MigrateC4MAsync(cancellationToken);
    }

    // The same, for a container built without a host: a tool, or a test's scratch database.
    public static async Task MigrateC4MAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        ctx.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        var pending = (await ctx.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        (scope.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance)
            .CreateLogger(typeof(C4MHostExtensions)).CmsEngineMigrating(pending);
        await ctx.Database.MigrateAsync(cancellationToken);
    }
}
