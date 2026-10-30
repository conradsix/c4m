import { z } from "zod";

// A block version's schema. Its __typename and __version are literals, so stored data names the schema it was saved
// with.
type VersionedSchema = z.ZodObject;

// Data at one version, as the next.
type Upgrade<TFrom extends VersionedSchema, TTo extends VersionedSchema> = (
  block: z.output<TFrom>,
) => z.input<TTo>;

type BlockDefinition<
  TName extends string,
  TSchemas extends readonly VersionedSchema[],
  TLatest extends VersionedSchema,
> = {
  readonly name: TName;
  // Every version, oldest first.
  readonly schemas: TSchemas;
  readonly latest: TLatest;
  // Data stored at any version, as the latest: through the legacy upgrade, then each later version's step. Returns new
  // data and leaves its input alone, even when the input is already the latest version.
  upgrade(block: z.output<TSchemas[number]>): z.output<TLatest>;
  // Adds the next version; its from takes the step that brings the current latest to it.
  version<TNext extends VersionedSchema>(
    schema: TNext,
  ): NextVersion<TName, TSchemas, TLatest, TNext>;
};

type NextVersion<
  TName extends string,
  TSchemas extends readonly VersionedSchema[],
  TLatest extends VersionedSchema,
  TNext extends VersionedSchema,
> = {
  from(
    upgrade: Upgrade<TLatest, TNext>,
  ): BlockDefinition<TName, readonly [...TSchemas, TNext], TNext>;
};

type Legacy<TVersions extends readonly VersionedSchema[]> = {
  // The versions from before the baseline, oldest first.
  readonly versions: TVersions;
  // Data stored at any of them, as the baseline. The catalogue sets the baseline's version but doesn't check the result
  // against its schema: it is taken as it is.
  readonly upgrade: (block: z.output<TVersions[number]>) => object;
};

type AnyUpgrade = (block: never) => unknown;

type Chain = {
  readonly name: string;
  readonly schemas: readonly VersionedSchema[];
  readonly baseline: number;
  readonly legacy: ((block: never) => object) | undefined;
  // steps[i] takes version baseline + i to the next.
  readonly steps: readonly AnyUpgrade[];
};

const literalOf = (schema: VersionedSchema, key: "__typename" | "__version"): unknown => {
  const field = (schema.shape as Readonly<Record<string, unknown>>)[key];
  return field instanceof z.ZodLiteral && field.values.size === 1
    ? [...field.values][0]
    : undefined;
};

const versionOf = (name: string, schema: VersionedSchema): number => {
  const version = literalOf(schema, "__version");
  if (literalOf(schema, "__typename") !== name || typeof version !== "number")
    throw new Error(
      `Every ${name} schema needs the literals __typename "${name}" and a __version number`,
    );
  return version;
};

const itemAt = <T>(items: readonly T[], index: number, name: string): T => {
  const item = items[index];
  if (item === undefined) throw new Error(`${name} has no version ${index + 1}`);
  return item;
};

const upgradeChain = (chain: Chain, block: { readonly __version: number }): unknown => {
  const latest = chain.schemas.length;
  let version = block.__version;
  if (!Number.isInteger(version) || version < 1 || version > latest)
    throw new Error(`${chain.name} has no version ${String(version)}`);
  let data: unknown = block;
  if (version < chain.baseline) {
    if (!chain.legacy) throw new Error(`${chain.name} has no legacy upgrade`);
    data = { ...chain.legacy(data as never), __version: chain.baseline };
    version = chain.baseline;
  }
  for (; version < latest; version++) {
    const step = itemAt(chain.steps, version - chain.baseline, chain.name);
    data = itemAt(chain.schemas, version, chain.name).parse(step(data as never));
  }
  return data;
};

const build = (
  chain: Chain,
): BlockDefinition<string, readonly VersionedSchema[], VersionedSchema> => ({
  name: chain.name,
  schemas: chain.schemas,
  latest: itemAt(chain.schemas, chain.schemas.length - 1, chain.name),
  upgrade: (block) =>
    upgradeChain(
      chain,
      structuredClone(block) as { readonly __version: number },
    ) as z.output<VersionedSchema>,
  version: <TNext extends VersionedSchema>(
    schema: TNext,
  ): NextVersion<string, readonly VersionedSchema[], VersionedSchema, TNext> => {
    if (versionOf(chain.name, schema) !== chain.schemas.length + 1)
      throw new Error(`The next ${chain.name} version is ${chain.schemas.length + 1}`);
    return {
      from: (upgrade) =>
        build({
          ...chain,
          schemas: [...chain.schemas, schema],
          steps: [...chain.steps, upgrade],
        }) as BlockDefinition<string, readonly [...VersionedSchema[], TNext], TNext>,
    };
  },
});

// A block type and every version it has had. Versions from before the baseline upgrade to it through the legacy
// upgrade; each version added after it brings its own step.
const defineBlock = <
  TName extends string,
  TBaseline extends VersionedSchema,
  const TLegacy extends readonly VersionedSchema[] = readonly [],
>(
  name: TName,
  definition: { readonly baseline: TBaseline; readonly legacy?: Legacy<TLegacy> },
): BlockDefinition<TName, readonly [...TLegacy, TBaseline], TBaseline> => {
  const schemas = [...(definition.legacy?.versions ?? []), definition.baseline];
  for (const [index, schema] of schemas.entries())
    if (versionOf(name, schema) !== index + 1)
      throw new Error(
        `${name} versions must run from 1 without gaps; version ${index + 1} is missing`,
      );
  return build({
    name,
    schemas,
    baseline: schemas.length,
    legacy: definition.legacy?.upgrade,
    steps: [],
  }) as BlockDefinition<TName, readonly [...TLegacy, TBaseline], TBaseline>;
};

export { defineBlock, versionOf };
export type { BlockDefinition, Upgrade, VersionedSchema };
