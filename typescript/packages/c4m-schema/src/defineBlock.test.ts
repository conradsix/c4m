import { describe, expect, it } from "vitest";
import { z } from "zod";

import { defineBlock } from "./defineBlock";
import { defineCatalogue } from "./defineCatalogue";

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
const NoteV3 = z.object({
  __typename: z.literal("note"),
  __version: z.literal(3),
  markdown: z.string(),
  tone: z.enum(["plain", "accent"]),
});
const QuoteV1 = z.object({
  __typename: z.literal("quote"),
  __version: z.literal(1),
  quote: z.string(),
});

const legacyNote = (block: z.output<typeof NoteV1>): object => ({
  __typename: "note",
  markdown: block.text,
});

const note = defineBlock("note", {
  legacy: { versions: [NoteV1], upgrade: legacyNote },
  baseline: NoteV2,
})
  .version(NoteV3)
  .from((block) => ({ ...block, __version: 3, tone: "plain" }));

const catalogue = defineCatalogue([note, defineBlock("quote", { baseline: QuoteV1 })]);

describe("defineBlock", () => {
  it("upgrades older versions through the legacy upgrade and then each later version's step", () => {
    expect(note.upgrade({ __typename: "note", __version: 1, text: "Hello" })).toEqual({
      __typename: "note",
      __version: 3,
      markdown: "Hello",
      tone: "plain",
    });
    expect(note.upgrade({ __typename: "note", __version: 2, markdown: "Hi" })).toEqual({
      __typename: "note",
      __version: 3,
      markdown: "Hi",
      tone: "plain",
    });
  });

  it("returns new data and leaves its input alone", () => {
    const latest = { __typename: "note", __version: 3, markdown: "Hi", tone: "accent" } as const;
    const stored = { __typename: "note", __version: 1, text: "Hello" } as const;

    const upgraded = note.upgrade(latest);

    expect(upgraded).toEqual(latest);
    expect(upgraded).not.toBe(latest);
    note.upgrade(stored);
    expect(stored).toEqual({ __typename: "note", __version: 1, text: "Hello" });
  });

  it("refuses versions that skip a number or belong to another block", () => {
    expect(() => defineBlock("note", { baseline: NoteV2 })).toThrow("version 1 is missing");
    expect(() => note.version(NoteV2)).toThrow("The next note version is 4");
    expect(() => defineBlock("quote", { baseline: NoteV1 })).toThrow('__typename "quote"');
  });
});

describe("defineCatalogue", () => {
  it("lists every version and each block's latest", () => {
    expect(catalogue.descriptors.map(({ name, version }) => `${name}:${version}`)).toEqual([
      "note:1",
      "note:2",
      "note:3",
      "quote:1",
    ]);
    expect(catalogue.current.map(({ name, version }) => `${name}:${version}`)).toEqual([
      "note:3",
      "quote:1",
    ]);
  });

  it("parses stored data at any version as its latest", () => {
    expect(catalogue.Block.parse({ __typename: "note", __version: 1, text: "Hello" })).toEqual({
      __typename: "note",
      __version: 3,
      markdown: "Hello",
      tone: "plain",
    });
    expect(
      catalogue.Block.safeParse({ __typename: "note", __version: 4, markdown: "Hi" }).success,
    ).toBe(false);
    expect(
      catalogue.Current.safeParse({ __typename: "note", __version: 2, markdown: "Hi" }).success,
    ).toBe(false);
  });

  it("reports a step whose result isn't valid as a parse issue", () => {
    const broken = defineCatalogue([
      defineBlock("note", { legacy: { versions: [NoteV1], upgrade: legacyNote }, baseline: NoteV2 })
        .version(NoteV3)
        // The step forgets the tone.
        .from((block) => ({ ...block, __version: 3 }) as z.input<typeof NoteV3>),
    ]);

    const result = broken.Block.safeParse({ __typename: "note", __version: 2, markdown: "Hi" });

    expect(result.success).toBe(false);
    expect(result.error?.issues.map((issue) => issue.path)).toEqual([["tone"]]);
  });

  it("refuses a block named twice", () => {
    expect(() => defineCatalogue([note, note])).toThrow("note twice");
  });

  it("refuses to upgrade a block it doesn't have", () => {
    expect(() => catalogue.upgrade({ __typename: "missing", __version: 1 })).toThrow(
      "Unknown block missing",
    );
  });
});
