namespace ConradSix.C4M.Endpoints.MenuActionType;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Endpoints.Base;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class CreateMenuActionType(CmsEngineDbContext ctx, ILogger<CreateMenuActionType> logger)
    : CreateCmsTypeDescriptorBase<MenuActionTypeEntity, GetMenuActionType>(ctx, logger)
{
    protected override string EntityTypeName => "menu action type";

    protected override string RoutePrefix => "menu-action-types";

    protected override DbSet<MenuActionTypeEntity> EntitySet => this.Ctx.MenuActionTypes;

    public override void Configure()
    {
        base.Configure();
        this.Summary(s =>
        {
            s.ExampleRequest = new CreateCmsTypeDescriptorRequest(
                Name: "page-link",
                Version: 1,
                JsonSchema: /*lang=json,strict*/ """{"type": "object", "properties": {"pageId": {"type": "string", "format": "uuid"}}, "required": ["pageId"]}""");
        });
    }

    protected override MenuActionTypeEntity CreateEntity(CreateCmsTypeDescriptorRequest request) =>
        new()
        {
            Name = request.Name,
            Version = request.Version,
            JsonSchema = request.JsonSchema,
        };
}
