using System.Collections.Generic;
using UnityEngine;

namespace Project.Online.Netcode
{
    /// <summary>Backend <c>MatchResultRequest</c> JSON'unu JsonUtility ile üretmek için yardımcı.</summary>
    public static class MatchResultJson
    {
        public static string Build(IReadOnlyList<TeamResult> teams)
        {
            var root = new Root { teams = new TeamBody[teams?.Count ?? 0] };
            if (teams == null)
                return JsonUtility.ToJson(root);

            for (var i = 0; i < teams.Count; i++)
            {
                var t = teams[i];
                var body = new TeamBody
                {
                    squadId = t.SquadId,
                    placement = t.Placement,
                    players = new PlayerBody[t.Players?.Count ?? 0]
                };

                if (t.Players != null)
                {
                    for (var p = 0; p < t.Players.Count; p++)
                    {
                        var pl = t.Players[p];
                        body.players[p] = new PlayerBody
                        {
                            playerId = pl.PlayerId,
                            kills = pl.Kills,
                            headshots = pl.Headshots,
                            damage = pl.Damage,
                            survivalSeconds = pl.SurvivalSeconds,
                            topWeaponId = pl.TopWeaponId ?? string.Empty
                        };
                    }
                }

                root.teams[i] = body;
            }

            // Backend JSON camelCase: teams / squadId / players / ...
            return JsonUtility.ToJson(root);
        }

        public readonly struct TeamResult
        {
            public readonly string SquadId;
            public readonly int Placement;
            public readonly IReadOnlyList<PlayerResult> Players;

            public TeamResult(string squadId, int placement, IReadOnlyList<PlayerResult> players)
            {
                SquadId = squadId;
                Placement = placement;
                Players = players;
            }
        }

        public readonly struct PlayerResult
        {
            public readonly string PlayerId;
            public readonly int Kills;
            public readonly int Headshots;
            public readonly float Damage;
            public readonly float SurvivalSeconds;
            public readonly string TopWeaponId;

            public PlayerResult(string playerId, int kills, int headshots, float damage, float survivalSeconds, string topWeaponId = null)
            {
                PlayerId = playerId;
                Kills = kills;
                Headshots = headshots;
                Damage = damage;
                SurvivalSeconds = survivalSeconds;
                TopWeaponId = topWeaponId;
            }
        }

        [System.Serializable]
        private sealed class Root
        {
            public TeamBody[] teams;
        }

        [System.Serializable]
        private sealed class TeamBody
        {
            public string squadId;
            public int placement;
            public PlayerBody[] players;
        }

        [System.Serializable]
        private sealed class PlayerBody
        {
            public string playerId;
            public int kills;
            public int headshots;
            public float damage;
            public float survivalSeconds;
            public string topWeaponId;
        }
    }
}
