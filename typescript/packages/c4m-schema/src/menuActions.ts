import { z } from "zod";

import { defineBlock } from "./defineBlock";

// Menu action types version and upgrade as blocks do.
const defineMenuAction = defineBlock;

// A menu item that only opens its children. The engine requires it to have some.
const EmptyActionV1Schema = z.object({
  __typename: z.literal("empty"),
  __version: z.literal(1),
});

const PageLinkActionV1Schema = z.object({
  __typename: z.literal("page-link"),
  __version: z.literal(1),
  pageRef: z.object({
    __typename: z.literal("page-ref"),
    id: z.uuid(),
  }),
  openInNewTab: z.boolean().optional().default(false),
});

const ExternalLinkActionV1Schema = z.object({
  __typename: z.literal("external-link"),
  __version: z.literal(1),
  url: z.url("Must be a valid URL"),
  openInNewTab: z.boolean().optional().default(true),
});

// The menu actions every host has: a heading for children, a link to a page, and a link elsewhere.
const standardMenuActions = [
  defineMenuAction("empty", { baseline: EmptyActionV1Schema }),
  defineMenuAction("page-link", { baseline: PageLinkActionV1Schema }),
  defineMenuAction("external-link", { baseline: ExternalLinkActionV1Schema }),
] as const;

type EmptyActionV1 = z.infer<typeof EmptyActionV1Schema>;
type PageLinkActionV1 = z.infer<typeof PageLinkActionV1Schema>;
type ExternalLinkActionV1 = z.infer<typeof ExternalLinkActionV1Schema>;

export {
  defineMenuAction,
  EmptyActionV1Schema,
  ExternalLinkActionV1Schema,
  PageLinkActionV1Schema,
  standardMenuActions,
};

export type { EmptyActionV1, ExternalLinkActionV1, PageLinkActionV1 };
