using Harekat.DiscordBot.Domain;
using Harekat.DiscordBot.Services;

namespace Harekat.DiscordBot.Formatting;

public static class EmbedTextFormatter
{
    public static string FormatLeaderboard(IReadOnlyList<LeaderboardEntry> entries, string title)
    {
        if (entries.Count == 0)
            return $"**{title}**\nHenüz veri yok.";

        var lines = entries.Select(e =>
            $"`#{e.Rank:00}` **{e.DisplayName}** — {RankCatalog.GetDisplayName(e.MilitaryRank)} | Skor: {e.Score:N0} | Ö: {e.Kills} | G: {e.Wins}");
        return $"**{title}**\n" + string.Join('\n', lines);
    }

    public static string FormatWeeklySquadKills(IReadOnlyList<SquadKillLeaderboardEntry> entries)
    {
        if (entries.Count == 0)
            return "**Haftalık En Çok Öldüren Tim**\nHenüz veri yok.";

        var lines = entries.Select(e =>
            $"`#{e.Rank:00}` **{e.SquadName}** — {e.WeeklyKills} öldürme ({e.MemberCount} üye)");
        return "**Haftalık En Çok Öldüren Tim**\n" + string.Join('\n', lines);
    }

    public static string FormatProfile(PlayerProfile profile)
    {
        var s = profile.Stats;
        return $"**Profil: {profile.DisplayName}**\n" +
               $"Rütbe: **{RankCatalog.GetDisplayName(s.Rank)}**\n" +
               $"XP: {s.Experience:N0} | Sezon XP: {profile.SeasonXp:N0}\n" +
               $"Maç: {s.Matches} | Galibiyet: {s.Wins} ({s.WinRate:P0})\n" +
               $"Öldürme: {s.Kills} | Kafa: {s.Headshots} ({s.HeadshotRate:P0})\n" +
               $"En iyi sıra: {s.BestPlacement} | Hasar: {s.TotalDamage:N0}\n" +
               $"En uzun hayatta kalma: {TimeSpan.FromSeconds(s.LongestSurvivalSeconds):mm\\:ss}\n" +
               $"Tim: {profile.SquadId ?? "Yok"}";
    }

    public static string FormatServers(ServerStatusSummary summary)
    {
        var header =
            $"**Sunucu Durumu** — Çevrimiçi: {summary.OnlineServers}/{summary.TotalServers} | " +
            $"Oyuncu: {summary.ActivePlayers}/{summary.Capacity}\n";

        if (summary.Servers.Count == 0)
            return header + "Kayıtlı sunucu yok.";

        var lines = summary.Servers.Select(s =>
        {
            var state = s.IsOnline ? "🟢" : "🔴";
            return $"{state} **{s.Name}** ({s.Region}) — {s.PlayerCount}/{s.MaxPlayers} | ping {s.PingMs} ms";
        });
        return header + string.Join('\n', lines);
    }

    public static string FormatSquadBoard(IReadOnlyList<SquadInfo> squads)
    {
        if (squads.Count == 0)
            return "**Tim İlanları**\nAçık koltuğu olan tim yok.";

        var lines = squads.Select(s =>
            $"• **{s.Name}** — Lider: {s.LeaderName} | {s.MemberCount}/10 | Boş: {s.OpenSlots}");
        return "**Tim İlanları** (boş koltuğu olanlar)\n" + string.Join('\n', lines);
    }

    public static string FormatRecruitment(SquadRecruitment r) =>
        $"**Tim Toplama:** {r.SquadName}\n" +
        $"İlan: `{r.RecruitmentId}` | Üye: {r.MemberCount}/10 | Boş: {r.OpenSlots}\n" +
        $"Komutan: {r.CreatorDisplayName}\n" +
        (string.IsNullOrWhiteSpace(r.Description) ? "" : $"Not: {r.Description}\n") +
        $"Katılmak için: `/tim-ara katil id:{r.RecruitmentId}`";

    public static string FormatTournamentBracket(Tournament t)
    {
        var header = $"**Turnuva: {t.Name}** (`{t.TournamentId}`) — {t.Status}\n";
        if (t.WinnerTeam is not null)
            header += $"Şampiyon: **{t.WinnerTeam}**\n";

        var lines = t.Matches
            .OrderBy(m => m.Round)
            .ThenBy(m => m.SlotIndex)
            .Select(m =>
            {
                var a = m.TeamA ?? "?";
                var b = m.TeamB ?? "?";
                var w = m.Winner is null ? "" : $" → {m.Winner}";
                return $"R{m.Round} `{m.MatchId}` {a} vs {b} [{m.Status}]{w}";
            });
        return header + string.Join('\n', lines);
    }

    public static string FormatModerationLog(IReadOnlyList<ModerationLogEntry> entries)
    {
        if (entries.Count == 0)
            return "**Moderasyon Logu**\nKayıt yok.";

        var lines = entries.Select(e =>
        {
            var dur = e.Duration is null ? "" : $" ({e.Duration.Value.TotalMinutes:0} dk)";
            return $"`{e.CreatedAt:u}` [{e.Action}] {e.TargetDisplayName} — {e.Reason}{dur} (mod: {e.ModeratorDisplayName})";
        });
        return "**Moderasyon Logu**\n" + string.Join('\n', lines);
    }

    public static string FormatMetrics(PerformanceSnapshot snapshot)
    {
        var lines = snapshot.Operations.Take(10).Select(o =>
            $"• {o.Name}: n={o.Count}, ort={o.AverageMs:F1}ms, max={o.MaxMs:F1}ms, hata={o.Errors}");
        return $"**Bot Performans** — toplam={snapshot.TotalOperations}, hata={snapshot.ErrorCount}, ort={snapshot.AverageMs:F1}ms\n" +
               string.Join('\n', lines);
    }
}
