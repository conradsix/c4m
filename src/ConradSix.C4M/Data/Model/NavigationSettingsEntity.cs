namespace ConradSix.C4M.Data.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore.Infrastructure;

internal sealed class NavigationSettingsEntity : EntityBase
{
    public static readonly Guid SingletonId = Guid.Parse("7d75d24a-82ed-4aeb-9096-d4804b29a5bf");

    private readonly ILazyLoader? _lazyLoader;

    public NavigationSettingsEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private NavigationSettingsEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid? DefaultMenuId { get; set; }

    public Guid? FooterMenuId { get; set; }

    [Timestamp]
    public uint Version { get; set; }

    [ForeignKey(nameof(DefaultMenuId))]
    public MenuEntity? DefaultMenu
    {
        get => this._lazyLoader?.Load(this, ref field) ?? field;
        set;
    }

    [ForeignKey(nameof(FooterMenuId))]
    public MenuEntity? FooterMenu
    {
        get => this._lazyLoader?.Load(this, ref field) ?? field;
        set;
    }
}
