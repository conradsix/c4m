import type { RegisteredSchema, SchemaRegistry } from "./syncSchemas.js";

import { describe, expect, it } from "vitest";
import { z } from "zod";

import { syncSchemas } from "./syncSchemas.js";

const NoteV1 = z.object({
  __typename: z.literal("note"),
  __version: z.literal(1),
  text: z.string(),
});
const NoteV2 = z.object({
  __typename: z.literal("note"),
  __version: z.literal(2),
  markdown: z.string(),
});
const descriptors = [
  { name: "note", version: 1, schema: NoteV1 },
  { name: "note", version: 2, schema: NoteV2 },
];

const registryWith = (
  schemas: RegisteredSchema[],
): SchemaRegistry & { readonly schemas: RegisteredSchema[] } => ({
  schemas,
  list: () => Promise.resolve([...schemas]),
  create: (schema) => {
    schemas.push(schema);
    return Promise.resolve();
  },
});

const registered = (version: 1 | 2, schema: z.ZodObject): RegisteredSchema => ({
  name: "note",
  version,
  // The CMS returns the schema with its keys in its own order.
  jsonSchema: JSON.stringify(Object.fromEntries(Object.entries(z.toJSONSchema(schema)).reverse())),
});

describe("syncSchemas", () => {
  it("creates what the CMS lacks and checks what it has", async () => {
    const registry = registryWith([registered(1, NoteV1)]);

    const result = await syncSchemas(descriptors, registry);

    expect(result.validated).toEqual([{ name: "note", version: 1 }]);
    expect(result.created).toEqual([{ name: "note", version: 2 }]);
    expect(registry.schemas.map((schema) => schema.version)).toEqual([1, 2]);
  });

  it("fails when a registered schema changed or is no longer local", async () => {
    const registry = registryWith([
      registered(1, NoteV2),
      { name: "retired", version: 1, jsonSchema: "{}" },
    ]);

    const failure = await syncSchemas(descriptors, registry).then(
      () => undefined,
      (error: unknown) => String(error),
    );

    expect(failure).toContain("MISMATCH: Schema mismatch for 'note' version 1");
    expect(failure).toContain("ORPHANED: The CMS has 'retired' version 1");
  });
});
