using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Lobi dioramasının saf (sahnesiz) matematiği: sırt profili, geçerli köşe denetimi, kamera nefes süzülmesi, alev maskesi (eski 30 sn'lik süzülme döngüsü artık kullanılmaz).</summary>
    public static class MenuSceneMath
    {
        /// <summary>Süzülme döngüsünün toplam süresi (sn).</summary>
        public const float LoopSeconds = 30f;

        /// <summary>Açı sayısı (her biri LoopSeconds / AngleCount sn).</summary>
        public const int AngleCount = 3;

        private static readonly Vector3[] PositionKeys =
        {
            Vector3.zero,
            new Vector3(3.6f, 0.35f, 1.2f),
            new Vector3(-3.1f, -0.1f, 2.4f)
        };

        private static readonly Vector3[] TargetKeys =
        {
            Vector3.zero,
            new Vector3(1.6f, 0.15f, 0.4f),
            new Vector3(-1.4f, 0.1f, 0.8f)
        };

        private static readonly float[] FovKeys = { 0f, -3f, 2f };

        /// <summary>
        /// Sırt kesit profili (0..1 → 0..1). Ucunda sin(PI) ~ -1e-7 olup negatif tabanın kesirli üssü NaN verirdi; taban 0'a sıkıştırılır.
        /// </summary>
        public static float RidgeProfile(float zf)
        {
            var f = Mathf.Clamp01(float.IsNaN(zf) ? 0f : zf);
            var s = Mathf.Sin(Mathf.PI * Mathf.Pow(f, 0.85f));
            return Mathf.Pow(Mathf.Max(0f, s), 0.75f);
        }

        /// <summary>Köşe NaN/sonsuz değil mi.</summary>
        public static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        /// <summary>Geçersiz köşeyi verilen güvenli değerle değiştirir.</summary>
        public static Vector3 FiniteOr(Vector3 v, Vector3 fallback) => IsFinite(v) ? v : fallback;

        /// <summary>
        /// Zamanın (sn) kamera sapması: 3 açı arasında yumuşak geçişle 30 sn'de kapanan döngü (t = 0 ve t = 30 aynı değer).
        /// </summary>
        public static void CameraLoop(float seconds, out Vector3 positionOffset, out Vector3 targetOffset, out float fovOffset)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
                seconds = 0f;

            var phase = Mathf.Repeat(seconds, LoopSeconds) / LoopSeconds * AngleCount;
            var index = Mathf.Min(AngleCount - 1, Mathf.FloorToInt(phase));
            var f = phase - index;
            f = f * f * (3f - 2f * f);
            var next = (index + 1) % AngleCount;
            positionOffset = Vector3.Lerp(PositionKeys[index], PositionKeys[next], f);
            targetOffset = Vector3.Lerp(TargetKeys[index], TargetKeys[next], f);
            fovOffset = Mathf.Lerp(FovKeys[index], FovKeys[next], f);
        }

        /// <summary>
        /// Sabit menü kamerasının nefes süzülmesi (m): her eksende en çok ±1 cm, çok yavaş (periyot 7-11 sn). NaN/sonsuz zaman 0 sayılır.
        /// </summary>
        public static Vector3 BreathingOffset(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
                seconds = 0f;

            return new Vector3(
                Mathf.Sin(seconds * 0.57f) * 0.008f,
                Mathf.Sin(seconds * 0.71f + 1.3f) * 0.01f,
                Mathf.Sin(seconds * 0.89f + 2.1f) * 0.006f);
        }

        /// <summary>
        /// Alev kartı dokusunun maskesi (0..1): <paramref name="frame"/> numaralı karede yuvarlak uçlu, tabanı geniş damla biçimli bir ana alev ve
        /// iki kısa yan dil (mızrak gibi sivri değil); u yatay, v dikey (0 = taban). Saf ve sonlu: NaN girişi 0 verir.
        /// </summary>
        public static float FlameMask(int frame, float u, float v)
        {
            if (float.IsNaN(u) || float.IsNaN(v) || v < 0f || v > 1f || u < 0f || u > 1f)
                return 0f;

            var best = 0f;
            for (var k = 0; k < 3; k++)
            {
                var side = k - 1;
                var height = k == 1 ? 0.88f + 0.07f * Mathf.Sin(frame * 2.3f) : 0.58f + 0.1f * Mathf.Sin(frame * 1.7f + k * 2f);
                if (v >= height)
                    continue;

                var t = v / height;
                var profile = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.55f));   // Taban yuvarlak, en geniş ~%20 yükseklikte, uç yuvarlak.
                var halfWidth = (k == 1 ? 0.3f : 0.15f) * Mathf.Pow(Mathf.Max(0f, profile), 0.8f);
                if (halfWidth < 1e-3f)
                    continue;

                var baseX = 0.5f + side * 0.19f * (1f - v * 0.5f);
                var lean = Mathf.Sin(frame * 1.9f + k * 2.3f) * 0.07f;
                var wobble = Mathf.Sin(v * 7f + frame * 2.1f + k) * 0.02f;
                var cx = baseX + lean * v * v + wobble * v;
                var m = Mathf.Clamp01(1f - Mathf.Abs(u - cx) / halfWidth);
                m = m * m * (3f - 2f * m);
                if (m > best)
                    best = m;
            }

            return best;
        }

        /// <summary>
        /// Mavi saat gökyüzü gradyanı (t: 0 = ufuk altı, 1 = tepe; sınır dışı değerler sıkıştırılır). Ufuk soluk soğuk mavi, tepe koyu lacivert; sonlu.
        /// </summary>
        public static Color SkyGradient(float t)
        {
            var f = Mathf.Clamp01(float.IsNaN(t) ? 0f : t);
            var horizon = new Color(0.34f, 0.43f, 0.58f, 1f);
            var mid = new Color(0.16f, 0.23f, 0.38f, 1f);
            var top = new Color(0.07f, 0.10f, 0.20f, 1f);
            // Ufuk ışıltısı ilk %12'de; sonra tepeye doğru koyulaşma.
            var k = Mathf.Clamp01((f - 0.10f) / 0.9f);
            k = k * k * (3f - 2f * k);
            return k < 0.5f ? Color.Lerp(horizon, mid, k * 2f) : Color.Lerp(mid, top, (k - 0.5f) * 2f);
        }

        /// <summary>Kütük oturağının orta noktasından, ateşe bakan oturan askerin yönü (derece).</summary>
        public static float YawToward(Vector3 from, Vector3 target)
        {
            var d = target - from;
            d.y = 0f;
            return d.sqrMagnitude < 1e-6f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }
    }
}
