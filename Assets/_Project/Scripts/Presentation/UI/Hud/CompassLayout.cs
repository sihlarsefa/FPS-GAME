using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Pusula şeridi saf yerleşim hesapları (EditMode testli): göreli açı → piksel, kenara yapışma + solma,
    /// üst üste binen işaretlerin ayrıştırılması (declutter), derece ↔ yön harfi geçişi, mesafeye göre işaret ölçeği.
    /// CompassView bu sınıfı kullanabilir (ENTEGRASYON: CompassView.PlaceMarker).
    /// </summary>
    public static class CompassLayout
    {
        /// <summary>Derece sayısı yerine yön harfi gösterilen pencere (COD MW: ±5°).</summary>
        public const float CardinalSnapDegrees = 5f;

        /// <summary>Kenara yapışan işaretin solma bandı (şerit yarı genişliğinin oranı).</summary>
        public const float EdgeFadeBand = 0.18f;

        /// <summary>Kenara yapışmış (görüş dışı) işaretin alt opaklığı.</summary>
        public const float OffscreenAlpha = 0.45f;

        /// <summary>Bakış merkezine yakın işaretlerin vurgu (büyütme) penceresi (derece).</summary>
        public const float CenterEmphasisDegrees = 8f;

        public struct Placed
        {
            public int Id;
            public float X;
            public float Alpha;
            public float Scale;
            public bool Offscreen;
        }

        /// <summary>Göreli açıyı (-180..180) şerit merkezli piksel konumuna çevirir (sağ = +).</summary>
        public static float RelativeToPixels(float relativeDegrees, float pixelsPerDegree) =>
            Mathf.DeltaAngle(0f, relativeDegrees) * pixelsPerDegree;

        /// <summary>
        /// İşareti şeride yerleştirir. Görünür yarı genişlik dışındaysa kenara yapışır ve soluk (<see cref="OffscreenAlpha"/>) olur;
        /// kenara yaklaşırken <see cref="EdgeFadeBand"/> içinde doğrusal solar.
        /// </summary>
        public static Placed Place(int id, float relativeDegrees, float stripWidth, float pixelsPerDegree)
        {
            var half = Mathf.Max(1f, stripWidth * 0.5f);
            var raw = RelativeToPixels(relativeDegrees, pixelsPerDegree);
            var p = new Placed { Id = id, Scale = 1f };
            if (Mathf.Abs(raw) >= half)
            {
                p.X = Mathf.Sign(raw) * half;
                p.Alpha = OffscreenAlpha;
                p.Offscreen = true;
                return p;
            }

            p.X = raw;
            var edge = 1f - Mathf.Abs(raw) / half;
            var fade = Mathf.Clamp01(edge / EdgeFadeBand);
            p.Alpha = Mathf.Lerp(OffscreenAlpha, 1f, fade);
            p.Offscreen = false;
            return p;
        }

        /// <summary>Mesafeye göre işaret ölçeği: yakın büyük (1.25), uzak küçük (0.7); 15 m altı ve 250 m üstü sabit.</summary>
        public static float DistanceScale(float meters)
        {
            if (float.IsNaN(meters) || meters < 0f)
                return 1f;
            var t = Mathf.Clamp01((meters - 15f) / 235f);
            return Mathf.Lerp(1.25f, 0.7f, t);
        }

        /// <summary>Merkeze yakın işaret hafif büyür (nişan alınan işareti okunur kılar).</summary>
        public static float CenterEmphasis(float relativeDegrees)
        {
            var d = Mathf.Abs(Mathf.DeltaAngle(0f, relativeDegrees));
            var t = 1f - Mathf.Clamp01(d / CenterEmphasisDegrees);
            return 1f + 0.2f * t;
        }

        /// <summary>
        /// Yan yana işaretleri (aynı satırda) en az <paramref name="minGap"/> px aralıkla ayırır; sıra korunur.
        /// Önce soldan sağa itme, sonra sağ kenar taşmasını geri çekme (iki geçiş) — şerit sınırına sığdırılır.
        /// </summary>
        public static void Declutter(List<Placed> items, float minGap, float stripWidth)
        {
            if (items == null || items.Count < 2)
                return;
            var half = stripWidth * 0.5f;
            items.Sort((a, b) => a.X.CompareTo(b.X));
            for (var i = 1; i < items.Count; i++)
            {
                var prev = items[i - 1];
                var cur = items[i];
                if (cur.X - prev.X < minGap)
                {
                    cur.X = prev.X + minGap;
                    items[i] = cur;
                }
            }

            // Sağ sınırı aşanları geri çek.
            for (var i = items.Count - 1; i >= 0; i--)
            {
                var cur = items[i];
                var limit = i == items.Count - 1 ? half : items[i + 1].X - minGap;
                if (cur.X > limit)
                {
                    cur.X = limit;
                    items[i] = cur;
                }
            }
        }

        /// <summary>Çentik etiketi: ana/ara yöne ±5° yakınsa harf, değilse (15° katıysa) derece.</summary>
        public static bool IsCardinalLabel(int degree) => ((degree % 45) + 45) % 45 == 0;

        /// <summary>Şerit merkezindeki derece kutusu için: bakış ana/ara yönün ±5° içindeyse yön harfi, değilse derece.</summary>
        public static string CenterReadout(float yaw)
        {
            string[] names = { "K", "KD", "D", "GD", "G", "GB", "B", "KB" };
            var y = ((yaw % 360f) + 360f) % 360f;
            var nearest = Mathf.RoundToInt(y / 45f);
            var target = nearest * 45f;
            if (Mathf.Abs(Mathf.DeltaAngle(y, target)) <= CardinalSnapDegrees)
                return names[nearest % 8];
            return Mathf.RoundToInt(y) % 360 + "°";
        }

        /// <summary>Çentik yüksekliği: 45° katı 12, 15° katı 9, 5° katı 5 px; merkeze yakınken +%0..30 uzar (gözün odağı).</summary>
        public static float TickHeight(int degree, float relativeDegrees)
        {
            var d = ((degree % 360) + 360) % 360;
            var baseH = d % 45 == 0 ? 12f : d % 15 == 0 ? 9f : 5f;
            var t = 1f - Mathf.Clamp01(Mathf.Abs(relativeDegrees) / 40f);
            return baseH * (1f + 0.3f * t);
        }
    }
}
