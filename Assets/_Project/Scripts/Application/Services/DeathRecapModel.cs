using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Project.Core.Domain;

namespace Project.Application.Services
{
    /// <summary>Alınan tek bir isabet (ölüm özeti zaman çizelgesi için). Saf veri.</summary>
    public readonly struct DamageTaken
    {
        public readonly float Time;
        public readonly float Amount;
        public readonly BodyPart Part;
        public readonly int AttackerId;
        public readonly string WeaponId;
        public readonly float Distance;

        public DamageTaken(float time, float amount, BodyPart part, int attackerId, string weaponId, float distance)
        {
            Time = time;
            Amount = amount;
            Part = part;
            AttackerId = attackerId;
            WeaponId = weaponId;
            Distance = distance;
        }
    }

    /// <summary>Ölüm özeti: öldüren, silah, mesafe, vuruş bölgeleri ve son saniyelerin hasar çizelgesi.</summary>
    public sealed class DeathRecap
    {
        public string KillerName = string.Empty;
        public string WeaponName = string.Empty;
        public float Distance;
        public bool HasKiller;
        public float KillerHealth;
        public float KillerMaxHealth;
        public bool KillerAlive;
        public bool FinalHeadshot;
        public float TotalDamage;
        public float WindowSeconds;
        /// <summary>BodyPart indeksine göre (Torso, Head, Arm, Leg) isabet sayısı.</summary>
        public readonly int[] HitsByPart = new int[4];
        public readonly float[] DamageByPart = new float[4];
        /// <summary>En eski -> en yeni, ölüme göre göreli saniye (negatif).</summary>
        public readonly List<DamageTaken> Timeline = new List<DamageTaken>(8);
        public float TimelineEnd;

        public int TotalHits
        {
            get
            {
                var n = 0;
                for (var i = 0; i < HitsByPart.Length; i++) n += HitsByPart[i];
                return n;
            }
        }

        public float KillerHealthFraction => KillerMaxHealth > 0f ? Math.Max(0f, Math.Min(1f, KillerHealth / KillerMaxHealth)) : 0f;
    }

    /// <summary>Alınan hasarı tutan, ölümde <see cref="DeathRecap"/> üreten saf biriktirici.</summary>
    public sealed class DeathRecapBuilder
    {
        public const float DefaultWindowSeconds = 8f;
        public const int MaxStored = 64;
        public const int MaxTimeline = 8;

        private readonly List<DamageTaken> _hits = new List<DamageTaken>(32);

        public int Count => _hits.Count;

        public void Clear() => _hits.Clear();

        public void Add(in DamageTaken hit)
        {
            if (hit.Amount <= 0f)
                return;

            if (_hits.Count >= MaxStored)
                _hits.RemoveAt(0);
            _hits.Add(hit);
        }

        /// <summary>Ölüm anına göre pencere içindeki isabetlerden özet üretir.</summary>
        public DeathRecap Build(float deathTime, float windowSeconds = DefaultWindowSeconds)
        {
            var recap = new DeathRecap { WindowSeconds = windowSeconds, TimelineEnd = deathTime };
            var from = deathTime - windowSeconds;
            DamageTaken? last = null;
            for (var i = 0; i < _hits.Count; i++)
            {
                var h = _hits[i];
                if (h.Time < from || h.Time > deathTime + 0.25f)
                    continue;

                var p = PartIndex(h.Part);
                recap.HitsByPart[p]++;
                recap.DamageByPart[p] += h.Amount;
                recap.TotalDamage += h.Amount;
                recap.Timeline.Add(h);
                last = h;
            }

            while (recap.Timeline.Count > MaxTimeline)
                recap.Timeline.RemoveAt(0);

            if (last.HasValue)
            {
                recap.HasKiller = last.Value.AttackerId >= 0;
                recap.Distance = last.Value.Distance;
                recap.FinalHeadshot = last.Value.Part == BodyPart.Head;
            }

            return recap;
        }

