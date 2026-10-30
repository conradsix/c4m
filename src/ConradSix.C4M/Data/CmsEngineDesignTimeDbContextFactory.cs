namespace ConradSix.C4M.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// For the EF tools. Adding a migration needs no database; commands that do read CMS_ENGINE_CONNECTION_STRING.
internal sealed class CmsEngineDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CmsEngineDbContext>
{
    public CmsEngineDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CMS_ENGINE_CONNECTION_STRING") ?? "Host=localhost;Database=cms_engine";
        var options = new DbContextOptionsBuilder<CmsEngineDbContext>();
        CmsEngineDbContext.Configure(options, connectionString);
        return new CmsEngineDbContext(options.Options);
    }
}
