using System;

namespace Project.Application.Settings
{
    /// <summary>Fare girdi tercihleri (kalıcı veri). Varsayılan: ham girdi açık, ivme kapalı, yumuşatma yok.</summary>
    public sealed class MouseInputSettings
    {
        /// <summary>İşletim sistemi imleç ivmesini atlayan ham delta (Input System Mouse.delta Windows'ta ham okur).</summary>
        public bool RawInput = true;
        /// <summary>Oyun içi fare ivmesi (varsayılan kapalı; açılırsa <see cref="AccelStrength"/> kullanılır).</summary>
        public bool Acceleration;
        /// <summary>İvme gücü 0..1; hız (sayım/ms) başına eklenecek kazanç.</summary>
        public float AccelStrength = 0.15f;
        /// <summary>İvme tavanı (en çok kaç katına çıkar) 1..3.</summary>
        public float AccelCap = 1.8f;
        /// <summary>Yumuşatma süresi ms (0 = kapalı, 0..60).</summary>
        public float SmoothingMs;
        /// <summary>Y/X hassasiyet oranı 0.5..1.5 (1 = eşit).</summary>
        public float YxRatio = 1f;
        /// <summary>Fare DPI'ı (cm/360 hesabı için; 100..32000).</summary>
        public int Dpi = 800;

        public void Sanitize()
        {
            AccelStrength = SettingsMath.Clamp(AccelStrength, 0f, 1f, 0.15f);
            AccelCap = SettingsMath.Clamp(AccelCap, 1f, 3f, 1.8f);
            SmoothingMs = SettingsMath.Clamp(SmoothingMs, 0f, 60f, 0f);
            YxRatio = SettingsMath.Clamp(YxRatio, 0.5f, 1.5f, 1f);
            Dpi = SettingsMath.ClampInt(Dpi, 100, 32000);
        }

        public MouseInputSettings Clone() => (MouseInputSettings)MemberwiseClone();
    }

    /// <summary>
    /// Fare delta işlemcisi (saf, durumlu): ivme + üstel yumuşatma + Y/X oranı. İvme kapalı ve yumuşatma 0 iken çıktı
    /// girdiyle birebir aynıdır (yalnızca Y oranı uygulanır) - kasıtlı olarak "sıfır ivme" garantisi.
    /// </summary>
    public sealed class MouseDeltaProcessor
    {
        private float _sx;
        private float _sy;

        public MouseInputSettings Settings { get; set; } = new MouseInputSettings();

        public void Reset()
        {
            _sx = 0f;
            _sy = 0f;
        }

        /// <summary>İvme çarpanı: hız = |delta|/dt (sayım/ms). Kapalıysa 1.</summary>
        public static float AccelGain(MouseInputSettings s, float counts, float dtMs)
        {
            if (s == null || !s.Acceleration || dtMs <= 0.0001f)
                return 1f;
            var speed = counts / dtMs;
            var gain = 1f + s.AccelStrength * speed;
            return gain > s.AccelCap ? s.AccelCap : gain;
        }

        public void Process(float dx, float dy, float dt, out float ox, out float oy)
        {
            var s = Settings ?? new MouseInputSettings();
            var dtMs = dt * 1000f;
            var mag = (float)Math.Sqrt(dx * dx + dy * dy);
            var gain = AccelGain(s, mag, dtMs);
            dx *= gain;
            dy *= gain * s.YxRatio;

            if (s.SmoothingMs > 0.01f && dtMs > 0.0001f)
            {
                // Üstel yumuşatma: alfa = 1 - e^(-dt/tau). Toplam hareket kaybolmaz (kalan sonraki karelere taşar).
                var alpha = 1f - (float)Math.Exp(-dtMs / s.SmoothingMs);
                _sx += dx;
                _sy += dy;
                ox = _sx * alpha;
                oy = _sy * alpha;
                _sx -= ox;
                _sy -= oy;
                return;
            }

            _sx = 0f;
            _sy = 0f;
            ox = dx;
            oy = dy;
        }
    }

    /// <summary>cm/360 ve derece/sayım dönüşümleri (fare hassasiyetini oyunlar arası taşımak için).</summary>
    public static class SensitivityConverter
    {
        /// <summary>Sayım başına derece (oyun hassasiyeti) + DPI → 360 derece için fare hareketi (cm).</summary>
        public static float Cm360(float degreesPerCount, int dpi)
        {
            if (degreesPerCount <= 0.00001f || dpi <= 0)
                return float.PositiveInfinity;
            var counts = 360f / degreesPerCount;
            return counts / dpi * 2.54f;
        }

        public static float DegreesPerCount(float cm360, int dpi)
        {
            if (cm360 <= 0.0001f || dpi <= 0)
                return 0f;
            var counts = cm360 / 2.54f * dpi;
            return 360f / counts;
        }

        /// <summary>Başka oyunun yaw değeriyle (derece/sayım, örn. CS2 0.022, Valorant 0.07) bu oyunun hassasiyetini hesaplar.</summary>
        public static float FromOtherGame(float otherSensitivity, float otherYaw, float ourDegreesPerSensitivityUnit)
        {
            if (ourDegreesPerSensitivityUnit <= 0.000001f)
                return 0f;
            return otherSensitivity * otherYaw / ourDegreesPerSensitivityUnit;
        }
    }
}
