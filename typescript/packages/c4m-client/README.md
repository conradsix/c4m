# @conradsix/c4m-client

A typed fetch client for the [C4M](https://github.com/conradsix/c4m) engine's endpoints, generated from its OpenAPI
document. It has no dependencies.

```ts
import { Api } from "@conradsix/c4m-client";

const c4m = new Api({ baseUrl: "https://cms.example.com" });
const { data: page } = await c4m.pages.getPageByPath({ path: "/about-us" });
```

The client's version is the engine's: use the same version as your `ConradSix.C4M` package. ESM only.
