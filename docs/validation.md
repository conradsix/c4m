# Validation

The engine checks every new block against its block type's JSON Schema, and every menu item against its action type's,
whether the write comes through an endpoint or a service. Block and menu action schemas are what zod 4's
`z.toJSONSchema` emits, so the validator implements the part of JSON Schema 2020-12 zod uses, and nothing else.

## The dialect

**Validation keywords:**

- `type` (one type or several), `const`, `enum`;
- `properties`, `required`, `additionalProperties`, `patternProperties`, `propertyNames`;
- `items` (one schema), `prefixItems`, `minItems`, `maxItems`;
- `minLength`, `maxLength`, `pattern`;
- `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`, `multipleOf`;
- `allOf`, `anyOf`, `oneOf`, `not`;
- `$ref` to a JSON Pointer within the same schema, and `$defs`.

**Annotations:** every other keyword JSON Schema doesn't define as a validation keyword, such as `title`,
`description`, `default` and `examples`. `format` is an annotation too, except `uri`: zod emits a `pattern` beside each
of the other formats, and that pattern is checked.

**Refused:** registering a type fails, as an invalid request, if its schema uses any of these, so a rule is never
silently skipped:

- `$anchor`, `$dynamicRef`, `$dynamicAnchor`, `$recursiveRef`, `$recursiveAnchor`, `$vocabulary`, and a nested `$id`;
- a `$ref` outside the schema;
- `contains`, `minContains`, `maxContains`, `uniqueItems`, `minProperties`, `maxProperties`;
- `dependentRequired`, `dependentSchemas`, `dependencies`, `if`, `then`, `else`;
- `unevaluatedItems`, `unevaluatedProperties`, `additionalItems`, and the array form of `items`;
- a `$schema` other than 2020-12;
- a keyword whose value has the wrong shape, such as `properties` that isn't an object;
- a schema that comes back to itself without moving into the data, such as `{"$ref": "#"}`;
- a pattern .NET would read differently from JavaScript: a letter escape such as `\p{...}` or `\A`, a `(?` group
  other than `(?:`, a lookaround or `(?<name>`, an empty class, or `\S` inside a class.

## JavaScript semantics

Stored data is zod's output, and hosts read it back with zod, so the validator reads numbers and patterns as JavaScript
does.

- A number is the double `JSON.parse` makes of it: `1e-29` isn't `0` or an integer, `0.10000000000000000001` isn't
  greater than `0.1`, and `2.0` is an integer. A number too big for a double is refused, as zod refuses `Infinity`.
- `multipleOf` is exact on the decimal digits JavaScript prints, as zod's is.
- A pattern is translated to a .NET one with its JavaScript meaning: `\d`, `\w` and `\b` are ASCII, `\s` includes
  Unicode spaces such as U+00A0, `.` stops at every line terminator, and `$` is the end of the input. Patterns run
  with a one-second match timeout.
- zod drops a regular expression's flags when it emits a schema, so a pattern written with `i` or `u` is checked
  without them. Write patterns that don't need flags.

## Details

- `uri` means RFC 3986, which is stricter than zod's `z.url()` about spaces and non-ASCII characters.
- Each schema is compiled once and cached.

## Errors

Endpoint errors are keyed `blocks[i].{type}.#/{JSON Pointer}`, with a kind such as `PropertyRequired`,
`ConstMismatch` or `PatternMismatch`. Menu item errors are keyed the same way, with the action type's name.

## How it's tested

- The JSON Schema Test Suite's 2020-12 cases for every supported keyword, in
  `dotnet/test/ConradSix.C4M.Tests/assets/json-schema-test-suite`.
- A parity fixture that `@conradsix/c4m-schema` writes from zod itself: the schemas zod emits, and which samples zod
  accepts as stored data. Stored data is zod's output, so a sample is valid only if zod parses it and returns it
  unchanged. The .NET tests must agree with every verdict, so a zod release that changes either shows up in both.
