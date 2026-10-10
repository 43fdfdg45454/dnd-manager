using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Catalog;
using OpenTrpg.Core.Infrastructure.Auth;
using OpenTrpg.Core.Infrastructure.Campaigns;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Core.Infrastructure.Email;
using OpenTrpg.Core.Infrastructure.Files;
using OpenTrpg.Core.Infrastructure.Options;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Core.Infrastructure.Persistence.Repositories;
using OpenTrpg.Core.Infrastructure.Sessions;
using OpenTrpg.Core.Infrastructure.Time;
using OpenTrpg.Core.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Application.Systems.Dnd5e;

namespace OpenTrpg.Core.Infrastructure;

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
        services.AddScoped<IDnd5eCharacterRepository, Dnd5eCharacterRepository>();
        services.AddScoped<IChangeRequestRepository, ChangeRequestRepository>();
        services.AddScoped<IRestRequestRepository, RestRequestRepository>();
        services.AddScoped<ICharacterCompanionRepository, CharacterCompanionRepository>();
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
        services.AddScoped<IContentPackImporter, ContentPackRegistry>();
        services.AddScoped<IDnd5eCatalogSystem, Dnd5eCatalogSystem>();
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
        services.AddSingleton<IBeastCatalog, SrdBeastCatalog>();

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
