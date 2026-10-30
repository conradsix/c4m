namespace Parapet.CMS.Engine;

using System.Text.Json;

// Business rules beyond the block type's JSON Schema. Throw CmsBlockValidationException to reject a block.
public interface IBlockValidator
{
    void Validate(string blockType, JsonElement block);
}
