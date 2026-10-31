import { readFileSync, writeFileSync } from "node:fs";

import { describe, expect, it } from "vitest";
import { z } from "zod";

import { isJsonEqual } from "./isJsonEqual.js";
import { standardMenuActions } from "./menuActions.js";
import { ImageRefSchema, PageRefSchema } from "./references.js";

// The schemas zod emits, and which data zod takes as stored, for the engine's validator to agree with. Stored data is
// zod's output, so a sample is valid when zod parses it and gives it back unchanged. The engine's tests read the
// fixture this writes; run with UPDATE_ZOD_PARITY=1 to rewrite it after changing the cases or zod.
const fixture = new URL(
  "../../../../test/ConradSix.C4M.Tests/assets/zod-parity.json",
  import.meta.url,
);

const id = "019a3a3c-0b8e-7c7d-9d1e-2f3a4b5c6d7e";

type Node = { name: string; children: Node[] };
const TreeSchema: z.ZodType<Node> = z.lazy(() =>
  z.object({ name: z.string().min(1), children: z.array(TreeSchema) }),
);

const cases: ReadonlyArray<{ name: string; schema: z.ZodType; samples: readonly unknown[] }> = [
  {
    name: "a versioned block with required and optional fields",
    schema: z.object({
      __typename: z.literal("note"),
      __version: z.literal(2),
      title: z.string().min(1).max(10),
      subtitle: z.string().optional(),
    }),
    samples: [
      { __typename: "note", __version: 2, title: "Hi" },
      { __typename: "note", __version: 2, title: "Hi", subtitle: "There" },
      { __typename: "note", __version: 3, title: "Hi" },
      { __typename: "quote", __version: 2, title: "Hi" },
      { __typename: "note", __version: 2, title: "" },
      { __typename: "note", __version: 2, title: "Much too long" },
      { __typename: "note", __version: 2 },
      { __typename: "note", __version: 2, title: "Hi", extra: true },
      { __typename: "note", __version: 2, title: 5 },
      "note",
    ],
  },
  {
    name: "enums, nullables and defaults",
    schema: z.object({
      tone: z.enum(["plain", "accent"]),
      caption: z.string().nullable(),
      showButton: z.boolean().optional().default(false),
    }),
    samples: [
      { tone: "plain", caption: null, showButton: true },
      { tone: "accent", caption: "Hello", showButton: false },
      { tone: "loud", caption: null, showButton: true },
      { tone: "plain", caption: 3, showButton: true },
      { tone: "plain", caption: null },
    ],
  },
  {
    name: "unions and discriminated unions",
    schema: z.object({
      value: z.union([z.string(), z.number()]),
      link: z.discriminatedUnion("kind", [
        z.object({ kind: z.literal("page"), pageRef: PageRefSchema }),
        z.object({ kind: z.literal("url"), url: z.url() }),
      ]),
    }),
    samples: [
      { value: "a", link: { kind: "page", pageRef: { __typename: "page-ref", id } } },
      { value: 1.5, link: { kind: "url", url: "https://example.com/a?b=c#d" } },
      { value: true, link: { kind: "url", url: "https://example.com" } },
      { value: "a", link: { kind: "url", url: "not a url" } },
      { value: "a", link: { kind: "page", url: "https://example.com" } },
      { value: "a", link: { kind: "file", url: "https://example.com" } },
    ],
  },
  {
    name: "arrays, tuples and records",
    schema: z.object({
      tags: z.array(z.string()).min(1).max(3),
      point: z.tuple([z.number(), z.number()]),
      counts: z.record(z.string(), z.int()),
    }),
    samples: [
      { tags: ["a"], point: [1, 2], counts: { a: 1 } },
      { tags: ["a", "b", "c"], point: [0, -1.5], counts: {} },
      { tags: [], point: [1, 2], counts: {} },
      { tags: ["a", "b", "c", "d"], point: [1, 2], counts: {} },
      { tags: ["a"], point: [1], counts: {} },
      { tags: ["a"], point: [1, 2, 3], counts: {} },
      { tags: ["a"], point: [1, "2"], counts: {} },
      { tags: ["a"], point: [1, 2], counts: { a: 1.5 } },
      { tags: [1], point: [1, 2], counts: {} },
    ],
  },
  {
    name: "string formats",
    schema: z.object({
      pattern: z.string().regex(/^[a-z]+-\d{2}$/),
      uuid: z.uuid(),
      email: z.email(),
      url: z.url(),
      at: z.iso.datetime(),
      day: z.iso.date(),
    }),
    samples: [
      {
        pattern: "abc-12",
        uuid: id,
        email: "a@example.com",
        url: "https://example.com/x",
        at: "2026-10-31T12:30:00Z",
        day: "2026-10-31",
      },
      {
        pattern: "abc-123",
        uuid: id,
        email: "a@example.com",
        url: "https://example.com/x",
        at: "2026-10-31T12:30:00Z",
        day: "2026-10-31",
      },
      {
        pattern: "abc-12",
        uuid: "not-a-uuid",
        email: "a@example.com",
        url: "https://example.com/x",
        at: "2026-10-31T12:30:00Z",
        day: "2026-10-31",
      },
      {
        pattern: "abc-12",
        uuid: id,
        email: "not an email",
        url: "https://example.com/x",
        at: "2026-10-31T12:30:00Z",
        day: "2026-10-31",
      },
      {
        pattern: "abc-12",
        uuid: id,
        email: "a@example.com",
        url: "example.com",
        at: "2026-10-31T12:30:00Z",
        day: "2026-10-31",
      },
      {
        pattern: "abc-12",
        uuid: id,
        email: "a@example.com",
        url: "https://example.com/x",
        at: "yesterday",
        day: "2026-10-31",
      },
      {
        pattern: "abc-12",
        uuid: id,
        email: "a@example.com",
        url: "https://example.com/x",
        at: "2026-10-31T12:30:00Z",
        day: "31/10/2026",
      },
    ],
  },
  {
    name: "numbers",
    schema: z.object({
      count: z.int().min(1).max(10),
      price: z.number().positive(),
      share: z.number().min(0).lt(1),
      step: z.number().multipleOf(0.25),
    }),
    samples: [
      { count: 1, price: 0.01, share: 0, step: 0.75 },
      { count: 10, price: 1e6, share: 0.99, step: -2 },
      { count: 0, price: 1, share: 0, step: 0 },
      { count: 11, price: 1, share: 0, step: 0 },
      { count: 1.5, price: 1, share: 0, step: 0 },
      { count: 1, price: 0, share: 0, step: 0 },
      { count: 1, price: 1, share: 1, step: 0 },
      { count: 1, price: 1, share: -0.1, step: 0 },
      { count: 1, price: 1, share: 0, step: 0.1 },
    ],
  },
  {
    name: "numbers as JavaScript reads them",
    schema: z.object({
      sort: z.enum({ Relevance: 0, Newest: 1 }),
      count: z.int(),
      share: z.number().gt(0.1),
    }),
    samples: [
      { sort: 0, count: 1, share: 0.2 },
      { sort: 1, count: 9007199254740991, share: 0.10000000000000002 },
      { sort: 1e-29, count: 1, share: 0.2 },
      { sort: 0, count: 1e-29, share: 0.2 },
      { sort: 0, count: 1, share: 0.1 },
    ],
  },
  {
    name: "patterns as JavaScript runs them",
    schema: z.object({
      condition: z.string().regex(/^(?!\s*$)(?!\s*(?:none|standard)\s*$).+$/),
      word: z.string().regex(/^\S+$/),
      slug: z.string().regex(/^[a-z]+$/),
    }),
    samples: [
      { condition: "premium", word: "a", slug: "abc" },
      { condition: "a\u00a0b", word: "a", slug: "abc" },
      { condition: "\u00a0", word: "a", slug: "abc" },
      { condition: "\u3000\u2028", word: "a", slug: "abc" },
      { condition: "a\rb", word: "a", slug: "abc" },
      { condition: "premium", word: "a\u00a0b", slug: "abc" },
      { condition: "premium", word: "a", slug: "abc\n" },
    ],
  },
  {
    name: "intersections",
    schema: z.intersection(z.object({ a: z.string() }), z.object({ b: z.number() })),
    samples: [{ a: "x", b: 1 }, { a: "x" }, { b: 1 }, { a: 1, b: 1 }],
  },
  {
    name: "recursive schemas",
    schema: TreeSchema,
    samples: [
      { name: "root", children: [] },
      { name: "root", children: [{ name: "leaf", children: [] }] },
      { name: "root", children: [{ name: "", children: [] }] },
      { name: "root", children: [{ name: "leaf" }] },
    ],
  },
  {
    name: "references",
    schema: z.object({ image: ImageRefSchema.nullable(), page: PageRefSchema }),
    samples: [
      { image: { __typename: "image-ref", id }, page: { __typename: "page-ref", id } },
      { image: null, page: { __typename: "page-ref", id } },
      { image: { __typename: "page-ref", id }, page: { __typename: "page-ref", id } },
      { image: null, page: { __typename: "page-ref", id: "x" } },
    ],
  },
  ...standardMenuActions.map((action) => ({
    name: `the ${action.name} menu action`,
    schema: action.latest,
    samples: [
      { __typename: "empty", __version: 1 },
      {
        __typename: "page-link",
        __version: 1,
        pageRef: { __typename: "page-ref", id },
        openInNewTab: false,
      },
      { __typename: "external-link", __version: 1, url: "https://example.com", openInNewTab: true },
      { __typename: "external-link", __version: 1, url: "https://example.com" },
    ],
  })),
];

const isStored = (schema: z.ZodType, data: unknown): boolean => {
  const result = schema.safeParse(data);
  return result.success && isJsonEqual(JSON.stringify(result.data), JSON.stringify(data));
};

const generated = cases.map(({ name, schema, samples }) => ({
  name,
  schema: z.toJSONSchema(schema),
  samples: samples.map((data) => ({ data, valid: isStored(schema, data) })),
}));

describe("the zod parity fixture", () => {
  it("is what zod emits and accepts now", () => {
    if (process.env.UPDATE_ZOD_PARITY)
      writeFileSync(fixture, `${JSON.stringify(generated, null, 2)}\n`);

    expect(JSON.parse(readFileSync(fixture, "utf8"))).toEqual(generated);
  });

  it("has a valid and an invalid sample for each case", () => {
    for (const { name, samples } of generated) {
      expect(
        samples.some((sample) => sample.valid),
        name,
      ).toBe(true);
      expect(
        samples.some((sample) => !sample.valid),
        name,
      ).toBe(true);
    }
  });
});
