using Project.Core.Domain;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// HUD için saf (sahnesiz, deterministik) kurallar: boşta solma, şarjör kontrolü yaklaşık sayımı, duruş etiketi,
    /// stamina çubuğu görünürlüğü, el bombası uyarısı, HUD opaklığı. EditMode testlenir.
    /// </summary>
    public static class HudRules
    {
        // ---- Karanlık + kırmızı kimlik renk belirteçleri (hepsi UiTheme renk körü paletinden geçer) ----

        /// <summary>İmza kırmızısı #D43A2E. Renk körü paletinde UiTheme.EnemyRed alternatifine döner.</summary>
        public static Color SignatureRed => UiTheme.ColorBlindPalette == 0 ? UiTheme.Hex(0xD4, 0x3A, 0x2E) : UiTheme.EnemyRed;

        /// <summary>Derin kırmızı (hasar yönü yayları). Renk körü modunda alternatif palete geçer.</summary>
        public static Color DeepRed => UiTheme.ColorBlindPalette == 0 ? UiTheme.Hex(0x9C, 0x24, 0x1C) : UiTheme.Darken(UiTheme.EnemyRed, 0.25f);

        /// <summary>Öldürme onayı X rengi: imza kırmızısı (renk körü modunda beyaz).</summary>
        public static Color KillConfirm => UiTheme.ColorBlindPalette == 0 ? SignatureRed : UiTheme.KillMarker;

        /// <summary>Pusula yön işareti rengi.</summary>
        public static Color CompassTick => SignatureRed;

        /// <summary>Kill-feed vurgu rengi.</summary>
        public static Color KillFeedHighlight => SignatureRed;

        /// <summary>Daralan bölge sayaç halkası: kalan oran yüksekken beyaz, orta kehribar, düşükken imza kırmızısı.</summary>
        public static Color ZoneTimerColor(float remaining01)
        {
            var f = Mathf.Clamp01(remaining01);
            if (f > 0.5f)
                return Color.white;
            if (f > 0.2f)
                return UiTheme.Amber;
            return SignatureRed;
        }

        public const float AmmoIdleDelay = 4f;
        public const float AmmoFadeSeconds = 1.5f;
        public const float AmmoIdleAlpha = 0.28f;
        public const float MagCheckSeconds = 3f;
        public const float GrenadeWarnRadiusFactor = 2f;

        /// <summary>Mermi sayacı opaklığı: son etkinlikten <see cref="AmmoIdleDelay"/> sn sonra <see cref="AmmoIdleAlpha"/>'ya solar.</summary>
        public static float AmmoAlpha(float idleSeconds)
        {
            if (idleSeconds <= AmmoIdleDelay)
                return 1f;
            var t = Mathf.Clamp01((idleSeconds - AmmoIdleDelay) / AmmoFadeSeconds);
            return Mathf.Lerp(1f, AmmoIdleAlpha, t);
        }

        /// <summary>Tarkov tarzı yaklaşık şarjör durumu (kesin sayı vermez).</summary>
        public static string MagCheckLabel(int ammo, int magazineSize, bool chambered = true)
        {
            if (ammo <= 0)
                return chambered ? "BOŞ" : "BOŞ — NAMLUDA YOK";
            if (magazineSize <= 0)
                return "—";
            var f = Mathf.Clamp01(ammo / (float)magazineSize);
            if (f >= 0.999f)
                return "DOLU";
            if (f > 0.75f)
                return "DOLUYA YAKIN";
            if (f > 0.55f)
                return "YARIDAN FAZLA";
            if (f > 0.4f)
                return "YARIM";
            if (f > 0.2f)
                return "YARIDAN AZ";
            return "BİTMEK ÜZERE";
        }

        public static string StanceLabel(Stance stance)
        {
            switch (stance)
            {
                case Stance.Crouching: return "ÇÖMEL";
                case Stance.Prone: return "YATIK";
                default: return "AYAKTA";
            }
        }

        /// <summary>Stamina çubuğu yalnızca dolu değilken (veya tükenmişken) görünür.</summary>
        public static bool StaminaVisible(float normalized, bool exhausted) => exhausted || normalized < 0.985f;

        /// <summary>Çubuk opaklığı hedefi (dolu olmayan her durumda görünür; kritik düşükte belirgin).</summary>
        public static float StaminaTargetAlpha(float normalized, bool exhausted)
        {
            if (!StaminaVisible(normalized, exhausted))
                return 0f;
            return exhausted || normalized < 0.25f ? 1f : 0.75f;
        }

        /// <summary>El bombası uyarı eşiği: yarıçapın <see cref="GrenadeWarnRadiusFactor"/> katı içinde, patlamamış parçalı.</summary>
        public static bool GrenadeIsThreat(float distance, float dangerRadius, bool detonated)
        {
            return !detonated && dangerRadius > 0f && distance <= dangerRadius * GrenadeWarnRadiusFactor;
        }

        /// <summary>Uyarı şiddeti 0..1: yakınlaştıkça ve fitil kısaldıkça artar.</summary>
        public static float GrenadeUrgency(float distance, float dangerRadius, float fuseRemaining)
        {
            if (dangerRadius <= 0f)
                return 0f;
            var near = 1f - Mathf.Clamp01(distance / (dangerRadius * GrenadeWarnRadiusFactor));
            var fuse = 1f - Mathf.Clamp01(fuseRemaining / 4f);
            return Mathf.Clamp01(0.55f * near + 0.45f * fuse);
        }

        /// <summary>Hedefin bakışa göre göreli açısı (-180..180; 0 = tam karşıda, + = sağda).</summary>
        public static float RelativeBearing(Vector3 origin, float yawDegrees, Vector3 target)
        {
            return HudFormat.DeltaAngle(yawDegrees, HudFormat.Bearing(origin, target));
        }

        /// <summary>Kullanıcı HUD opaklığı (0.3..1).</summary>
        public static float ClampOpacity(float value) => float.IsNaN(value) ? 1f : Mathf.Clamp(value, 0.3f, 1f);
    }

    /// <summary>
    /// HUD'un ortak biçem değerleri. Opaklık, ayarlar paneli bağlandığında <see cref="Opacity"/> üzerinden yazılır
    /// (GameSettings.HudOpacity → AdvancedDisplay.Apply → HudStyle.Opacity bağlı).
    /// </summary>
    public static class HudStyle
    {
        private static float _opacity = 1f;

        public static float Opacity
        {
            get => _opacity;
            set => _opacity = HudRules.ClampOpacity(value);
        }

        /// <summary>Güvenli alan kenar boşluğu (referans px): pencerenin güvenli alanından türetilir.</summary>
        public static Vector2 SafeMargin()
        {
            var w = Mathf.Max(1, Screen.width);
            var h = Mathf.Max(1, Screen.height);
            var safe = Screen.safeArea;
            var left = safe.xMin / w * UiTheme.ReferenceWidth;
            var bottom = safe.yMin / h * UiTheme.ReferenceHeight;
            return new Vector2(Mathf.Max(0f, left), Mathf.Max(0f, bottom));
        }
    }
}
