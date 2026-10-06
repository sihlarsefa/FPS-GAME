using System;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>Maç öncesi lobi akışının evreleri.</summary>
    public enum LobbyPhase { Gather = 0, Briefing = 1, Countdown = 2, Done = 3 }

    /// <summary>
    /// Maç öncesi lobi akışı için saf zaman çizelgesi (Unity nesnesi yok; testlenebilir).
    /// TİM TOPLANIYOR (0–7 sn, 9 bot tek tek) → HARİTA BRİFİNGİ (7–11 sn) → İNTİKALE HAZIR OL geri sayımı (11–16 sn) → devir.
    /// </summary>
    public static class LobbyFlowRules
    {
        public const int Teammates = 9;
        public const float FirstJoin = 0.5f;
        public const float JoinStagger = 0.6f;
        public const float CardSeconds = 0.4f;
        public const float GatherEnd = 7f;
        public const float BriefingEnd = 11f;
        public const float CountdownSeconds = 5f;
        public const float Duration = BriefingEnd + CountdownSeconds;
        public const float PhaseFade = 0.35f;

        public static LobbyPhase PhaseAt(float t)
        {
            if (t < GatherEnd) return LobbyPhase.Gather;
            if (t < BriefingEnd) return LobbyPhase.Briefing;
            return t < Duration ? LobbyPhase.Countdown : LobbyPhase.Done;
        }

        /// <summary>i. botun (0..8) katılma anı.</summary>
        public static float JoinTime(int index) => FirstJoin + Math.Max(0, index) * JoinStagger;

        /// <summary>Katılmış bot sayısı (kart girişi başlamışlar).</summary>
        public static int JoinedCount(float t)
        {
            if (t < FirstJoin) return 0;
            var n = (int)((t - FirstJoin) / JoinStagger) + 1;
            return Math.Min(Teammates, Math.Max(0, n));
        }

        /// <summary>i. kartın giriş ilerlemesi 0..1 (hafif aşan eğri 1'e oturur).</summary>
        public static float CardPop(float t, int index)
        {
            var x = Clamp01((t - JoinTime(index)) / CardSeconds);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = x - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Verilen evrenin görünürlüğü (giriş/çıkış yumuşatması) 0..1.</summary>
        public static float PhaseAlpha(float t, LobbyPhase phase)
        {
            float start, end;
            switch (phase)
            {
                case LobbyPhase.Gather: start = 0f; end = GatherEnd; break;
                case LobbyPhase.Briefing: start = GatherEnd; end = Duration; break;
                default: return 0f;
            }
            var a = Clamp01((t - start) / PhaseFade);
            var b = phase == LobbyPhase.Gather ? Clamp01((end - t) / PhaseFade) : 1f;
            return Math.Min(a, b);
        }

        /// <summary>Geri sayım rakamı: 5..1; geri sayım dışında 0.</summary>
        public static int CountdownNumber(float t)
        {
            if (t < BriefingEnd || t >= Duration) return 0;
            var left = Duration - t;
            return Math.Max(1, (int)Math.Ceiling(left));
        }

        /// <summary>Önceki ve şimdiki zaman arasında yeni bir geri sayım tiki geçti mi (her tam saniye başı).</summary>
        public static bool CountdownTickCrossed(float prevT, float t)
        {
            var a = CountdownNumber(prevT);
            var b = CountdownNumber(t);
            if (b == 0) return false;
            return a != b;
        }

        /// <summary>Geri sayımın tamamlandığı (devir) an mı.</summary>
        public static bool IsDone(float t) => t >= Duration;

        /// <summary>Boşlukla geçiş: evre sınırına atlar; geri sayımda doğrudan bitirir.</summary>
        public static float SkipTarget(float t)
        {
            switch (PhaseAt(t))
            {
                case LobbyPhase.Gather: return GatherEnd;
                case LobbyPhase.Briefing: return BriefingEnd;
                default: return Duration;
            }
        }

        /// <summary>Geri sayım sırasında rakam içi vuruş ilerlemesi 0..1 (animasyon).</summary>
        public static float CountdownBeat(float t)
        {
            if (CountdownNumber(t) == 0) return 0f;
            var left = Duration - t;
            return 1f - (left - (float)Math.Floor(left));
        }

        /// <summary>Brifing maddeleri görünürlüğü: i. madde (0..2) sırayla belirir.</summary>
        public static float BulletAlpha(float t, int index) =>
            Clamp01((t - (GatherEnd + 0.5f + Math.Max(0, index) * 0.7f)) / 0.4f);

        private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
    }
}
