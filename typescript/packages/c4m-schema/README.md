# @conradsix/c4m-schema

Block and menu action schemas for [C4M](https://github.com/conradsix/c4m), defined with zod 4, with every version a
type has had and the steps that upgrade stored data to the latest.

```ts
import { defineBlock, defineCatalogue, syncSchemas } from "@conradsix/c4m-schema";
```

- `defineBlock` and `defineCatalogue` declare block types and their versions.
- `syncSchemas` registers the versions the engine lacks and checks the rest are unchanged.
- `ImageRefSchema`, `PageRefSchema`, the `page-ref:` markdown link helpers, the page SEO limits and the standard menu
  actions are the engine's own contracts.

ESM only, with `zod` 4 as a peer dependency. See the [C4M README](https://github.com/conradsix/c4m#block-schemas).
