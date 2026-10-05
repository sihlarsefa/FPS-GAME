using Harekat.DiscordBot.Domain;

namespace Harekat.DiscordBot.Services;

public interface ITournamentService
{
    Tournament CreateBracket(string name, IReadOnlyList<string> teamNames);
    Tournament? Get(string tournamentId);
    IReadOnlyList<Tournament> List();
    TournamentResultSubmitResult SubmitResult(string tournamentId, string matchId, string winnerTeam);
}

public sealed record TournamentResultSubmitResult(
    bool Ok,
    string Message,
    Tournament? Tournament,
    bool TournamentCompleted);

public sealed class TournamentService(InMemoryGameDataStore store, PerformanceMetrics metrics) : ITournamentService
{
    public Tournament CreateBracket(string name, IReadOnlyList<string> teamNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(teamNames);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var cleaned = teamNames
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleaned.Count < 2)
                throw new ArgumentException("En az 2 takım gerekli.", nameof(teamNames));
            if (cleaned.Count > 32)
                throw new ArgumentException("En fazla 32 takım desteklenir.", nameof(teamNames));

            var size = 1;
            while (size < cleaned.Count)
                size <<= 1;

            while (cleaned.Count < size)
                cleaned.Add($"BYE-{cleaned.Count + 1}");

            var tournament = new Tournament
            {
                TournamentId = $"trn-{Guid.NewGuid():N}"[..12],
                Name = name.Trim(),
                Status = TournamentStatus.InProgress
            };
            tournament.TeamNames.AddRange(cleaned.Where(t => !t.StartsWith("BYE-", StringComparison.Ordinal)));

            var rounds = (int)Math.Log2(size);
            var byRound = new Dictionary<int, List<TournamentMatch>>();
            var matchCounter = 0;

            for (var round = 1; round <= rounds; round++)
            {
                var matchesInRound = size >> round; // 8→4,2,1 | 4→2,1
                var list = new List<TournamentMatch>(matchesInRound);
                for (var slot = 0; slot < matchesInRound; slot++)
                {
                    list.Add(new TournamentMatch
                    {
                        MatchId = $"m{++matchCounter}",
                        Round = round,
                        SlotIndex = slot,
                        Status = MatchSlotStatus.Pending
                    });
                }

                byRound[round] = list;
            }

            for (var round = 1; round < rounds; round++)
            {
                var current = byRound[round];
                var next = byRound[round + 1];
                for (var slot = 0; slot < current.Count; slot++)
                {
                    var parent = next[slot / 2];
                    current[slot].NextMatchId = parent.MatchId;
                    current[slot].AdvancesAsTeamA = slot % 2 == 0;
                }
            }

            foreach (var list in byRound.Values)
                tournament.Matches.AddRange(list);

            var round1 = byRound[1];
            for (var i = 0; i < round1.Count; i++)
            {
                var match = round1[i];
                match.TeamA = cleaned[i * 2];
                match.TeamB = cleaned[i * 2 + 1];

                var aBye = match.TeamA.StartsWith("BYE-", StringComparison.Ordinal);
                var bBye = match.TeamB.StartsWith("BYE-", StringComparison.Ordinal);
                if (aBye || bBye)
                {
                    match.Winner = aBye ? match.TeamB : match.TeamA;
                    match.Status = MatchSlotStatus.Bye;
                    AdvanceWinner(tournament, match);
                }
                else
                {
                    match.Status = MatchSlotStatus.Ready;
                }
            }

            store.UpsertTournament(tournament);
            metrics.Record("tournament.create", sw.Elapsed);
            return store.GetTournament(tournament.TournamentId)!;
        }
        catch
        {
            metrics.Record("tournament.create", sw.Elapsed, success: false);
            throw;
        }
    }

    public Tournament? Get(string tournamentId) => store.GetTournament(tournamentId);

    public IReadOnlyList<Tournament> List() => store.GetTournaments();

    public TournamentResultSubmitResult SubmitResult(string tournamentId, string matchId, string winnerTeam)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tournamentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(winnerTeam);

        var tournament = store.GetTournament(tournamentId);
        if (tournament is null)
            return new TournamentResultSubmitResult(false, "Turnuva bulunamadı.", null, false);

        if (tournament.Status is TournamentStatus.Completed or TournamentStatus.Cancelled)
            return new TournamentResultSubmitResult(false, "Turnuva kapalı.", tournament, false);

        var match = tournament.Matches.FirstOrDefault(m =>
            string.Equals(m.MatchId, matchId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return new TournamentResultSubmitResult(false, "Maç bulunamadı.", tournament, false);

        if (match.Status is MatchSlotStatus.Completed or MatchSlotStatus.Bye)
            return new TournamentResultSubmitResult(false, "Maç zaten sonuçlanmış.", tournament, false);

        if (match.TeamA is null || match.TeamB is null)
            return new TournamentResultSubmitResult(false, "Maç henüz hazır değil.", tournament, false);

        var winner = winnerTeam.Trim();
        if (!string.Equals(winner, match.TeamA, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(winner, match.TeamB, StringComparison.OrdinalIgnoreCase))
        {
            return new TournamentResultSubmitResult(false, "Kazanan, maçtaki takımlardan biri olmalı.", tournament, false);
        }

        match.Winner = string.Equals(winner, match.TeamA, StringComparison.OrdinalIgnoreCase)
            ? match.TeamA
            : match.TeamB;
        match.Status = MatchSlotStatus.Completed;
        AdvanceWinner(tournament, match);

        var maxRound = tournament.Matches.Max(x => x.Round);
        var final = tournament.Matches.First(m => m.Round == maxRound);
        var completed = final.Status is MatchSlotStatus.Completed or MatchSlotStatus.Bye && final.Winner is not null;
        if (completed)
        {
            tournament.Status = TournamentStatus.Completed;
            tournament.WinnerTeam = final.Winner!.StartsWith("BYE-", StringComparison.Ordinal)
                ? null
                : final.Winner;
        }

        store.UpsertTournament(tournament);
        var fresh = store.GetTournament(tournament.TournamentId)!;
        return new TournamentResultSubmitResult(
            true,
            completed ? $"Turnuva tamamlandı. Şampiyon: {fresh.WinnerTeam}" : "Sonuç kaydedildi, sonraki tura ilerletildi.",
            fresh,
            completed);
    }

    private static void AdvanceWinner(Tournament tournament, TournamentMatch match)
    {
        if (string.IsNullOrEmpty(match.NextMatchId) || string.IsNullOrEmpty(match.Winner))
            return;

        var next = tournament.Matches.FirstOrDefault(m => m.MatchId == match.NextMatchId);
        if (next is null)
            return;

        if (match.AdvancesAsTeamA)
            next.TeamA = match.Winner;
        else
            next.TeamB = match.Winner;

        if (next.TeamA is not null && next.TeamB is not null && next.Status == MatchSlotStatus.Pending)
        {
            var aBye = next.TeamA.StartsWith("BYE-", StringComparison.Ordinal);
            var bBye = next.TeamB.StartsWith("BYE-", StringComparison.Ordinal);
            if (aBye || bBye)
            {
                next.Winner = aBye ? next.TeamB : next.TeamA;
                next.Status = MatchSlotStatus.Bye;
                AdvanceWinner(tournament, next);
            }
            else
            {
                next.Status = MatchSlotStatus.Ready;
            }
        }
    }
}
