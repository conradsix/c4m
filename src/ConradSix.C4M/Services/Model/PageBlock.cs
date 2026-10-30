#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1402 // File may only contain a single type
namespace Parapet.CMS.Engine.Services.Model;

public record PageBlock(Guid? BlockId, Guid? BlockTypeId, string? BlockData, Guid? InstanceId = null)
{
    public bool IsNew => this.BlockTypeId.HasValue && this.BlockData is not null;

    public bool IsExisting => this.BlockId.HasValue;

    public INewBlock ToNewBlock() => this.IsNew
        ? (INewBlock)new NewBlock(this.BlockTypeId!.Value, this.BlockData!)
        : throw new InvalidOperationException("Cannot convert to NewBlock");

    public IExistingBlock ToExistingBlock() => this.IsExisting
        ? (IExistingBlock)new ExistingBlock(this.BlockId!.Value)
        : throw new InvalidOperationException("Cannot convert to ExistingBlock");
}

public interface IExistingBlock
{
    Guid BlockId { get; }
}

public interface INewBlock
{
    Guid BlockTypeId { get; }

    string BlockData { get; }
}

public record NewBlock(Guid BlockTypeId, string BlockData) : INewBlock;

public record ExistingBlock(Guid BlockId) : IExistingBlock;
