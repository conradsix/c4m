namespace Parapet.CMS.Engine.Data.Migrations;

using Microsoft.EntityFrameworkCore.Migrations;

internal static class CmsEngineMigrationBuilderExtensions
{
    // Runs one of the SQL files embedded from Data/Migrations/Sql.
    public static MigrationBuilder ExecuteSqlResource(this MigrationBuilder migrationBuilder, string fileName)
    {
        var name = $"Parapet.CMS.Engine.Sql.{fileName}";
        using var stream = typeof(CmsEngineMigrationBuilderExtensions).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The CMS engine has no SQL resource named {name}.");
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
        return migrationBuilder;
    }
}
