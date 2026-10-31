import { describe, expect, it } from "vitest";
import { z } from "zod";

import { createPreviewProtocol, PREVIEW_PROTOCOL_VERSION } from "./protocol";
import { signPreviewToken, verifyPreviewToken } from "./server";

const secret = "a-preview-secret-of-at-least-32-characters";
const pageId = "019a3a3c-0b8e-7c7d-9d1e-2f3a4b5c6d7e";
const pageVersionId = "019a3a3c-0b8e-7c7d-9d1e-2f3a4b5c6d7f";
const sessionId = "019a3a3c-0b8e-7c7d-9d1e-2f3a4b5c6d80";

const NoteSchema = z.object({ __typename: z.literal("note"), text: z.string() });
const LinkSchema = z.object({ __typename: z.literal("link"), url: z.url() });

describe("preview tokens", () => {
  const claims = {
    kind: "page",
    pageId,
    pageVersionId,
    sessionId,
    expiresAt: Date.now() + 60_000,
  } as const;

  it("verify when signed with the same secret", () => {
    expect(verifyPreviewToken(signPreviewToken(claims, secret), secret)).toEqual(claims);
  });

  it("don't verify with another secret, altered, or expired", () => {
    const token = signPreviewToken(claims, secret);

    expect(verifyPreviewToken(token, `${secret}!`)).toBeUndefined();
    expect(verifyPreviewToken(`${token}x`, secret)).toBeUndefined();
    expect(
      verifyPreviewToken(signPreviewToken({ ...claims, expiresAt: 1 }, secret), secret),
    ).toBeUndefined();
  });
});

describe("the preview protocol", () => {
  it("carries the host's blocks and menu actions", () => {
    const protocol = createPreviewProtocol({ block: NoteSchema, menuAction: LinkSchema });
    const snapshot = {
      type: "c4m:preview-snapshot",
      protocolVersion: PREVIEW_PROTOCOL_VERSION,
      sessionId,
      document: {
        pageId,
        pageVersionId,
        revision: 0,
        slug: "about",
        parentPageId: null,
        seo: {
          title: "About",
          description: "About us",
          socialImageId: null,
          index: true,
          follow: true,
        },
        blocks: [{ __typename: "note", text: "Hello" }],
      },
    };

    expect(protocol.PreviewSnapshotMessageSchema.safeParse(snapshot).success).toBe(true);
    expect(
      protocol.PreviewSnapshotMessageSchema.safeParse({
        ...snapshot,
        document: {
          ...snapshot.document,
          blocks: [{ __typename: "link", url: "https://example.com" }],
        },
      }).success,
    ).toBe(false);
    expect(
      protocol.PreviewMenuDocumentSchema.safeParse({
        name: "Main",
        items: [
          {
            key: "a",
            title: "Home",
            action: { __typename: "link", url: "https://example.com" },
            children: [],
          },
        ],
      }).success,
    ).toBe(true);
  });

  it("uses the names a host already shipped", () => {
    const protocol = createPreviewProtocol({
      block: NoteSchema,
      menuAction: LinkSchema,
      messageTypes: { pageReady: "site:ready" },
    });
    const ready = { protocolVersion: PREVIEW_PROTOCOL_VERSION, pageId, pageVersionId, sessionId };

    expect(
      protocol.PreviewReadyMessageSchema.safeParse({ ...ready, type: "site:ready" }).success,
    ).toBe(true);
    expect(
      protocol.PreviewReadyMessageSchema.safeParse({ ...ready, type: "c4m:preview-ready" }).success,
    ).toBe(false);
    expect(protocol.messageTypes.menuReady).toBe("c4m:menu-preview-ready");
  });
});
