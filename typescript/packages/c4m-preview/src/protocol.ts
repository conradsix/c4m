import { PageSeoInputSchema } from "@conradsix/c4m-schema";
import { z } from "zod";

// The editor and the site talk over postMessage: the site's preview tab says it's ready, and the editor sends it the
// unsaved page or menu. Both sides must agree on this version.
const PREVIEW_PROTOCOL_VERSION = 1 as const;

// The postMessage type names. A host that already shipped names of its own keeps them, so its editor and site can
// deploy apart.
type PreviewMessageTypes = {
  readonly pageReady: string;
  readonly pageSnapshot: string;
  readonly menuReady: string;
  readonly menuSnapshot: string;
};

const defaultMessageTypes: PreviewMessageTypes = {
  pageReady: "c4m:preview-ready",
  pageSnapshot: "c4m:preview-snapshot",
  menuReady: "c4m:menu-preview-ready",
  menuSnapshot: "c4m:menu-preview-snapshot",
};

type PreviewMenuItem<TAction> = {
  readonly key: string;
  readonly title: string;
  readonly action: TAction;
  readonly children: readonly PreviewMenuItem<TAction>[];
};

type PreviewPageDocumentSchema<TBlock extends z.ZodType> = z.ZodObject<{
  pageId: z.ZodUUID;
  pageVersionId: z.ZodUUID;
  revision: z.ZodNumber;
  slug: z.ZodString;
  parentPageId: z.ZodNullable<z.ZodUUID>;
  seo: typeof PageSeoInputSchema;
  blocks: z.ZodArray<TBlock>;
}>;

type PreviewMenuDocumentSchema<TMenuAction extends z.ZodType> = z.ZodObject<{
  name: z.ZodString;
  items: z.ZodArray<z.ZodType<PreviewMenuItem<z.output<TMenuAction>>>>;
}>;

type ProtocolVersion = z.ZodLiteral<typeof PREVIEW_PROTOCOL_VERSION>;

type PreviewProtocol<TBlock extends z.ZodType, TMenuAction extends z.ZodType> = {
  readonly messageTypes: PreviewMessageTypes;
  readonly PreviewPageDocumentSchema: PreviewPageDocumentSchema<TBlock>;
  readonly PreviewReadyMessageSchema: z.ZodObject<{
    type: z.ZodLiteral<string>;
    protocolVersion: ProtocolVersion;
    pageId: z.ZodUUID;
    pageVersionId: z.ZodUUID;
    sessionId: z.ZodUUID;
  }>;
  readonly PreviewSnapshotMessageSchema: z.ZodObject<{
    type: z.ZodLiteral<string>;
    protocolVersion: ProtocolVersion;
    sessionId: z.ZodUUID;
    document: PreviewPageDocumentSchema<TBlock>;
  }>;
  readonly PreviewMenuItemSchema: z.ZodType<PreviewMenuItem<z.output<TMenuAction>>>;
  readonly PreviewMenuDocumentSchema: PreviewMenuDocumentSchema<TMenuAction>;
  readonly MenuPreviewReadyMessageSchema: z.ZodObject<{
    type: z.ZodLiteral<string>;
    protocolVersion: ProtocolVersion;
    sessionId: z.ZodUUID;
  }>;
  readonly MenuPreviewSnapshotMessageSchema: z.ZodObject<{
    type: z.ZodLiteral<string>;
    protocolVersion: ProtocolVersion;
    sessionId: z.ZodUUID;
    document: PreviewMenuDocumentSchema<TMenuAction>;
  }>;
};

// The messages for one host's blocks and menu actions.
const createPreviewProtocol = <TBlock extends z.ZodType, TMenuAction extends z.ZodType>(options: {
  readonly block: TBlock;
  readonly menuAction: TMenuAction;
  readonly messageTypes?: Partial<PreviewMessageTypes>;
}): PreviewProtocol<TBlock, TMenuAction> => {
  const types = { ...defaultMessageTypes, ...options.messageTypes };

  const PreviewPageDocumentSchema = z.object({
    pageId: z.uuid(),
    pageVersionId: z.uuid(),
    revision: z.number().int().nonnegative(),
    slug: z.string(),
    parentPageId: z.uuid().nullable(),
    seo: PageSeoInputSchema,
    blocks: z.array(options.block),
  });

  const PreviewReadyMessageSchema = z.object({
    type: z.literal(types.pageReady),
    protocolVersion: z.literal(PREVIEW_PROTOCOL_VERSION),
    pageId: z.uuid(),
    pageVersionId: z.uuid(),
    sessionId: z.uuid(),
  });

  const PreviewSnapshotMessageSchema = z.object({
    type: z.literal(types.pageSnapshot),
    protocolVersion: z.literal(PREVIEW_PROTOCOL_VERSION),
    sessionId: z.uuid(),
    document: PreviewPageDocumentSchema,
  });

  // TypeScript can't see through a lazy schema over a generic action, so the item's type is stated.
  const PreviewMenuItemSchema = z.lazy(() =>
    z.object({
      key: z.string().min(1),
      title: z.string().trim().min(1).max(200),
      action: options.menuAction,
      children: z.array(PreviewMenuItemSchema),
    }),
  ) as unknown as z.ZodType<PreviewMenuItem<z.output<TMenuAction>>>;

  const PreviewMenuDocumentSchema = z.object({
    name: z.string().min(1),
    items: z.array(PreviewMenuItemSchema).max(20),
  });

  const MenuPreviewReadyMessageSchema = z.object({
    type: z.literal(types.menuReady),
    protocolVersion: z.literal(PREVIEW_PROTOCOL_VERSION),
    sessionId: z.uuid(),
  });

  const MenuPreviewSnapshotMessageSchema = z.object({
    type: z.literal(types.menuSnapshot),
    protocolVersion: z.literal(PREVIEW_PROTOCOL_VERSION),
    sessionId: z.uuid(),
    document: PreviewMenuDocumentSchema,
  });

  return {
    messageTypes: types,
    PreviewPageDocumentSchema,
    PreviewReadyMessageSchema,
    PreviewSnapshotMessageSchema,
    PreviewMenuItemSchema,
    PreviewMenuDocumentSchema,
    MenuPreviewReadyMessageSchema,
    MenuPreviewSnapshotMessageSchema,
  };
};

export { createPreviewProtocol, defaultMessageTypes, PREVIEW_PROTOCOL_VERSION };
export type { PreviewMenuItem, PreviewMessageTypes, PreviewProtocol };
