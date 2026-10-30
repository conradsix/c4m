import type { CmsSchemaDescriptor } from "./descriptor";

import { z } from "zod";

import { isJsonEqual } from "./isJsonEqual";

type RegisteredSchema = {
  readonly name: string;
  readonly version: number;
  readonly jsonSchema: string;
};

// Where the CMS keeps the schemas it checks stored data against: its block types, or its menu action types.
type SchemaRegistry = {
  list(): Promise<readonly RegisteredSchema[]>;
  create(schema: RegisteredSchema): Promise<void>;
};

type SyncResult = {
  readonly created: Array<{ name: string; version: number }>;
  readonly validated: Array<{ name: string; version: number }>;
  readonly errors: Array<{ type: "mismatch" | "orphaned" | "creation"; details: string }>;
};

const keyOf = (schema: { readonly name: string; readonly version: number }): string =>
  `${schema.name}:${schema.version}`;

// Registers the schemas the registry lacks and checks the rest are unchanged. A registered schema never changes, so
// a different one, or one the host no longer has, fails the sync.
const syncSchemas = async (
  descriptors: readonly CmsSchemaDescriptor[],
  registry: SchemaRegistry,
): Promise<SyncResult> => {
  if (descriptors.length === 0) throw new Error("No local schemas to sync");

  const local = new Map(
    descriptors.map((descriptor) => [
      keyOf(descriptor),
      { ...descriptor, jsonSchema: JSON.stringify(z.toJSONSchema(descriptor.schema)) },
    ]),
  );
  const registered = new Map((await registry.list()).map((schema) => [keyOf(schema), schema]));
  const result: SyncResult = { created: [], validated: [], errors: [] };

  for (const [key, schema] of registered)
    if (!local.has(key))
      result.errors.push({
        type: "orphaned",
        details: `The CMS has '${schema.name}' version ${schema.version}, which doesn't exist locally`,
      });

  for (const [key, schema] of local) {
    const existing = registered.get(key);
    const entry = { name: schema.name, version: schema.version };
    if (existing) {
      if (isJsonEqual(schema.jsonSchema, existing.jsonSchema)) result.validated.push(entry);
      else
        result.errors.push({
          type: "mismatch",
          details: `Schema mismatch for '${schema.name}' version ${schema.version}: local and CMS schemas differ`,
        });
      continue;
    }

    try {
      await registry.create({ ...entry, jsonSchema: schema.jsonSchema });
      result.created.push(entry);
    } catch (error) {
      result.errors.push({
        type: "creation",
        details: `Failed to create '${schema.name}' version ${schema.version}: ${String(error)}`,
      });
    }
  }

  if (result.errors.length > 0)
    throw new Error(
      `Schema sync failed:\n${result.errors.map((error) => `${error.type.toUpperCase()}: ${error.details}`).join("\n")}`,
    );
  return result;
};

export { syncSchemas };
export type { RegisteredSchema, SchemaRegistry, SyncResult };
