import { PageRefSchema } from "./resourceRef";

const MARKDOWN_PAGE_REF_PREFIX = "page-ref:";

const toMarkdownPageRefHref = (pageId: string): string => {
  const result = PageRefSchema.shape.id.safeParse(pageId);
  if (!result.success) throw new Error(`Invalid CMS page ID: ${pageId}`);
  return `${MARKDOWN_PAGE_REF_PREFIX}${result.data}`;
};

const fromMarkdownPageRefHref = (href: string): string | undefined => {
  if (!href.startsWith(MARKDOWN_PAGE_REF_PREFIX)) return undefined;
  const result = PageRefSchema.shape.id.safeParse(href.slice(MARKDOWN_PAGE_REF_PREFIX.length));
  return result.success ? result.data : undefined;
};

const extractMarkdownPageRefIds = (markdown: string): string[] => {
  const ids = new Set<string>();

  for (const match of markdown.matchAll(/page-ref:([0-9a-f-]{36})/giu)) {
    const id = match[1];
    if (!id) continue;
    const result = PageRefSchema.shape.id.safeParse(id);
    if (result.success) ids.add(result.data);
  }

  return [...ids];
};

export { extractMarkdownPageRefIds, fromMarkdownPageRefHref, toMarkdownPageRefHref };
