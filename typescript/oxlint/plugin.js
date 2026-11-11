import { basename, extname } from "node:path";

const plugin = {
  meta: { name: "c4m" },
  rules: {
    "no-restricted-syntax": {
      meta: {
        type: "problem",
        schema: {
          type: "array",
          items: {
            anyOf: [
              { type: "string" },
              {
                type: "object",
                properties: { selector: { type: "string" }, message: { type: "string" } },
                required: ["selector", "message"],
                additionalProperties: false,
              },
            ],
          },
        },
      },
      create: (context) =>
        Object.fromEntries(
          context.options.map((restriction) => {
            const selector = typeof restriction === "string" ? restriction : restriction.selector;
            const message =
              typeof restriction === "string"
                ? `Use an arrow function instead of ${restriction}.`
                : restriction.message;
            return [selector, (node) => context.report({ node, message })];
          }),
        ),
    },
    "filename-pattern": {
      meta: {
        type: "suggestion",
        schema: [{ type: "string" }],
      },
      create: (context) => ({
        Program: (node) => {
          const name = basename(context.filename, extname(context.filename));
          const pattern = context.options[0];
          if (!new RegExp(pattern, "u").test(name)) {
            context.report({ node, message: `Filename must match ${pattern}.` });
          }
        },
      }),
    },
  },
};

export default plugin;
