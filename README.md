# C4M

**_C4M_** (Contempt 4 Management) is a headless CMS engine for ASP.NET Core. It keeps pages, their versions, typed
blocks and menus in PostgreSQL, and serves them through FastEndpoints. Block types are zod schemas on the TypeScript
side, and the engine checks every block it saves against them.

C4M is a library, not an application. Your service hosts it beside its own endpoints, your editor calls it through a
typed client, and your site reads published pages from it.

It came out of a property site's CMS, which still runs on it. Treat the 0.x previews as young: the API can change
between them.

## Packages

| Package                  | Registry                                         | Use it for                                                                             |
| ------------------------ | ------------------------------------------------ | -------------------------------------------------------------------------------------- |
| `ConradSix.C4M`          | [![NuGet][nuget-c4m-badge]][nuget-c4m]           | The engine: services, PostgreSQL storage and migrations, and its FastEndpoints endpoints. |
| `ConradSix.C4M.Testing`  | [![NuGet][nuget-testing-badge]][nuget-testing]   | Fakes for the engine's hooks, and seeding and clearing its tables in your tests.        |
| `@conradsix/c4m-schema`  | [![npm][npm-schema-badge]][npm-schema]           | Versioned block and menu action schemas with zod, and syncing them to the engine.      |
| `@conradsix/c4m-client`  | [![npm][npm-client-badge]][npm-client]           | A typed fetch client for the engine's endpoints.                                       |
| `@conradsix/c4m-preview` | [![npm][npm-preview-badge]][npm-preview]         | Live preview: the editor-to-site postMessage protocol and signed preview tokens.       |

[nuget-c4m-badge]: https://img.shields.io/nuget/vpre/ConradSix.C4M
[nuget-c4m]: https://www.nuget.org/packages/ConradSix.C4M/
[nuget-testing-badge]: https://img.shields.io/nuget/vpre/ConradSix.C4M.Testing
[nuget-testing]: https://www.nuget.org/packages/ConradSix.C4M.Testing/
[npm-schema-badge]: https://img.shields.io/npm/v/@conradsix/c4m-schema/next
[npm-schema]: https://www.npmjs.com/package/@conradsix/c4m-schema
[npm-client-badge]: https://img.shields.io/npm/v/@conradsix/c4m-client/next
[npm-client]: https://www.npmjs.com/package/@conradsix/c4m-client
[npm-preview-badge]: https://img.shields.io/npm/v/@conradsix/c4m-preview/next
[npm-preview]: https://www.npmjs.com/package/@conradsix/c4m-preview

The packages are released together, at one version. The npm packages are ESM only, and need zod 4 as a peer.

## Install

```bash
dotnet add package ConradSix.C4M
dotnet add package ConradSix.C4M.Testing   # in test projects
```

```bash
npm install @conradsix/c4m-schema @conradsix/c4m-client @conradsix/c4m-preview zod
```

Previews are published under npm's `next` tag; add `@next` to install one.

## Host the engine

C4M needs .NET 10, FastEndpoints 8 and PostgreSQL 14 or later. It keeps its tables in the `cms` schema of the database
you give it, so it can share a database with your service.

```csharp
using ConradSix.C4M;
using FastEndpoints;

builder.Services
    .AddC4M(c4m => c4m
        .UsePostgres(provider => provider.GetRequiredService<IConfiguration>().GetConnectionString("C4M")!)
        .ConfigureOptions(options => options.ReservedRootSegments.UnionWith(["api", "search"])))
    .AddAuthorization()
    .AddFastEndpoints(options => options.Assemblies = [typeof(Program).Assembly, C4MEndpoints.Assembly]);

var app = builder.Build();

app.UseAuthorization().UseFastEndpoints(config =>
{
    config.Endpoints.Configurator = endpoint => endpoint.ConfigureCmsEndpoint(
        editor: e => e.Policies("CmsEditor"),
        delivery: e => e.AllowAnonymous());
});

// Your own migrations first, then the engine's.
await app.MigrateC4MAsync();
await app.RunAsync();
```

