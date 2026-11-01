using ConradSix.C4M;
using ConradSix.C4M.SampleHost.OpenApi;

using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddC4M(c4m => c4m.UsePostgres(provider => provider.GetRequiredService<IConfiguration>().GetConnectionString("C4M")
        ?? throw new InvalidOperationException("Connection string 'ConnectionStrings:C4M' is required.")))
    .AddAuthorization()
    .AddFastEndpoints(options =>
    {
        options.DisableAutoDiscovery = true;
        options.Assemblies = [C4MEndpoints.Assembly];
    })
    .SwaggerDocument(options =>
    {
        options.ShortSchemaNames = true;
        options.DocumentSettings = settings =>
        {
            settings.DocumentName = "c4m";
            settings.Title = "C4M";
            settings.Description = "The C4M engine's endpoints: pages and their versions, block and menu action types, and menus.";
            settings.Version = "v1";
            settings.SchemaSettings.SchemaProcessors.Add(new GuidSchemaProcessor());
            settings.DocumentProcessors.Add(new RequiredFromNullabilityProcessor());
        };
    });

var app = builder.Build();

app
    .UseAuthorization()
    .UseFastEndpoints(config =>
    {
        // The sample leaves every endpoint open. A real host authorizes the editor's endpoints apart from the ones a
        // public site reads, for example with editor: e => e.Policies("CmsEditor").
        config.Endpoints.Configurator = endpoint => endpoint.ConfigureCmsEndpoint(
            editor: e => e.AllowAnonymous(),
            delivery: e => e.AllowAnonymous());

        // Operation ids are the endpoint names, which the client's method names come from.
        config.Endpoints.NameGenerator = context => context.EndpointType.Name;
    })
    .UseSwaggerGen();

// With --export-swagger-docs true, writes the OpenAPI document and exits; the client is generated from it.
await app.ExportSwaggerDocsAndExitAsync("c4m");

await app.MigrateC4MAsync();
await app.RunAsync();
