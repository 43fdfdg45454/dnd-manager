using Dnd.Api.Auth;
using Dnd.Api.Endpoints;
using Dnd.Api.Errors;
using Dnd.Api.Hosting;
using Dnd.Application;
using Dnd.Application.Common;
using Dnd.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddJwtAuthentication();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "D&D Companion API",
        Version = "v1",
        Description = "API del companion para campañas de D&D 5e.",
    });

    // Optional<T> request fields are plain (nullable) values on the wire.
    options.MapType<Optional<string?>>(() => new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null });
    options.MapType<Optional<Guid?>>(() => new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Format = "uuid" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token obtenido en POST /api/v1/auth/login.",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

builder.Services
    .AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString(Dnd.Infrastructure.DependencyInjection.DefaultConnectionName)!,
        name: "postgres",
        tags: ["ready"]);

var app = builder.Build();

// Migrations first, then the SRD catalog import and the initial admin bootstrap.
await app.InitializeAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// Liveness: the process is up. Readiness: dependencies (PostgreSQL) answer.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapAppEndpoints();
app.MapAuthEndpoints();
app.MapAdminUserEndpoints();
app.MapUserEndpoints();
app.MapCampaignEndpoints();
app.MapCatalogEndpoints();
app.MapCharacterEndpoints();
app.MapChangeRequestEndpoints();
app.MapPageEndpoints();

await app.RunAsync();

/// <summary>Exposed so integration tests can bootstrap the host with WebApplicationFactory.</summary>
public partial class Program;
