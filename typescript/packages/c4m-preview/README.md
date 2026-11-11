# @conradsix/c4m-preview

Live preview for [C4M](https://github.com/conradsix/c4m). An editor opens the site's preview tab with a signed token;
the tab says when it's ready, and the editor sends it the unsaved page or menu over `postMessage`.

```ts
import { createPreviewProtocol } from "@conradsix/c4m-preview";
import { signPreviewToken, verifyPreviewToken } from "@conradsix/c4m-preview/server";

const preview = createPreviewProtocol({ block: BlockSchema, menuAction: MenuActionSchema });
```

- `createPreviewProtocol` builds the message schemas from your block and menu action schemas. Pass `messageTypes` to
  keep message names you already use.
- `@conradsix/c4m-preview/server` signs and checks preview tokens with `node:crypto`, so import it only on the server.

ESM only, with `zod` 4 as a peer dependency.
