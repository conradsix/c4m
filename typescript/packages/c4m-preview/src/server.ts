import { createHmac, timingSafeEqual } from "node:crypto";

import { z } from "zod";

// Preview tokens: the editor signs one to open the site's preview of an unsaved page or menu, and the site checks it.
// They use node:crypto, so they belong on the server.
const PagePreviewTokenClaimsSchema = z.object({
  kind: z.literal("page"),
  pageId: z.uuid(),
  pageVersionId: z.uuid(),
  sessionId: z.uuid(),
  expiresAt: z.number().int().positive(),
});

const MenuPreviewTokenClaimsSchema = z.object({
  kind: z.literal("menu"),
  sessionId: z.uuid(),
  path: z.string().startsWith("/"),
  expiresAt: z.number().int().positive(),
});

const PreviewTokenClaimsSchema = z.discriminatedUnion("kind", [
  PagePreviewTokenClaimsSchema,
  MenuPreviewTokenClaimsSchema,
]);

type PreviewTokenClaims = z.infer<typeof PreviewTokenClaimsSchema>;
type PagePreviewTokenClaims = z.infer<typeof PagePreviewTokenClaimsSchema>;
type MenuPreviewTokenClaims = z.infer<typeof MenuPreviewTokenClaimsSchema>;

const signatureFor = (payload: string, secret: string): string =>
  createHmac("sha256", secret).update(payload).digest("base64url");

const signPreviewToken = (claims: PreviewTokenClaims, secret: string): string => {
  const parsed = PreviewTokenClaimsSchema.parse(claims);
  const payload = Buffer.from(JSON.stringify(parsed), "utf8").toString("base64url");
  return `${payload}.${signatureFor(payload, secret)}`;
};

const verifyPreviewToken = (token: string, secret: string): PreviewTokenClaims | undefined => {
  const [payload, signature, unexpected] = token.split(".");
  if (!payload || !signature || unexpected) return undefined;

  const expected = signatureFor(payload, secret);
  const actualBytes = Buffer.from(signature, "utf8");
  const expectedBytes = Buffer.from(expected, "utf8");
  if (actualBytes.length !== expectedBytes.length || !timingSafeEqual(actualBytes, expectedBytes)) {
    return undefined;
  }

  try {
    const decoded: unknown = JSON.parse(Buffer.from(payload, "base64url").toString("utf8"));
    const parsed = PreviewTokenClaimsSchema.safeParse(decoded);
    if (!parsed.success || parsed.data.expiresAt <= Date.now()) return undefined;
    return parsed.data;
  } catch {
    return undefined;
  }
};

export { signPreviewToken, verifyPreviewToken };
export type { MenuPreviewTokenClaims, PagePreviewTokenClaims, PreviewTokenClaims };
