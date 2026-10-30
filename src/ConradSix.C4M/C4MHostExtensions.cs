#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Hosting;

using ConradSix.C4M.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class C4MHostExtensions
{
    // Brings the engine's schema up to date. Hosts run it at startup, after their own migrations.
    public static async Task MigrateC4MAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        await using var scope = host.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        ctx.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        var pending = (await ctx.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(C4MHostExtensions)).CmsEngineMigrating(pending);
        await ctx.Database.MigrateAsync(cancellationToken);
    }
}
