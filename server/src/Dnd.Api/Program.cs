using Dnd.Api.Endpoints;
using Dnd.Application;
using Dnd.Infrastructure;
using Dnd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "D&D Companion API",
        Version = "v1",
        Description = "API del companion para campañas de D&D 5e.",
    });
});

builder.Services
    .AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString(Dnd.Infrastructure.DependencyInjection.DefaultConnectionName)!,
        name: "postgres",
        tags: ["ready"]);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

// Liveness: the process is up. Readiness: dependencies (PostgreSQL) answer.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapAppEndpoints();

await app.RunAsync();

/// <summary>Exposed so integration tests can bootstrap the host with WebApplicationFactory.</summary>
public partial class Program;
