using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Infrastructure.Auth;
using Dnd.Infrastructure.Campaigns;
using Dnd.Infrastructure.Catalog;
using Dnd.Infrastructure.Email;
using Dnd.Infrastructure.Files;
using Dnd.Infrastructure.Options;
using Dnd.Infrastructure.Persistence;
using Dnd.Infrastructure.Persistence.Repositories;
using Dnd.Infrastructure.Sessions;
using Dnd.Infrastructure.Time;
using Dnd.Domain.Sessions;
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
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddScoped<IChangeRequestRepository, ChangeRequestRepository>();
        services.AddScoped<IRestRequestRepository, RestRequestRepository>();
        services.AddScoped<IItemTemplateRepository, ItemTemplateRepository>();
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IPartyStashRepository, PartyStashRepository>();
        services.AddScoped<IDirectMessageRepository, DirectMessageRepository>();
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<ILoreRepository, LoreRepository>();
        services.AddScoped<IMapRepository, MapRepository>();
        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IReleaseRepository, ReleaseRepository>();
        services.AddScoped<IInstanceStatsRepository, InstanceStatsRepository>();
        services.AddScoped<ISrdSeeder, SrdSeeder>();
        services.AddScoped<IContentPackImporter, ContentPackImporter>();
        services.AddScoped<SystemDocumentSeeder>();

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<CatalogOptions>(configuration.GetSection(CatalogOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .Validate(o => IsValidPublicUrl(o.PublicUrl), "App:PublicUrl is required and must be an absolute http(s) URL without path, for example https://dnd.example.com.")
            .Validate(o => CampaignSchedule.IsValidTimeZone(o.DefaultTimeZone), "App:DefaultTimeZone must be a valid IANA time zone id, for example Europe/Madrid.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.Secret.Length >= JwtOptions.MinSecretLength, $"Jwt:Secret must be at least {JwtOptions.MinSecretLength} characters long.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => o.AccessTokenMinutes > 0 && o.RefreshTokenDays > 0, "Jwt token lifetimes must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IFileStorage, DiskFileStorage>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<PublicLinkBuilder>();
        services.AddScoped<IAccountEmailService, AccountEmailService>();
        services.AddSingleton<ISessionLinkTokens, SessionLinkTokens>();
        services.AddScoped<ISessionEmailService, SessionEmailService>();
        services.AddSingleton<ICampaignDefaults, CampaignDefaults>();

        return services;
    }

    private static bool IsValidPublicUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value.Trim().TrimEnd('/'), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.UserInfo.Length == 0
        && !string.IsNullOrEmpty(uri.Host)
        && uri.AbsolutePath == "/"
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0;
}
