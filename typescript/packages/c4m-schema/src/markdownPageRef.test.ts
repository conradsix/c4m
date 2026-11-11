import { describe, expect, it } from "vitest";

import {
  extractMarkdownPageRefIds,
  fromMarkdownPageRefHref,
  toMarkdownPageRefHref,
} from "./markdownPageRef.js";

const PAGE_ID = "11111111-1111-4111-8111-111111111111";
const SECOND_PAGE_ID = "22222222-2222-4222-8222-222222222222";

describe("Markdown page references", () => {
  it("round-trips a page ID through the reserved Markdown link destination", () => {
    const href = toMarkdownPageRefHref(PAGE_ID);

    expect(href).toBe(`page-ref:${PAGE_ID}`);
    expect(fromMarkdownPageRefHref(href)).toBe(PAGE_ID);
  });

  it("rejects malformed or unrelated link destinations", () => {
    expect(fromMarkdownPageRefHref("/a-page")).toBeUndefined();
    expect(fromMarkdownPageRefHref("page-ref:not-a-page")).toBeUndefined();
    expect(() => toMarkdownPageRefHref("not-a-page")).toThrow("Invalid CMS page ID");
  });

  it("extracts unique valid page references from Markdown", () => {
    const markdown = [
      `[First](page-ref:${PAGE_ID})`,
      `[Second](page-ref:${SECOND_PAGE_ID})`,
      `[Duplicate](page-ref:${PAGE_ID})`,
      "[Invalid](page-ref:not-a-page)",
    ].join("\n");

    expect(extractMarkdownPageRefIds(markdown)).toEqual([PAGE_ID, SECOND_PAGE_ID]);
  });
});
