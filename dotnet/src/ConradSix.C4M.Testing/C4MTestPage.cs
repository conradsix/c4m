namespace ConradSix.C4M.Testing;

// A page C4MTestData added. CurrentVersionId is set when it was added with a version.
public sealed record C4MTestPage(Guid Id, string Slug, string Path, Guid? CurrentVersionId);
