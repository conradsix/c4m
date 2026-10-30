namespace Parapet.CMS.Engine.Data.Model;

// The context gives an added entity without an id a new one.
internal interface IHasGuidId
{
    Guid Id { get; set; }
}
