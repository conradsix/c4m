namespace Parapet.CMS.Engine.Data.Model;

// The context stamps these when it saves.
internal interface IHasTimestamps : IHasCreatedAt, IHasUpdatedAt
{
}

internal interface IHasCreatedAt
{
    DateTimeOffset CreatedAt { get; set; }
}

internal interface IHasUpdatedAt
{
    DateTimeOffset UpdatedAt { get; set; }
}
