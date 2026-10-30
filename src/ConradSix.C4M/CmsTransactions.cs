namespace Parapet.CMS.Engine;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

// Each CMS operation is atomic on its own: it commits in its own transaction and never joins a caller's.
internal static class CmsTransactions
{
    public static void EnsureNoCallerTransaction(DatabaseFacade database)
    {
        // The engine's connections never enlist in an ambient transaction, so a caller's TransactionScope would not
        // cover what it writes.
        if (database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null)
        {
            throw new InvalidOperationException("CMS operations commit in their own transaction. Call them outside yours.");
        }
    }

    // After a failed operation, forgets what it added or changed so a later save in the same scope doesn't retry it.
    public static void ForgetUnsaved(ChangeTracker tracker)
    {
        foreach (var entry in tracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            entry.State = EntityState.Detached;
        }
    }
}
