# Repository Guidelines

## Project Structure

C4M is a CMS engine shipped as NuGet and npm packages. The .NET solution is `ConradSix.C4M.slnx`; the npm workspace is
`typescript/`.

- `src/ConradSix.C4M/` is the engine: services and hooks at the root and in `Services/`, the internal
  `CmsEngineDbContext` and its migrations in `Data/`, FastEndpoints endpoints in `Endpoints/`, and the JSON Schema
  validator in `Validation/`.
- `src/ConradSix.C4M.Testing/` has fakes for hosts' tests. The engine gives it `InternalsVisibleTo`.
- `test/ConradSix.C4M.Tests/` tests the engine on a slim `TestServer` host, over a PostgreSQL database built by the
  engine's migrations alone. Testcontainers starts the database, so the tests need Docker.
- `samples/ConradSix.C4M.SampleHost/` is the smallest host. `@conradsix/c4m-client` is generated from its OpenAPI
  document, and the EF tools use it as the startup project.
- `typescript/packages/` has `c4m-schema` (zod block schemas and their sync), `c4m-client` (generated) and
  `c4m-preview` (the preview protocol and tokens).
- `docs/` has the fixed identifiers and how to add a migration.

## Commands

From the repository root:

```bash
dotnet build ConradSix.C4M.slnx --configuration Release
dotnet test --solution ConradSix.C4M.slnx --configuration Release
dotnet format ConradSix.C4M.slnx --verify-no-changes --verbosity minimal
```

Release builds treat warnings as errors. `C4M_POSTGRES_IMAGE` picks the tests' PostgreSQL image (default
`postgres:18`); CI runs 14, 16 and 18.

From `typescript/` (Node 26.10.0 and Yarn 4, as `.nvmrc` and `package.json` pin):

```bash
yarn install --immutable
yarn check:all        # build, lint, cycles, format, types and tests
yarn fix:lint && yarn fix:format
```

The packages depend on each other's `dist`, so build before type-checking or testing one alone.

- Regenerate the client after changing an endpoint: `yarn workspace @conradsix/c4m-client run generate`. CI fails when
  the committed client differs from what the sample host produces.
- After changing zod or the parity cases, rewrite the validator's fixture: `UPDATE_ZOD_PARITY=1 yarn workspace
  @conradsix/c4m-schema run test`, then run the .NET tests.
- Add a migration with `./migration.sh add -n Name`; see [docs/migrations.md](docs/migrations.md).

## Rules

- **Never change the identifiers in [docs/fixed-identifiers.md](docs/fixed-identifiers.md)**: the `cms` schema, its
  history table, the baseline migration, the advisory lock keys and the log event range.
- **Public API is recorded** in each package's `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, and the build
  fails on a change that isn't. Add new API to `PublicAPI.Unshipped.txt` deliberately, and treat removals and signature
  changes as breaking. A release moves the unshipped entries to shipped.
- **Engine operations are the atomicity boundary.** Each commits in its own transaction; never share a transaction with
  a host or add a way to.
- **The validator's dialect is closed.** A schema keyword the validator doesn't implement fails registration; don't
  skip one silently.
- **Hosts are third parties.** Nothing in the engine names a particular host or site; host behaviour goes behind a hook.
- The npm packages are ESM only, with zod 4 as a peer. Relative imports carry `.js` extensions.
- The packages depend on the lowest dependency versions they support (`Directory.Packages.props`); the tests and the
  sample run on current ones.

## Style

Follow `.editorconfig`: 4-space C#, file-scoped namespaces, `using` directives inside the namespace, `this.`
qualification, and StyleCop's ordering. DI extension methods live in `Microsoft.Extensions.DependencyInjection` or
`Microsoft.Extensions.Hosting` and suppress `IDE0130`.

Test classes are named by feature, and test methods `Behaviour_Variant_Expectation`, without an `Async` suffix.

TypeScript uses arrow functions, `type` over `interface`, and explicit return types, as `typescript/.oxlintrc.json`
enforces.

## Commits and Releases

Commits use Conventional Commit prefixes with an optional scope: `feat:`, `fix(validation):`, `refactor:`, `test:`,
`docs:`, `chore:`.

A `vX.Y.Z` or `vX.Y.Z-preview.N` tag releases every package at that version: `build.yml` tests, packs and publishes to
nuget.org and npm through trusted publishing, then creates the GitHub release. Prereleases go to npm's `next` tag.
`./pack-local.sh` packs everything locally for a host to try first.

Do not commit `bin/`, `obj/`, `TestResults/`, `node_modules/` or `dist/`.
