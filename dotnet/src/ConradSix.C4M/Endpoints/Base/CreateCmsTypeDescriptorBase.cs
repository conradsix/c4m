#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Base;

using System.Diagnostics;

using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Validation;

using FastEndpoints;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

internal abstract class CreateCmsTypeDescriptorBase<TEntity, TGetEndpoint>(
        CmsEngineDbContext ctx,
        ILogger logger)
    : Endpoint<CreateCmsTypeDescriptorRequest, CreateCmsTypeDescriptorResponse>
    where TEntity : class, IHasGuidId, IHasCreatedAt, new()
    where TGetEndpoint : IEndpoint
{
    protected abstract string EntityTypeName { get; }

    protected abstract string RoutePrefix { get; }

    protected CmsEngineDbContext Ctx { get; } = ctx;

    protected abstract DbSet<TEntity> EntitySet { get; }

    public override void Configure()
    {
        this.Post($"/{this.RoutePrefix}");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status409Conflict));
        this.Summary(s =>
        {
            s.Summary = $"Creates a new {this.EntityTypeName}.";
            s.Description = $"Adds a new {this.EntityTypeName} to the system.";
            s.Response<CreateCmsTypeDescriptorResponse>(StatusCodes.Status201Created, $"{this.EntityTypeName} was successfully created.");
            s.Response(StatusCodes.Status400BadRequest, "The request is invalid.");
            s.Response(StatusCodes.Status409Conflict, $"A {this.EntityTypeName} with the same name and version already exists.");
        });
    }

    public override async Task HandleAsync(CreateCmsTypeDescriptorRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var entity = this.CreateEntity(req);
            this.EntitySet.Add(entity);

            try
            {
                await this.Ctx.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                var constraintName = ex.GetUniqueConstraintName();

                if (constraintName?.Contains("name", StringComparison.OrdinalIgnoreCase) == true &&
                    constraintName?.Contains("version", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.AddError($"A {this.EntityTypeName} with the name '{req.Name}' and version '{req.Version}' already exists.");
                }
                else
                {
                    this.AddError($"A {this.EntityTypeName} with the same {nameof(req.Name)} ({req.Name}) and {nameof(req.Version)} ({req.Version}) already exists.");
                }

                logger.CreateCmsTypeDescriptorConflict(this.EntityTypeName, req.Name);
                await this.Send.ErrorsAsync(statusCode: StatusCodes.Status409Conflict, ct);
                return;
            }

            await this.Send.CreatedAtAsync<TGetEndpoint>(
                new { entity.Id },
                new CreateCmsTypeDescriptorResponse(entity.Id),
                cancellation: ct);
            logger.CreateCmsTypeDescriptorCompleted(this.EntityTypeName, entity.Id, req.Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, this.GetType().Name);
            throw;
        }
    }

    protected abstract TEntity CreateEntity(CreateCmsTypeDescriptorRequest request);
}

internal class CreateCmsTypeDescriptorRequestValidator
    : Validator<CreateCmsTypeDescriptorRequest>
{
    public CreateCmsTypeDescriptorRequestValidator()
    {
        this.RuleFor(r => r.Name)
            .NotEmpty()
            .Matches(@"^[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]$")
            .WithMessage("Name must contain only alphanumeric characters and hyphens, and cannot start or end with a hyphen");
        this.RuleFor(r => r.Version)
            .GreaterThan(0)
            .WithMessage("Version must be greater than 0");

        // A schema the engine can't check in full is refused, so stored data never skips a keyword.
        this.RuleFor(r => r.JsonSchema)
            .NotEmpty()
            .Custom((jsonSchema, context) =>
            {
                if (!string.IsNullOrEmpty(jsonSchema) && !this.Resolve<CmsJsonSchemaCache>().TryGet(jsonSchema, out _, out var error))
                {
                    context.AddFailure(error);
                }
            });
    }
}

public record CreateCmsTypeDescriptorResponse(Guid Id);

public record CreateCmsTypeDescriptorRequest(
    string Name,
    int Version,
    string JsonSchema);
