import { z } from "zod";

const PageSeoInputSchema = z.object({
  title: z.string().trim().max(60, "Use 60 characters or fewer"),
  description: z.string().trim().max(160, "Use 160 characters or fewer"),
  socialImageId: z.uuid().nullable(),
  index: z.boolean(),
  follow: z.boolean(),
});

const PublishablePageSeoSchema = PageSeoInputSchema.extend({
  title: z.string().trim().min(1, "Enter an SEO title").max(60, "Use 60 characters or fewer"),
  description: z
    .string()
    .trim()
    .min(1, "Enter an SEO description")
    .max(160, "Use 160 characters or fewer"),
});

type PageSeoInput = z.infer<typeof PageSeoInputSchema>;

export { PageSeoInputSchema, PublishablePageSeoSchema };
export type { PageSeoInput };
