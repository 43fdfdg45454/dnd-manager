using Dnd.Api.Auth;
using Dnd.Api.Endpoints;
using Dnd.Api.Errors;
using Dnd.Api.Hosting;
using Dnd.Application;
using Dnd.Application.Abstractions;
using Dnd.Application.Common;
using Dnd.Infrastructure;
using Dnd.Infrastructure.Files;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Console logging with Serilog (JSON outside Development). The default console provider is removed so
// events are not printed twice; providers added later (for example by tests) still receive the events.
builder.Logging.ClearProviders();
builder.Host.UseSerilog((context, _, logger) => LoggingSetup.Configure(context, logger), writeToProviders: true);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The public URL is decided by the reverse proxy of the operator: the API learns it from the (trusted)
// X-Forwarded-* headers and the Host of the requests, and remembers the last one for background services.
builder.Services.AddAppForwardedHeaders(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<PublicOriginStore>();
builder.Services.AddSingleton<IPublicUrlProvider, PublicUrlProvider>();

// Uploads: Kestrel's default body limit (30 MB) and the multipart limit (128 MB) follow
// FileStorage:MaxUploadMegabytes (plus room for the rest of the multipart body).
builder.Services.AddOptions<KestrelServerOptions>().Configure<IOptions<FileStorageOptions>>((kestrel, files) =>
    kestrel.Limits.MaxRequestBodySize = files.Value.MaxUploadMegabytes * 1024L * 1024L + FileEndpoints.MultipartOverheadBytes);
builder.Services.AddOptions<FormOptions>().Configure<IOptions<FileStorageOptions>>((form, files) =>
    form.MultipartBodyLengthLimit = files.Value.MaxUploadMegabytes * 1024L * 1024L + FileEndpoints.MultipartOverheadBytes);

// Background sender of the session reminders (Reminders:Enabled / Reminders:PollSeconds).
builder.Services.AddOptions<ReminderOptions>()
    .Bind(builder.Configuration.GetSection(ReminderOptions.SectionName))
    .Validate(o => o.PollSeconds >= 1, "Reminders:PollSeconds must be at least 1.")
    .ValidateOnStart();
builder.Services.AddHostedService<ReminderDispatcher>();

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
    options.MapType<Optional<int?>>(() => new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int32" });
    options.MapType<Optional<bool?>>(() => new OpenApiSchema { Type = JsonSchemaType.Boolean | JsonSchemaType.Null });
    options.MapType<Optional<decimal?>>(() => new OpenApiSchema { Type = JsonSchemaType.Number | JsonSchemaType.Null, Format = "double" });
    options.MapType<Optional<IReadOnlyList<string>?>>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.Array | JsonSchemaType.Null,
        Items = new OpenApiSchema { Type = JsonSchemaType.String },
    });

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

// First of all, so everything below (logging, links in emails) sees the scheme and host the client used.
app.UseForwardedHeaders();
app.UsePublicOriginTracking();

app.UseRequestCorrelation();
app.UseSerilogRequestLogging(options => options.GetLevel = LoggingSetup.RequestLevel);

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
app.MapAdminReleaseEndpoints();
app.MapUserEndpoints();
app.MapCampaignEndpoints();
app.MapCatalogEndpoints();
app.MapCharacterEndpoints();
app.MapChangeRequestEndpoints();
app.MapItemEndpoints();
app.MapInventoryEndpoints();
app.MapShopEndpoints();
app.MapFileEndpoints();
app.MapLoreEndpoints();
app.MapMapEndpoints();
app.MapLibraryEndpoints();
app.MapSessionEndpoints();
app.MapPublicSessionEndpoints();
app.MapPageEndpoints();

await app.RunAsync();

/// <summary>Exposed so integration tests can bootstrap the host with WebApplicationFactory.</summary>
public partial class Program;
