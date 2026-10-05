using System.Text;
using Harekat.Application.Abstractions;
using Harekat.Domain.Entities;
using Harekat.Infrastructure.Auth;
using Harekat.Infrastructure.Persistence;
using Harekat.Infrastructure.Repositories;
using Harekat.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
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

        var provider = config["Storage:Provider"] ?? "Memory";

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "Postgres", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<HarekatDbContext>(opt =>
            {
                if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    var cs = config.GetConnectionString("Sqlite") ?? "Data Source=harekat.db";
                    opt.UseSqlite(cs);
                }
                else
                {
                    var cs = config.GetConnectionString("Postgres")
                             ?? config.GetConnectionString("Default")
                             ?? "Host=localhost;Database=harekat;Username=harekat;Password=harekat";
                    opt.UseNpgsql(cs);
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
        var provider = config["Storage:Provider"] ?? "Memory";
        if (provider is "Sqlite" or "Postgres" or "PostgreSQL")
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HarekatDbContext>();
            await db.Database.EnsureCreatedAsync();
            if (!await db.Seasons.AnyAsync())
            {
                db.Seasons.Add(new Season
                {
                    Number = 1,
                    Name = "Sezon 1 — Kuzgun Vadisi",
                    StartsAt = DateTimeOffset.UtcNow.AddDays(-7),
                    EndsAt = DateTimeOffset.UtcNow.AddDays(83)
                });
                await db.SaveChangesAsync();
            }
        }
    }
}
