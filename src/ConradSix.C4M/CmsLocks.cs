namespace Parapet.CMS.Engine;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

// The CMS's advisory locks. They use Postgres's two-key form under the CMS's own namespaces, a key space a host's
// single-key locks can never collide with. Hosts never take them.
internal static class CmsLocks
{
    public const int PublicationNamespace = 0x434D5301;

    public const int PageNamespace = 0x434D5302;

    // Serialises publication state, paths and menus. Take it inside a read-committed transaction so validation sees
    // commits made while waiting.
    public static Task PublicationAsync(DatabaseFacade database, CancellationToken ct) =>
        database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({PublicationNamespace}, 0)", ct);

    // Serialises a page's version changes.
    public static Task PageAsync(DatabaseFacade database, Guid pageId, CancellationToken ct) =>
        database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({PageNamespace}, hashtext({pageId.ToString()}))", ct);
}