        public static int PartIndex(BodyPart part)
        {
            var i = (int)part;
            return i < 0 || i > 3 ? 0 : i;
        }
    }

    /// <summary>Ölüm/yaralı/tim elenme ekranlarının Türkçe metinleri (saf).</summary>
    public static class DeathRecapText
    {
        private static readonly CultureInfo Tr = new CultureInfo("tr-TR");

        public static string PartName(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: return "Kafa";
                case BodyPart.Arm: return "Kol";
                case BodyPart.Leg: return "Bacak";
                default: return "Gövde";
            }
        }

        public static string Distance(float meters) =>
            Math.Max(0, (int)Math.Round(meters)).ToString(Tr) + " m";

        public static string KillerHealth(float current, float max) =>
            Math.Max(0, (int)Math.Ceiling(current)).ToString(Tr) + " / " + Math.Max(1, (int)Math.Round(max)).ToString(Tr) + " CAN";

        /// <summary>"-0,8 sn" biçimi: ölüme göre göreli zaman.</summary>
        public static string RelativeTime(float hitTime, float deathTime)
        {
            var d = Math.Max(0f, deathTime - hitTime);
            return "-" + d.ToString("0.0", Tr) + " sn";
        }

        public static string Damage(float amount) => "-" + Math.Max(0, (int)Math.Round(amount)).ToString(Tr);

        public static string Placement(int placement, int teams)
        {
            if (placement <= 0)
                return "TİM SIRASI: -";
            return teams > 0 ? "TİM SIRASI: #" + placement + " / " + teams : "TİM SIRASI: #" + placement;
        }

        public static string Survival(float seconds)
        {
            var s = Math.Max(0, (int)seconds);
            return (s / 60).ToString("00", Tr) + ":" + (s % 60).ToString("00", Tr);
        }

        public static string Headline(bool killerKnown, bool headshot)
        {
            if (!killerKnown) return "ŞEHİT DÜŞTÜN";
            return headshot ? "KAFADAN VURULDUN" : "ŞEHİT DÜŞTÜN";
        }

        public static string Summary(DeathRecap r)
        {
            if (r == null) return string.Empty;
            var sb = new StringBuilder(96);
            sb.Append(r.TotalHits).Append(" isabet, toplam ").Append(Math.Max(0, (int)Math.Round(r.TotalDamage))).Append(" hasar");
            return sb.ToString();
        }
    }

    /// <summary>Yaralı (DBNO) ekranı için saf hesaplar: kanama halkası, kaldırma kesilmesi, yardım çağrısı bekleme süresi.</summary>
    public static class DownedHudMath
    {
        public const float CallCooldownSeconds = 5f;

        /// <summary>Halka doluluk oranı (1 = yeni düştü, 0 = ölüyor).</summary>
        public static float BleedFraction(float remaining, float total)
        {
            if (total <= 0f) return 0f;
            return Math.Max(0f, Math.Min(1f, remaining / total));
        }

        /// <summary>Kaldırma ilerlemesi azaldı ve kaldıran yok: kaldırma kesildi (tamamlanma değil).</summary>
        public static bool ReviveInterrupted(float previousProgress, float currentProgress, bool stillDowned)
        {
            return stillDowned && previousProgress > 0.05f && currentProgress < previousProgress - 0.02f;
        }

        public static bool CanCall(float now, float lastCall) => now - lastCall >= CallCooldownSeconds;

        /// <summary>Kanama süresi azaldıkça nabız hızı (Hz) artar.</summary>
        public static float PulseHz(float bleedFraction) => 1f + (1f - Math.Max(0f, Math.Min(1f, bleedFraction))) * 2.5f;

        public static string CallButtonText(float now, float lastCall, string keyTag)
        {
            var left = CallCooldownSeconds - (now - lastCall);
            return left > 0f
                ? "YARDIM İSTENDİ (" + (int)Math.Ceiling(left) + ")"
                : keyTag + " YARDIM İSTE";
        }
    }
}
