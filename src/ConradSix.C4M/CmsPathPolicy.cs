namespace Parapet.CMS.Engine;

using Microsoft.Extensions.Options;

public sealed class CmsPathPolicy(IOptions<CmsEngineOptions> options)
{
    public bool IsReserved(string path)
    {
        var rootSegment = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return rootSegment is not null && options.Value.ReservedRootSegments.Contains(rootSegment);
    }
}
