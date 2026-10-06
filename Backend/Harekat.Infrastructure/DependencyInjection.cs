using System.Text;
using Harekat.Application.Abstractions;
using Harekat.Domain.Entities;
using Harekat.Infrastructure.Auth;
using Harekat.Infrastructure.Persistence;
using Harekat.Infrastructure.Repositories;
using Harekat.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Harekat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IEmailService, FakeEmailService>();
        services.AddHttpClient<ISteamTicketValidator, SteamTicketValidator>();

        // Varsayılan: SqlServer (üretim). Memory/Sqlite yerel/test.
        var provider = config["Storage:Provider"] ?? "SqlServer";

        if (IsEfProvider(provider))
        {
            services.AddDbContext<HarekatDbContext>(opt =>
            {
                if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    var cs = config.GetConnectionString("Sqlite") ?? "Data Source=harekat.db";
                    opt.UseSqlite(cs);
                }
                else if (IsSqlServerProvider(provider))
                {
                    var cs = config.GetConnectionString("SqlServer")
                             ?? config.GetConnectionString("Default")
                             ?? "Server=localhost;Database=Harekat;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
                    opt.UseSqlServer(cs, sql =>
                        sql.MigrationsAssembly(typeof(HarekatDbContext).Assembly.FullName));
                }
                else if (IsPostgresProvider(provider))
                {
                    throw new InvalidOperationException(
                        "PostgreSQL/Npgsql kaldırıldı. Storage:Provider=SqlServer kullanın. " +
                        "İsteğe bağlı Postgres için EnablePostgres derleme bayrağı ve Npgsql paketi gerekir.");
                }
                else
                {
                    throw new InvalidOperationException($"Bilinmeyen Storage:Provider '{provider}'. SqlServer | Sqlite | Memory.");
                }
            });

            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
            services.AddScoped<IPlayerRepository, EfPlayerRepository>();
            services.AddScoped<ISquadRepository, EfSquadRepository>();
            services.AddScoped<IMatchTicketRepository, EfMatchTicketRepository>();
            services.AddScoped<IMatchRepository, EfMatchRepository>();
            services.AddScoped<IGameServerRepository, EfGameServerRepository>();
            services.AddScoped<IFriendshipRepository, EfFriendshipRepository>();
            services.AddScoped<ISeasonRepository, EfSeasonRepository>();
            services.AddScoped<IModerationRepository, EfModerationRepository>();
            services.AddScoped<INewsRepository, EfNewsRepository>();
            services.AddScoped<IClientVersionRepository, EfClientVersionRepository>();
        }
        else
        {
            var jsonPath = config["Storage:JsonPath"];
            if (string.IsNullOrWhiteSpace(jsonPath))
                jsonPath = null;
            services.AddSingleton(new InMemoryStore(jsonPath));
            services.AddSingleton<IUnitOfWork, MemoryUnitOfWork>();
            services.AddSingleton<IPlayerRepository, MemoryPlayerRepository>();
            services.AddSingleton<ISquadRepository, MemorySquadRepository>();
            services.AddSingleton<IMatchTicketRepository, MemoryMatchTicketRepository>();
            services.AddSingleton<IMatchRepository, MemoryMatchRepository>();
            services.AddSingleton<IGameServerRepository, MemoryGameServerRepository>();
            services.AddSingleton<IFriendshipRepository, MemoryFriendshipRepository>();
            services.AddSingleton<ISeasonRepository, MemorySeasonRepository>();
            services.AddSingleton<IModerationRepository, MemoryModerationRepository>();
            services.AddSingleton<INewsRepository, MemoryNewsRepository>();
            services.AddSingleton<IClientVersionRepository, MemoryClientVersionRepository>();
        }

        var secret = config["Jwt:Secret"] ?? "HarekatDevSecretKey_ChangeInProduction_Min32Chars!";
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"] ?? "harekat",
                    ValidAudience = config["Jwt:Audience"] ?? "harekat-clients",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                // SignalR JWT via query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Moderator", p => p.RequireRole("Moderator", "Admin"));
            options.AddPolicy("Admin", p => p.RequireRole("Admin"));
            options.AddPolicy("Server", p => p.RequireAssertion(_ => true)); // validated via header in endpoint
        });

        return services;
    }

    public static async Task EnsureStorageAsync(this IServiceProvider sp, IConfiguration config)
    {
        var provider = config["Storage:Provider"] ?? "SqlServer";
        if (!IsEfProvider(provider))
            return;

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HarekatDbContext>();

        if (IsSqlServerProvider(provider))
        {
            // EnsureCreated ile açılmış DB'lerde __EFMigrationsHistory boş kalır.
            // Pending migration varken MigrateAsync CREATE TABLE dener → 2714 (object exists).
            // History yoksa EnsureCreated (mevcut şemaya dokunmaz); history varsa Migrate.
            var applied = await db.Database.GetAppliedMigrationsAsync();
            if (applied.Any())
                await db.Database.MigrateAsync();
            else
                await db.Database.EnsureCreatedAsync();
        }
        else
        {
            // Sqlite test/dev: EnsureCreated yeterli.
            await db.Database.EnsureCreatedAsync();
            try
            {
                _ = await db.Seasons.AnyAsync();
            }
            catch
            {
                var creator = (RelationalDatabaseCreator)db.GetService<IDatabaseCreator>()!;
                await creator.CreateTablesAsync();
            }
        }

        if (!await db.Seasons.AnyAsync())
        {
            db.Seasons.Add(new Season
            {
                Number = 1,
                Name = "Sezon 1 — Kuzgun İnişi",
                StartsAt = DateTimeOffset.Parse("2026-11-02T00:00:00+00:00"),
                EndsAt = DateTimeOffset.Parse("2027-01-11T00:00:00+00:00")
            });
            await db.SaveChangesAsync();
        }
    }

    private static bool IsSqlServerProvider(string provider) =>
        string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, "SQLServer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, "MSSQL", StringComparison.OrdinalIgnoreCase);

    private static bool IsPostgresProvider(string provider) =>
        string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase);

    private static bool IsEfProvider(string provider) =>
        string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase) ||
        IsSqlServerProvider(provider) ||
        IsPostgresProvider(provider);
}