- `UsePostgres` is the only required call. The engine opens its own connections and never joins your transactions.
- `C4MEndpoints.Assembly` adds the engine's endpoints to your FastEndpoints host.
- `ConfigureCmsEndpoint` lets you authorize the editor's endpoints apart from the ones a public site reads. It returns
  false for endpoints that aren't the engine's, so you can configure your own in the same configurator.
- `MigrateC4MAsync` brings the `cms` schema up to date.
- `ReservedRootSegments` are top-level paths the engine refuses for pages, because your site routes them itself.

[`samples/ConradSix.C4M.SampleHost`](samples/ConradSix.C4M.SampleHost) is the smallest working host.

### Endpoints

| Area               | Routes                                                                                                   | Kind     |
| ------------------ | -------------------------------------------------------------------------------------------------------- | -------- |
| Pages              | `/pages`, `/pages/{id}`, `/pages/{id}/details`, `/pages/{id}/path`, `/pages/path-availability`           | editor   |
| Page versions      | `/pages/by-version/{id}`, `/pages/{id}/history`                                                          | editor   |
| Page lifecycle     | `/pages/{id}/publish`, `/pages/{id}/unpublish`, `/pages/{id}/archive`, `/pages/{id}/reparent-children`   | editor   |
| Block types        | `/block-types`, `/block-types/{id}`, `/block-types/by-name/{name}`                                       | editor   |
| Menu action types  | `/menu-action-types`, `/menu-action-types/{id}`, `/menu-action-types/by-name/{name}`                     | editor   |
| Menus              | `/menus`, `/menus/{id}`, `/navigation-settings`                                                          | editor   |
| Published content  | `/pages/by-path`, `/menus/default`, `/menus/footer`, `GET /menus/{id}`                                   | delivery |

### Hooks

Everything but the database has a default. Replace or add hooks on the builder:

| Hook                        | Builder method                   | Default                       | What it does                                                         |
| --------------------------- | -------------------------------- | ----------------------------- | -------------------------------------------------------------------- |
| `IAssetResolver`            | `UseAssetResolver<T>()`          | no images                     | Turns the `image-ref` ids in blocks into image details for readers.  |
| `ICmsChangeHandler`         | `UseChangeHandler<T>()`          | nothing                       | Hears about published changes, for example to purge a site's cache.  |
| `IBlockChangeDescriber`     | `UseBlockChangeDescriber<T>()`   | generic wording               | Words the change summaries in a page's history.                     |
| `ILinkedPageImageSelector`  | `UseLinkedPageImageSelector<T>()`| none                          | Picks the image shown for a page another page links to.             |
| `IBlockValidator`           | `AddBlockValidator<T>()`         | —                             | Rules a block's schema can't express.                                |
| `IReferenceExtractor`       | `AddReferenceExtractor<T>()`     | typed references              | Finds references in blocks, for `IBlockReferenceQuery`. The default finds `{ __typename, id }` objects and `page-ref:` links. |
| `IPublicationRule`          | `AddPublicationRule<T>()`        | —                             | Refuses to publish a page that breaks one of your rules.             |

### Reading and writing from your service

Your service reads through `ICmsPageReader` and `IBlockReferenceQuery`, and writes through `IPageService`,
`IPageVersionServices` and `IBlockReferenceReindexer`. Keep page and version ids as plain values: C4M's tables are its
own, so don't add foreign keys or navigations into them.

### Atomicity

Each engine operation commits in its own transaction, and that is the atomicity boundary. An operation refuses to run
inside an ambient `TransactionScope` or a transaction on the engine's context, and the engine shares no transactions
with your service.

Write to the engine first, then record the ids it returns in your own transaction. To retry safely:

- choose a new page's id yourself (`NewPage.PageId`): repeating the create returns the same page;
- pass an idempotency key with a version write;
- pass the latest version you expect (`PageVersionWrite`): the write returns a conflict if an editor saved meanwhile.

