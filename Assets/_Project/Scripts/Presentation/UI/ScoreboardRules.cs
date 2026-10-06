using System;
using Project.Core.Domain;

namespace Project.Presentation.UI
{
    public enum ScoreboardStatus
    {
        Alive = 0,
        Downed = 1,
        Dead = 2
    }

    /// <summary>Skor tablosu sıralama anahtarı (tim).</summary>
    public struct ScoreboardTeamKey
    {
        public int Team;
        public int Alive;
        public int Placement; // 0 = elenmedi
        public int Kills;
    }

    /// <summary>Skor tablosu üyesi sıralama anahtarı.</summary>
    public struct ScoreboardMemberKey
    {
        public ScoreboardStatus Status;
        public int Kills;
        public float Damage;
        public int Index;
    }

    /// <summary>Skor tablosunun saf mantığı: sıralama ve kısa metinler (Unity'ye bağımlı değil).</summary>
    public static class ScoreboardRules
    {
        public static readonly Comparison<ScoreboardTeamKey> TeamOrder = CompareTeams;
        public static readonly Comparison<ScoreboardMemberKey> MemberOrder = CompareMembers;

        /// <summary>Hayatta kalan sayısı çok olan önce; eşitse elenmeyen, sonra daha iyi derece (küçük #), sonra öldürme.</summary>
        public static int CompareTeams(ScoreboardTeamKey a, ScoreboardTeamKey b)
        {
            var c = b.Alive.CompareTo(a.Alive);
            if (c != 0)
                return c;

            var aOut = a.Placement > 0;
            var bOut = b.Placement > 0;
            if (aOut != bOut)
                return aOut ? 1 : -1;

            if (aOut)
            {
                c = a.Placement.CompareTo(b.Placement);
                if (c != 0)
                    return c;
            }

            c = b.Kills.CompareTo(a.Kills);
            return c != 0 ? c : a.Team.CompareTo(b.Team);
        }

        /// <summary>Hayatta önce, sonra öldürme, hasar; en son kayıt sırası.</summary>
        public static int CompareMembers(ScoreboardMemberKey a, ScoreboardMemberKey b)
        {
            var c = a.Status.CompareTo(b.Status);
            if (c != 0)
                return c;

            c = b.Kills.CompareTo(a.Kills);
            if (c != 0)
                return c;

            c = b.Damage.CompareTo(a.Damage);
            return c != 0 ? c : a.Index.CompareTo(b.Index);
        }

        public static string RoleCode(TeamRole role)
        {
            switch (role)
            {
                case TeamRole.Leader: return "KMT";
                case TeamRole.Marksman: return "KN";
                case TeamRole.MachineGunner: return "MT";
                case TeamRole.Medic: return "SH";
                case TeamRole.Radioman: return "TLS";
                case TeamRole.Grenadier: return "BMB";
                default: return "PYD";
            }
        }

        public static string StatusText(ScoreboardStatus status)
        {
            switch (status)
            {
                case ScoreboardStatus.Alive: return "Hayatta";
                case ScoreboardStatus.Downed: return "Yaralı";
                default: return "Öldü";
            }
        }
    }
}
