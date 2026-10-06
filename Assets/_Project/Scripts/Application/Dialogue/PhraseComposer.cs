using System;
using System.Collections.Generic;
using System.Text;

namespace Project.Application.Dialogue
{
    /// <summary>Parçalardan birleşen replik: metin + sırayla çalınacak klip parça kimlikleri.</summary>
    public sealed class ComposedPhrase
    {
        public string Text;
        public readonly List<string> PartIds = new List<string>(8);
        /// <summary>Parça klipleri yoksa denenecek bütün satır kimliği (contact_full_*), ya da null.</summary>
        public string FallbackLineId;
        public int ClockHour;
        public int Meters;
        public string TargetId;
    }

    /// <summary>
    /// Düşman tespiti cümlesini parçalardan kurar: [açılış] + "Saat üç yönü" + "yüz metre" + "düşman piyade" + [kapanış].
    /// Parça kimlikleri klip adlarıdır (open_cat_03, clk_03, num_1/num_100, unit_metre, tgt_piyade, tail_t_dikkat).
    /// Saf mantık; rastgelelik <paramref name="roll"/> (0..1 döndüren) fonksiyonla gelir.
    /// </summary>
    public static class PhraseComposer
    {
        public static readonly string[] TargetIds =
        {
            "tgt_piyade", "tgt_piyade", "tgt_piyade", "tgt_grup", "tgt_tek", "tgt_sniper", "tgt_mg", "tgt_arac",
            "tgt_bina", "tgt_cati", "tgt_tepe", "tgt_agac"
        };

        private static readonly string[] CalmTails = { "tail_t_hazir", "tail_t_isaretle", "tail_t_bekle" };
        private static readonly string[] CombatTails = { "tail_t_dikkat", "tail_t_ates", "tail_t_ort", "tail_t_hareket", "tail_t_yaklasiyor" };
        private static readonly string[] PanicTails = { "tail_t_dikkat", "tail_t_kapan", "tail_t_ort", "tail_t_yakin", "tail_t_yaklasiyor" };

        public static string RandomTarget(Func<float> roll) => TargetIds[Index(roll(), TargetIds.Length)];

        /// <summary>
        /// Tespit cümlesi. <paramref name="relativeAngle"/> -180..180 (sağ +), <paramref name="meters"/> gerçek mesafe.
        /// Kitap parçaları (açılış/saat/hedef) eksikse null döner (çağıran bütün satır/yedek kullanır).
        /// </summary>
        public static ComposedPhrase ComposeSpotted(DialogueLineBook book, BarkMemory memory, int speakerId, DialogueStress stress,
            float relativeAngle, float meters, string targetId, Func<float> roll)
        {
            if (book == null || roll == null)
                return null;

            var scratch = new List<DialogueLine>(16);
            if (book.Collect(DialogueCats.PartOpen, stress, scratch) == 0)
                return null;

            var open = memory != null ? memory.Pick(DialogueCats.PartOpen, speakerId, scratch, roll()) : scratch[Index(roll(), scratch.Count)];

            var hour = TurkishNumbers.ClockHour(relativeAngle);
            var clockId = (stress == DialogueStress.Panic && roll() < 0.7f ? "clks_" : "clk_") + hour.ToString("00");
            var clock = book.Get(clockId) ?? book.Get("clk_" + hour.ToString("00"));
            if (clock == null)
                return null;

            if (string.IsNullOrEmpty(targetId))
                targetId = RandomTarget(roll);
            var target = book.Get(targetId);
            if (target == null)
                return null;

            var phrase = new ComposedPhrase { ClockHour = hour, TargetId = targetId };
            phrase.PartIds.Add(open.Id);
            phrase.PartIds.Add(clock.Id);

            var rounded = TurkishNumbers.RoundDistance(meters);
            phrase.Meters = rounded;
            var withDistance = stress != DialogueStress.Panic || roll() < 0.6f;
            string distanceText = null;
            if (withDistance)
            {
                var near = stress == DialogueStress.Panic && meters < 20f;
                TurkishNumbers.ToPartIds(rounded, phrase.PartIds);
                phrase.PartIds.Add(near ? "unit_metre_yakin" : "unit_metre");
                distanceText = TurkishNumbers.ToWords(rounded) + (near ? " metre, çok yakın" : " metre");
            }

            phrase.PartIds.Add(target.Id);

            string tailId = null;
            var tailChance = stress == DialogueStress.Panic ? 0.5f : stress == DialogueStress.Combat ? 0.25f : 0.1f;
            if (roll() < tailChance)
            {
                var tails = stress == DialogueStress.Panic ? PanicTails : stress == DialogueStress.Combat ? CombatTails : CalmTails;
                tailId = tails[Index(roll(), tails.Length)];
                if (book.Get(tailId) == null)
                    tailId = null;
                else
                    phrase.PartIds.Add(tailId);
            }

            var sb = new StringBuilder(96);
            sb.Append(open.Text).Append(' ').Append(clock.Text);
            if (distanceText != null)
                sb.Append(", ").Append(distanceText);
            sb.Append(", ").Append(target.Text).Append(stress == DialogueStress.Calm ? '.' : '!');
            if (tailId != null)
                sb.Append(' ').Append(book.Get(tailId).Text);
            phrase.Text = sb.ToString();
            phrase.FallbackLineId = FallbackFor(book, stress, roll);
            return phrase;
        }

        private static string FallbackFor(DialogueLineBook book, DialogueStress stress, Func<float> roll)
        {
            var list = new List<DialogueLine>(8);
            if (book.Collect(DialogueCats.ContactFull, stress, list) == 0)
                return null;
            return list[Index(roll(), list.Count)].Id;
        }

        private static int Index(float roll, int count)
        {
            if (count <= 0)
                return 0;
            var i = (int)(Math.Max(0f, Math.Min(0.9999999f, roll)) * count);
            return i >= count ? count - 1 : i;
        }
    }
}
