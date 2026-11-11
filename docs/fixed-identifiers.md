# Fixed identifiers

Hosts' databases already hold data under these names, so they never change. Renaming one would orphan a host's data or
make the engine apply its migrations a second time.

| What                     | Value                                                                         |
| ------------------------ | ----------------------------------------------------------------------------- |
| Schema                   | `cms`                                                                         |
| Migration history table  | `cms."__EFMigrationsHistory"`, with snake_case columns                        |
| Baseline migration       | `20261029180539_InitialCreate`                                                |
| Naming convention        | snake_case for every table, column, key, index and constraint                |
| Advisory lock namespaces | `0x434D5301` (publication, paths and menus) and `0x434D5302` (a page's versions) |
| Log event ids            | 11000–11999                                                                   |

- **The baseline migration is never squashed or regenerated.** Hosts that adopted the engine's tables recorded it as
  applied instead of running it, so its id is what tells the engine those tables exist.
- **The lock namespaces** are the first key of Postgres's two-key `pg_advisory_xact_lock`. Hosts never take them, and a
  host's single-key locks can't collide with them.
- **The naming convention** comes from `EFCore.NamingConventions`, which is why the engine depends on it.
