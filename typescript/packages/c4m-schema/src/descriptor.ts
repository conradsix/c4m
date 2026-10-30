import type { z } from "zod";

// One version of a CMS type, as the CMS registers it and checks stored data against it.
type CmsSchemaDescriptor = {
  readonly name: string;
  readonly version: number;
  readonly schema: z.ZodObject;
};

export type { CmsSchemaDescriptor };
