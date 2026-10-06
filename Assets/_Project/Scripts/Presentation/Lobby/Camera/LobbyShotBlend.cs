using UnityEngine;

namespace Project.Presentation.Lobby.CameraWork
{
    /// <summary>
    /// İki çekim arasında saf enterpolasyon. Kanallar ayrı zamanlanır (sinematik his):
    /// konum ease-in-out kübik + geçiş ortasında yana hafif yay (pan arc), bakış hedefi konumun biraz gerisinden gelir (kamera dönüşü
    /// dolly'yi izler), FOV erken biter, odak ve bulanıklık %25 gecikmeyle yetişir (odak çekme).
    /// </summary>
    public static class LobbyShotBlend
    {
        /// <summary>Hedef gecikmesi: bakış, konumdan bu kadar geç başlar.</summary>
        public const float TargetLag = 0.12f;
        public const float FovEnd = 0.8f;
        public const float FocusDelay = 0.25f;

        /// <summary>Yay çarpanı: geçiş uzunluğunun bu oranı kadar yana taşar (üst sınır <see cref="MaxArcMeters"/>).</summary>
        public const float ArcFactor = 0.06f;
        public const float MaxArcMeters = 0.35f;

        public static LobbyShot Evaluate(in LobbyShot a, in LobbyShot b, float t)
        {
            t = LobbyEasing.Clamp01(t);
            if (t <= 0f) return a;
            if (t >= 1f) return b;

            var pos = LobbyEasing.InOutCubic(t);
            var look = LobbyEasing.InOutCubic(LobbyEasing.Delayed(t, TargetLag));
            var fov = LobbyEasing.Smootherstep(LobbyEasing.Early(t, FovEnd));
            var focus = LobbyEasing.Smoothstep(LobbyEasing.Delayed(t, FocusDelay));
            var tone = LobbyEasing.Smoothstep(t);

            var position = Vector3.Lerp(a.Position, b.Position, pos) + ArcOffset(a.Position, b.Position, t);
            var target = Vector3.Lerp(a.Target, b.Target, look);

            return new LobbyShot(
                position,
                target,
                Mathf.Lerp(a.FieldOfView, b.FieldOfView, fov),
                FocusLerp(a.FocusDistance, b.FocusDistance, focus),
                Mathf.Lerp(a.Blur, b.Blur, focus),
                Mathf.Lerp(a.ExposureEv, b.ExposureEv, tone),
                Mathf.Lerp(a.ShakeScale, b.ShakeScale, tone),
                Mathf.Lerp(a.RollDegrees, b.RollDegrees, tone));
        }

        /// <summary>Konumlar arası yatay yay: sin(pi t) çanı, yürüyüş yönüne dik (yukarı eksenle çapraz çarpım).</summary>
        public static Vector3 ArcOffset(Vector3 from, Vector3 to, float t)
        {
            var delta = to - from;
            delta.y = 0f;
            var len = delta.magnitude;
            if (len < 0.05f) return Vector3.zero;

            var side = Vector3.Cross(Vector3.up, delta / len);
            var amount = Mathf.Min(len * ArcFactor, MaxArcMeters) * Mathf.Sin(Mathf.PI * LobbyEasing.Clamp01(t));
            return side * amount;
        }

        /// <summary>Odak mesafesi logaritmik enterpole edilir: 1,2 m ile 8 m arasında geçişin ortası 3,1 m olur (algı oransaldır).</summary>
        public static float FocusLerp(float a, float b, float t)
        {
            a = Mathf.Max(a, 0.05f);
            b = Mathf.Max(b, 0.05f);
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), t));
        }

        /// <summary>
        /// Geçiş süresi (sn): kısa mesafe 0,9 sn, uzak/büyük FOV değişimi 2,2 sn. Pozisyon + bakış + FOV farkından türetilir.
        /// </summary>
        public static float DurationFor(in LobbyShot a, in LobbyShot b)
        {
            var move = Vector3.Distance(a.Position, b.Position);
            var turn = Vector3.Distance(a.Target, b.Target) * 0.25f;
            var zoom = Mathf.Abs(a.FieldOfView - b.FieldOfView) * 0.03f;
            return Mathf.Clamp(0.9f + move * 0.5f + turn + zoom, 0.9f, 2.2f);
        }
    }
}