## Block schemas

Block and menu action types are zod schemas, and every version a type has had stays registered. Stored data at any
version parses with that version's schema and upgrades to the latest.

```ts
import { defineBlock, defineCatalogue, syncSchemas } from "@conradsix/c4m-schema";
import { z } from "zod";

const QuoteV1Schema = z.object({
  __typename: z.literal("quote"),
  __version: z.literal(1),
  text: z.string().min(1),
});

const QuoteV2Schema = QuoteV1Schema.extend({
  __version: z.literal(2),
  attribution: z.string().optional(),
});

const quote = defineBlock("quote", { baseline: QuoteV1Schema })
  .version(QuoteV2Schema)
  .from((v1) => ({ ...v1, __version: 2 }));

const blocks = defineCatalogue([quote]);
```

`syncSchemas` registers the versions the engine lacks and checks that the rest are unchanged. A registered schema never
changes, so a change to a block is a new version with an upgrade step. Run it when your editor deploys, through a
small registry over the client's block type endpoints.

The engine checks each block against its type's JSON Schema, which is what zod's `z.toJSONSchema` emits. Its validator
implements JSON Schema 2020-12 for the keywords zod emits, and refuses to register a schema that uses any other, so a
rule is never silently skipped. [docs/validation.md](docs/validation.md) has the details.

The package also has the engine's own contracts: `image-ref` and `page-ref` references, `page-ref:` links in markdown,
page SEO limits, and the standard menu actions (`empty`, `page-link` and `external-link`).

## Client

```ts
import { Api } from "@conradsix/c4m-client";

const c4m = new Api({ baseUrl: "https://cms.example.com" });
const { data: page } = await c4m.pages.getPageByPath({ path: "/about-us" });
```

The client is generated from the sample host's OpenAPI document, so its types follow the engine's endpoints exactly.

## Preview

An editor opens the site's preview tab with a signed token. The tab says when it's ready, and the editor sends it the
unsaved page or menu over `postMessage`.

```ts
import { createPreviewProtocol } from "@conradsix/c4m-preview";

const preview = createPreviewProtocol({ block: BlockSchema, menuAction: MenuActionSchema });
```

`createPreviewProtocol` builds the message schemas from your own block and menu action schemas. Sign and check tokens on
the server with `signPreviewToken` and `verifyPreviewToken` from `@conradsix/c4m-preview/server`.

## Testing your host

```csharp
services.AddC4M(c4m => c4m
    .UsePostgres(_ => connectionString)
    .UseFakeAssetResolver()
    .UseRecordingChangeHandler());
services.AddC4MTesting();
```

- `C4MTestData` seeds block types and pages, and `ResetAsync` empties the engine's tables between tests.
- `FakeAssetResolver` returns the images you add to it.
- `RecordingChangeHandler` records the changes the engine reports.

## Develop

The .NET solution is `ConradSix.C4M.slnx`, and the npm workspace is in `typescript/`. The tests start PostgreSQL with
Testcontainers, so they need Docker.

```bash
dotnet build ConradSix.C4M.slnx -c Release
dotnet test --solution ConradSix.C4M.slnx -c Release

cd typescript
yarn install --immutable
yarn check:all
```

To run the sample host against the repo's database:

```bash
docker compose up -d postgres
dotnet run --project samples/ConradSix.C4M.SampleHost --urls http://localhost:5000
```

Its OpenAPI UI is at `http://localhost:5000/swagger`.

`./pack-local.sh` packs every package at a local version that sorts above the last release: the NuGet packages into
`~/.nuget/local-c4m` and the npm tarballs into `~/.npm-local/c4m`. It prints the version.

[AGENTS.md](AGENTS.md) has the conventions. [docs/](docs) covers the validator's dialect, the schema's fixed
identifiers, and how to add a migration.

## License

MIT
