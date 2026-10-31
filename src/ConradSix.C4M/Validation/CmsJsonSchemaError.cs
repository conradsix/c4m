namespace ConradSix.C4M.Validation;

// Where data fails its schema: a JSON Pointer fragment ("#", "#/items/0/title") and the kind of failure. With no path,
// the failure is about the data as a whole, and Kind says why in words.
internal readonly record struct CmsJsonSchemaError(string? Path, string Kind);
