namespace ConradSix.C4M.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// For the EF tools. Adding a migration needs no database; commands that do read C4M_CONNECTION_STRING, or
// use the repo's docker-compose database.
internal sealed class CmsEngineDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CmsEngineDbContext>
{
    public CmsEngineDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("C4M_CONNECTION_STRING")
            ?? "Host=localhost;Port=5433;Database=c4m;Username=c4m;Password=c4m";
        var options = new DbContextOptionsBuilder<CmsEngineDbContext>();
        CmsEngineDbContext.Configure(options, connectionString);
        return new CmsEngineDbContext(options.Options);
    }
}
