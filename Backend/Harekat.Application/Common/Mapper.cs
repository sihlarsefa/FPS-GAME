using Harekat.Domain.Entities;
using Harekat.Domain.ValueObjects;
using Harekat.Application.Dtos;

namespace Harekat.Application.Common;

public static class Mapper
{
    public static PlayerDto ToDto(Player p) => new(
        p.Id,
        p.Username,
        p.Email,
        p.Stats,
        p.Stats.Rank,
        p.EloRating,
        p.SquadId,
        p.EmailVerified,
        p.Role,
        p.IsOnline,
        p.Region,
        p.OwnedCosmetics,
        p.EquippedCamo,
        p.EquippedBeret,
        p.UnlockedAchievements.ToList(),
        p.SeasonXp);

    public static SquadDto ToDto(Squad s) => new(
        s.Id,
        s.Name,
        s.LeaderId,
        s.MemberIds,
        s.ReadyMemberIds.ToList(),
        s.InviteCode,
        s.Region,
        s.AllReady,
        s.OpenSlots);

    public static MatchDto ToDto(Match m) => new(
        m.Id,
        m.Region,
        m.Status.ToString(),
        m.ServerEndpoint,
        m.Teams.Select(t => new MatchTeamDto(t.SquadId, t.SquadName, t.PlayerIds, t.BotCount, t.Placement)).ToList(),
        m.SeasonNumber);

    public static ServerDto ToDto(GameServer s) => new(
        s.Id,
        s.Endpoint,
        s.Region,
        s.Status.ToString(),
        s.CurrentPlayers,
        s.MaxPlayers);
}
