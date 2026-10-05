using Harekat.Application.Dtos;
using Harekat.Application.Services;
using Harekat.Domain.Catalogs;
using Harekat.Infrastructure.Repositories;
using Harekat.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Harekat.Tests.Application;

public class EdgeCaseTests
{
    [Fact]
    public async Task MatchResult_RejectsDoubleSubmit()
    {
        var store = new InMemoryStore();
        var players = new MemoryPlayerRepository(store);
        var squadsRepo = new MemorySquadRepository(store);
        var tickets = new MemoryMatchTicketRepository(store);
        var matches = new MemoryMatchRepository(store);
        var serversRepo = new MemoryGameServerRepository(store);
        var seasons = new MemorySeasonRepository(store);
        var mod = new MemoryModerationRepository(store);
        var uow = new MemoryUnitOfWork(store);
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = new Harekat.Infrastructure.Auth.JwtTokenService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "HarekatDevSecretKey_ChangeInProduction_Min32Chars!"
            }).Build());
        var auth = new AuthService(players, hasher, jwt, new FakeEmailService(NullLogger<FakeEmailService>.Instance),
            mod, uow, NullLogger<AuthService>.Instance);
        var squads = new SquadService(squadsRepo, players, uow, NullLogger<SquadService>.Instance);
        var mm = new MatchmakingService(tickets, squadsRepo, players, matches, serversRepo, seasons, uow, NullLogger<MatchmakingService>.Instance);
        var results = new MatchResultService(matches, players, squadsRepo, serversRepo, seasons, uow, new AchievementService(), NullLogger<MatchResultService>.Instance);
        var servers = new GameServerService(serversRepo, hasher, uow);

        var a = await auth.RegisterAsync(new RegisterRequest("EdgeCase1", "edge@t.com", "password123"));
        await squads.CreateAsync(a.Player.Id, new CreateSquadRequest("Echo", "tr"));
        var server = await servers.RegisterAsync(new RegisterServerRequest("10.0.0.1", 7777, "tr", "server-key-edgecase1"));
        await mm.EnqueueAsync(a.Player.Id, new QueueRequest("tr"));
        foreach (var t in store.Tickets.Values)
            t.EnqueuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var match = await mm.TryFormMatchesAsync("tr");
        match.Should().NotBeNull();
        var team = match!.Teams.First(t => t.SquadId != Guid.Empty);
        var req = new MatchResultRequest([
            new TeamMatchResultDto(team.SquadId, 2, [new PlayerMatchResultDto(a.Player.Id, 1, 0, 100, 60)])
        ]);
        await results.SubmitResultAsync(match.Id, req, server.Id);
        var act = () => results.SubmitResultAsync(match.Id, req, server.Id);
        await act.Should().ThrowAsync<Harekat.Application.Common.AppException>().Where(e => e.StatusCode == 409);
    }

    [Fact]
    public async Task Ban_BlocksLogin()
    {
        var store = new InMemoryStore();
        var players = new MemoryPlayerRepository(store);
        var uow = new MemoryUnitOfWork(store);
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = new Harekat.Infrastructure.Auth.JwtTokenService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "HarekatDevSecretKey_ChangeInProduction_Min32Chars!"
            }).Build());
        var modRepo = new MemoryModerationRepository(store);
        var auth = new AuthService(players, hasher, jwt, new FakeEmailService(NullLogger<FakeEmailService>.Instance),
            modRepo, uow, NullLogger<AuthService>.Instance);
        var moderation = new ModerationService(modRepo, players, uow);

        var admin = await auth.RegisterAsync(new RegisterRequest("AdminUser", "admin@t.com", "password123"));
        var victim = await auth.RegisterAsync(new RegisterRequest("VictimUsr", "vic@t.com", "password123"));

        // elevate admin
        var adminEntity = await players.GetByIdAsync(admin.Player.Id);
        adminEntity!.Role = "Admin";
        await players.UpdateAsync(adminEntity);
        await uow.SaveChangesAsync();

        await moderation.BanAsync(admin.Player.Id, new BanPlayerRequest(victim.Player.Id, "Hile", 24));
        var act = () => auth.LoginAsync(new LoginRequest("VictimUsr", "password123"));
        await act.Should().ThrowAsync<Harekat.Application.Common.AppException>().Where(e => e.ErrorCode == "banned");
    }

    [Fact]
    public void CosmeticCatalog_HasCamoAndBeret()
    {
        CosmeticCatalog.All.Should().Contain(c => c.Slot == "camo");
        CosmeticCatalog.All.Should().Contain(c => c.Slot == "beret");
        CosmeticCatalog.All.Count(c => c.UnlockXp == 0).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task NonLeader_CannotQueue()
    {
        var store = new InMemoryStore();
        var players = new MemoryPlayerRepository(store);
        var squadsRepo = new MemorySquadRepository(store);
        var tickets = new MemoryMatchTicketRepository(store);
        var matches = new MemoryMatchRepository(store);
        var serversRepo = new MemoryGameServerRepository(store);
        var seasons = new MemorySeasonRepository(store);
        var mod = new MemoryModerationRepository(store);
        var uow = new MemoryUnitOfWork(store);
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = new Harekat.Infrastructure.Auth.JwtTokenService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "HarekatDevSecretKey_ChangeInProduction_Min32Chars!"
            }).Build());
        var auth = new AuthService(players, hasher, jwt, new FakeEmailService(NullLogger<FakeEmailService>.Instance),
            mod, uow, NullLogger<AuthService>.Instance);
        var squads = new SquadService(squadsRepo, players, uow, NullLogger<SquadService>.Instance);
        var mm = new MatchmakingService(tickets, squadsRepo, players, matches, serversRepo, seasons, uow, NullLogger<MatchmakingService>.Instance);

        var leader = await auth.RegisterAsync(new RegisterRequest("LeaderX", "lx@t.com", "password123"));
        var member = await auth.RegisterAsync(new RegisterRequest("MemberX", "mx@t.com", "password123"));
        var squad = await squads.CreateAsync(leader.Player.Id, new CreateSquadRequest("Delta", "tr"));
        await squads.JoinByInviteAsync(member.Player.Id, new InviteSquadRequest(squad.InviteCode));
        await squads.SetReadyAsync(member.Player.Id, true);

        var act = () => mm.EnqueueAsync(member.Player.Id, new QueueRequest("tr"));
        await act.Should().ThrowAsync<Harekat.Application.Common.AppException>().Where(e => e.StatusCode == 403);
    }
}
