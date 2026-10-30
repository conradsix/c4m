namespace Parapet.CMS.Engine.Data;

using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using Npgsql;

internal static partial class CmsDbUpdateExceptionExtensions
{
    // Raised by the page hierarchy triggers.
    private const string CycleDetected = "P0002";

    public static bool IsUniqueConstraintViolation(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public static string? GetUniqueConstraintName(this DbUpdateException ex) => ConstraintName(ex);

    public static bool IsForeignKeyConstraintViolation(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };

    public static string? GetForeignKeyConstraintName(this DbUpdateException ex) => ConstraintName(ex);

    public static bool IsCycleDetected(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: CycleDetected };

    private static string? ConstraintName(DbUpdateException ex) =>
        ex.InnerException is PostgresException postgres && ConstraintRegex().Match(postgres.Message) is { Success: true } match
            ? match.Groups[1].Value
            : null;

    [GeneratedRegex(@"constraint ""([^""]+)""")]
    private static partial Regex ConstraintRegex();
}
