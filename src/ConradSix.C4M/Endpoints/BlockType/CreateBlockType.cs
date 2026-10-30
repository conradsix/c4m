namespace ConradSix.C4M.Endpoints.BlockType;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Endpoints.Base;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class CreateBlockType(CmsEngineDbContext ctx, ILogger<CreateBlockType> logger)
    : CreateCmsTypeDescriptorBase<BlockTypeEntity, GetBlockType>(ctx, logger)
{
    protected override string EntityTypeName => "block type";

    protected override string RoutePrefix => "block-types";

    protected override DbSet<BlockTypeEntity> EntitySet => this.Ctx.BlockTypes;

    public override void Configure()
    {
        base.Configure();
        this.Summary(s =>
        {
            s.ExampleRequest = new CreateCmsTypeDescriptorRequest(
                Name: "hero-section",
                Version: 1,
                JsonSchema: /*lang=json,strict*/ """{"type": "object", "properties": {"title": {"type": "string"}, "subtitle": {"type": "string"}}}""");
        });
    }

    protected override BlockTypeEntity CreateEntity(CreateCmsTypeDescriptorRequest request) =>
        new()
        {
            Name = request.Name,
            Version = request.Version,
            JsonSchema = request.JsonSchema,
        };
}
