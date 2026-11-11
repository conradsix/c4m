import type { PreviewTokenClaims } from "@conradsix/c4m-preview/server";

import assert from "node:assert/strict";
import { readFileSync, realpathSync } from "node:fs";
import { resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

import { Api } from "@conradsix/c4m-client";
import { createPreviewProtocol, PREVIEW_PROTOCOL_VERSION } from "@conradsix/c4m-preview";
import { signPreviewToken, verifyPreviewToken } from "@conradsix/c4m-preview/server";
import { defineBlock, defineCatalogue, EmptyActionV1Schema } from "@conradsix/c4m-schema";
import { z } from "zod";

for (const name of ["c4m-schema", "c4m-client", "c4m-preview"]) {
  const entry = import.meta.resolve(`@conradsix/${name}`);
  assert.ok(realpathSync(fileURLToPath(entry)).startsWith(`${resolve("node_modules")}${sep}`));
  const metadata: { version: string } = JSON.parse(
    readFileSync(new URL("../package.json", entry), "utf8"),
  );
  assert.equal(metadata.version, process.env.C4M_PACKAGE_VERSION);
}

const Note = z.object({
  __typename: z.literal("note"),
  __version: z.literal(1),
  text: z.string().min(1),
});
const catalogue = defineCatalogue([defineBlock("note", { baseline: Note })]);
assert.equal(
  catalogue.Block.parse({ __typename: "note", __version: 1, text: "Installed" }).text,
  "Installed",
);

const protocol = createPreviewProtocol({
  block: catalogue.Current,
  menuAction: EmptyActionV1Schema,
});
const sessionId = "019a3a3c-0b8e-7c7d-9d1e-2f3a4b5c6d80";
assert.ok(
  protocol.MenuPreviewReadyMessageSchema.safeParse({
    type: protocol.messageTypes.menuReady,
    protocolVersion: PREVIEW_PROTOCOL_VERSION,
    sessionId,
  }).success,
);

const claims: PreviewTokenClaims = {
  kind: "menu",
  sessionId,
  path: "/consumer",
  expiresAt: Date.now() + 60_000,
};
const secret = "c4m-package-consumer-preview-secret";
const token = signPreviewToken(claims, secret);
assert.deepEqual(verifyPreviewToken(token, secret), claims);
assert.equal(verifyPreviewToken(token, `${secret}-wrong`), undefined);

const client = new Api({
  baseUrl: "https://cms.example.test",
  customFetch: async (input): Promise<Response> => {
    const url = new URL(String(input));
    assert.equal(url.pathname, "/pages/by-path");
    assert.equal(url.searchParams.get("path"), "/consumer");
    return new Response(JSON.stringify({ pageId: sessionId }), {
      headers: { "Content-Type": "application/json" },
    });
  },
});
assert.equal((await client.pages.getPageByPath({ path: "/consumer" })).data.pageId, sessionId);
console.log(`Installed C4M npm packages ${process.env.C4M_PACKAGE_VERSION} passed.`);
