using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Infrastructure.Auth;
using Dnd.Infrastructure.Campaigns;
using Dnd.Infrastructure.Email;
using Dnd.Infrastructure.Files;
using Dnd.Infrastructure.Options;
using Dnd.Infrastructure.Persistence;
using Dnd.Infrastructure.Persistence.Repositories;
using Dnd.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dnd.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DefaultConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{DefaultConnectionName}' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPasswordTokenRepository, PasswordTokenRepository>();
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<ICampaignAccess, CampaignAccess>();

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.PublicUrl, UriKind.Absolute, out _), "App:PublicUrl must be an absolute URL.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.Secret.Length >= JwtOptions.MinSecretLength, $"Jwt:Secret must be at least {JwtOptions.MinSecretLength} characters long.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => o.AccessTokenMinutes > 0 && o.RefreshTokenDays > 0, "Jwt token lifetimes must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IAccountEmailService, AccountEmailService>();

        return services;
    }
}
