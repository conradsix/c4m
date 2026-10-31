import { z } from "zod";

const ImageRefSchema = z.object({
  __typename: z.literal("image-ref"),
  id: z.uuid(),
});

const PageRefSchema = z.object({
  __typename: z.literal("page-ref"),
  id: z.uuid(),
});

const ResourceRefSchema = z.union([ImageRefSchema, PageRefSchema]);

type ImageRef = z.infer<typeof ImageRefSchema>;
type PageRef = z.infer<typeof PageRefSchema>;
type ResourceRef = z.infer<typeof ResourceRefSchema>;

export { ImageRefSchema, PageRefSchema, ResourceRefSchema };

export type { ImageRef, PageRef, ResourceRef };
