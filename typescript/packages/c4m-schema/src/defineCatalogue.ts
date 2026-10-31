import type { BlockDefinition, VersionedSchema } from "./defineBlock.js";
import type { CmsSchemaDescriptor } from "./descriptor.js";

import { z } from "zod";

import { versionOf } from "./defineBlock.js";

type AnyBlock = BlockDefinition<string, readonly VersionedSchema[], VersionedSchema>;

type VersionOf<TSchema> = TSchema extends VersionedSchema
  ? z.output<TSchema> extends { readonly __version: infer TVersion }
    ? TVersion
    : number
  : never;

type StoredSchemaOf<TBlocks extends readonly AnyBlock[]> = TBlocks[number]["schemas"][number];

type LatestSchemas<TBlocks extends readonly AnyBlock[]> = {
  -readonly [K in keyof TBlocks]: TBlocks[K]["latest"];
};

type DescriptorsOf<TBlock extends AnyBlock> = {
  [K in keyof TBlock["schemas"]]: {
    readonly name: TBlock["name"];
    readonly version: VersionOf<TBlock["schemas"][K]>;
    readonly schema: TBlock["schemas"][K];
  };
}[number];

type CurrentDescriptors<TBlocks extends readonly AnyBlock[]> = {
  readonly [K in keyof TBlocks]: {
    readonly name: TBlocks[K]["name"];
    readonly version: VersionOf<TBlocks[K]["latest"]>;
    readonly schema: TBlocks[K]["latest"];
  };
};

type StoredUnion<TBlocks extends readonly AnyBlock[]> = z.ZodUnion<StoredSchemaOf<TBlocks>[]>;

type CurrentUnion<TBlocks extends readonly AnyBlock[]> = z.ZodUnion<LatestSchemas<TBlocks>>;

type BlockCatalogue<TBlocks extends readonly AnyBlock[]> = {
  readonly blocks: TBlocks;
  // Every version of every block: what the CMS registers.
  readonly descriptors: readonly DescriptorsOf<TBlocks[number]>[];
  // Each block's latest version, in catalogue order.
  readonly current: CurrentDescriptors<TBlocks>;
  // Data stored at any version of any block.
  readonly Stored: StoredUnion<TBlocks>;
  // Any block at its latest version.
  readonly Current: CurrentUnion<TBlocks>;
  // Stored data, upgraded to its block's latest version.
  readonly Block: z.ZodPipe<
    StoredUnion<TBlocks>,
    z.ZodTransform<z.output<CurrentUnion<TBlocks>>, z.output<StoredUnion<TBlocks>>>
  >;
  // Upgrades data that names its block and version. Throws for a block or version the catalogue doesn't have.
  readonly upgrade: (block: {
    readonly __typename: string;
    readonly __version: number;
  }) => z.output<CurrentUnion<TBlocks>>;
};

// The blocks a host stores. Each block's name may appear once.
const defineCatalogue = <const TBlocks extends readonly AnyBlock[]>(
  blocks: TBlocks,
): BlockCatalogue<TBlocks> => {
  const byName = new Map<string, AnyBlock>();
  for (const block of blocks) {
    if (byName.has(block.name)) throw new Error(`The catalogue has ${block.name} twice`);
    byName.set(block.name, block);
  }

  const descriptors: CmsSchemaDescriptor[] = blocks.flatMap((block) =>
    block.schemas.map((schema) => ({
      name: block.name,
      version: versionOf(block.name, schema),
      schema,
    })),
  );
  const current = blocks.map((block) => ({
    name: block.name,
    version: versionOf(block.name, block.latest),
    schema: block.latest,
  }));
  const Stored = z.union(descriptors.map((descriptor) => descriptor.schema));
  const Current = z.union(blocks.map((block) => block.latest));

  const upgrade = (block: { readonly __typename: string; readonly __version: number }): unknown => {
    const definition = byName.get(block.__typename);
    if (!definition) throw new Error(`Unknown block ${block.__typename}`);
    return definition.upgrade(block);
  };

  const Block = Stored.transform((block, ctx) => {
    try {
      return upgrade(block as { readonly __typename: string; readonly __version: number });
    } catch (error) {
      if (!(error instanceof z.ZodError)) throw error;
      for (const issue of error.issues)
        ctx.issues.push({ code: "custom", message: issue.message, path: issue.path, input: block });
      return z.NEVER;
    }
  });

  return {
    blocks,
    descriptors,
    current,
    Stored,
    Current,
    Block,
    upgrade,
  } as unknown as BlockCatalogue<TBlocks>;
};

export { defineCatalogue };
export type { BlockCatalogue };
