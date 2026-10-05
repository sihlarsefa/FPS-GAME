using Harekat.Application.Abstractions;
using Harekat.Application.Dtos;
using Harekat.Application.Services;
using Harekat.Domain.Catalogs;
using Harekat.Domain.Enums;
using Harekat.Infrastructure.Repositories;
using Harekat.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Harekat.Tests.Application;

public class AuthAndMatchFlowTests
{
    private static (AuthService auth, SquadService squads, MatchmakingService mm, MatchResultService results, GameServerService servers, InMemoryStore store) CreateServices()
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
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "HarekatDevSecretKey_ChangeInProduction_Min32Chars!",
                    ["Jwt:Issuer"] = "harekat",
                    ["Jwt:Audience"] = "harekat-clients"
                }).Build());

        var auth = new AuthService(players, hasher, jwt, new FakeEmailService(NullLogger<FakeEmailService>.Instance),
            mod, uow, new Harekat.Infrastructure.Auth.PassThroughSteamTicketValidator(), NullLogger<AuthService>.Instance);
        var squads = new SquadService(squadsRepo, players, uow, NullLogger<SquadService>.Instance);
        var mm = new MatchmakingService(tickets, squadsRepo, players, matches, serversRepo, seasons, uow, NullLogger<MatchmakingService>.Instance);
        var ach = new AchievementService();
        var results = new MatchResultService(matches, players, squadsRepo, serversRepo, seasons, uow, ach, NullLogger<MatchResultService>.Instance);
        var servers = new GameServerService(serversRepo, hasher, uow);
        return (auth, squads, mm, results, servers, store);
    }

    [Fact]
    public async Task Register_Login_CreateSquad_Works()
    {
        var (auth, squads, _, _, _, _) = CreateServices();

        var a = await auth.RegisterAsync(new RegisterRequest("Asker1", "a1@test.com", "password123", "tr"));
        a.AccessToken.Should().NotBeNullOrEmpty();
        a.Player.Username.Should().Be("Asker1");

        var login = await auth.LoginAsync(new LoginRequest("Asker1", "password123"));
        login.RefreshToken.Should().NotBeNullOrEmpty();

        var squad = await squads.CreateAsync(a.Player.Id, new CreateSquadRequest("Alfa Tim", "tr"));
        squad.MemberIds.Should().ContainSingle().Which.Should().Be(a.Player.Id);
        squad.InviteCode.Should().HaveLength(6);
    }

    [Fact]
    public async Task MatchResult_AwardsXpAndRank()
    {
        var (auth, squads, mm, results, servers, store) = CreateServices();
        var a = await auth.RegisterAsync(new RegisterRequest("AskerXp", "xp@test.com", "password123", "tr"));
        await squads.CreateAsync(a.Player.Id, new CreateSquadRequest("Bravo", "tr"));
        var server = await servers.RegisterAsync(new RegisterServerRequest("127.0.0.1", 7777, "tr", "server-key-12345678"));

        await mm.EnqueueAsync(a.Player.Id, new QueueRequest("tr", 100));
        foreach (var t in store.Tickets.Values)
            t.EnqueuedAt = DateTimeOffset.UtcNow.AddMinutes(-2);

        var match = await mm.TryFormMatchesAsync("tr");
        match.Should().NotBeNull();
        match!.Teams.Should().HaveCount(10);
        match.Teams.Count(t => t.SquadId == Guid.Empty || t.BotCount == 10).Should().BeGreaterThan(0);

        var team = match.Teams.First(t => t.SquadId != Guid.Empty);
        var result = await results.SubmitResultAsync(match.Id, new MatchResultRequest(
        [
            new TeamMatchResultDto(team.SquadId, 1,
            [
                new PlayerMatchResultDto(a.Player.Id, 5, 2, 1200, 400, WeaponIds.Mpt76)
            ])
        ]), server.Id);

        result.Awards.Should().ContainSingle();
        var award = result.Awards[0];
        award.XpGained.Should().Be(2900);
        award.NewRank.Should().Be(MilitaryRank.SozlesmeliEr);
    }

    [Fact]
    public async Task DuplicateUsername_ThrowsConflict()
    {
        var (auth, _, _, _, _, _) = CreateServices();
        await auth.RegisterAsync(new RegisterRequest("Same", "s1@t.com", "password123"));
        var act = () => auth.RegisterAsync(new RegisterRequest("Same", "s2@t.com", "password123"));
        await act.Should().ThrowAsync<Harekat.Application.Common.AppException>()
            .Where(e => e.StatusCode == 409);
    }

    [Fact]
    public async Task Squad_RejectsEleventhMember()
    {
        var (auth, squads, _, _, _, _) = CreateServices();
        var leader = await auth.RegisterAsync(new RegisterRequest("Lider", "l@t.com", "password123"));
        var squad = await squads.CreateAsync(leader.Player.Id, new CreateSquadRequest("Full", "tr"));

        for (var i = 0; i < 9; i++)
        {
            var p = await auth.RegisterAsync(new RegisterRequest($"User{i}", $"u{i}@t.com", "password123"));
            await squads.JoinByInviteAsync(p.Player.Id, new InviteSquadRequest(squad.InviteCode));
        }

        var overflow = await auth.RegisterAsync(new RegisterRequest("Extra", "e@t.com", "password123"));
        var act = () => squads.JoinByInviteAsync(overflow.Player.Id, new InviteSquadRequest(squad.InviteCode));
        await act.Should().ThrowAsync<Harekat.Application.Common.AppException>();
    }

    [Fact]
    public void Pbkdf2_HashAndVerify()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("secret-pass");
        hasher.Verify("secret-pass", hash).Should().BeTrue();
        hasher.Verify("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void EloCalculator_ChangesRating()
    {
        var win = EloCalculator.Update(1000, 1, 10, 5);
        var lose = EloCalculator.Update(1000, 10, 10, 0);
        win.Should().BeGreaterThan(1000);
        lose.Should().BeLessThan(1000);
    }

    [Fact]
    public void AchievementCatalog_HasAtLeast30()
    {
        AchievementCatalog.All.Should().HaveCountGreaterThanOrEqualTo(30);
    }

    [Fact]
    public async Task SteamLogin_CreatesAndReusesAccount()
    {
        var (auth, _, _, _, _, store) = CreateServices();
        var first = await auth.LoginWithSteamAsync(new SteamAuthRequest("DEV:76561198000000001:MaviAsker", "MaviAsker"));
        first.AccessToken.Should().NotBeNullOrEmpty();
        first.Player.Username.Should().Be("MaviAsker");

        var entity = store.Players.Values.Single(p => p.Id == first.Player.Id);
        entity.SteamId.Should().Be(76561198000000001UL);
        entity.EmailVerified.Should().BeTrue();

        var second = await auth.LoginWithSteamAsync(new SteamAuthRequest("DEV:76561198000000001"));
        second.Player.Id.Should().Be(first.Player.Id);
        store.Players.Should().HaveCount(1);
    }

    [Fact]
    public async Task Friendship_RequestAndAccept()
    {
        var store = new InMemoryStore();
        var players = new MemoryPlayerRepository(store);
        var friends = new MemoryFriendshipRepository(store);
        var uow = new MemoryUnitOfWork(store);
        var hasher = new Pbkdf2PasswordHasher();
        var jwt = new Harekat.Infrastructure.Auth.JwtTokenService(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "HarekatDevSecretKey_ChangeInProduction_Min32Chars!"
            }).Build());
        var mod = new MemoryModerationRepository(store);
        var auth = new AuthService(players, hasher, jwt, new FakeEmailService(NullLogger<FakeEmailService>.Instance),
            mod, uow, new Harekat.Infrastructure.Auth.PassThroughSteamTicketValidator(), NullLogger<AuthService>.Instance);
        var service = new FriendshipService(friends, players, uow);

        var a = await auth.RegisterAsync(new RegisterRequest("Friend1", "f1@t.com", "password123"));
        var b = await auth.RegisterAsync(new RegisterRequest("Friend2", "f2@t.com", "password123"));
        var req = await service.SendRequestAsync(a.Player.Id, b.Player.Id);
        var accepted = await service.AcceptAsync(b.Player.Id, req.Id);
        accepted.Status.Should().Be("Accepted");
    }
}
