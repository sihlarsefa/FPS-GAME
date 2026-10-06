using System;

namespace Project.Application.Combat.Suppression
{
    /// <summary>Bastırma ayarları (ContentOverrides ile değiştirilebilsin diye tek yerde).</summary>
    public sealed class SuppressionConfig
    {
        /// <summary>Bu mesafeden (m) yakın geçen mermi bastırır.</summary>
        public float NearMissRadius = 3f;
        /// <summary>Tek mermiden gelen en yüksek şiddet katkısı.</summary>
        public float MaxPerShot = 0.45f;
        /// <summary>Şiddet saniyedeki sönüm hızı.</summary>
        public float DecayPerSecond = 0.35f;
        /// <summary>Ses boğulması süresi (sn).</summary>
        public float MuffleSeconds = 0.5f;
        /// <summary>Boğulma için asgari tek-atış şiddeti.</summary>
        public float MuffleMinShot = 0.15f;
        /// <summary>Nişangah sallanma çarpanı: 1 + şiddet * bu değer.</summary>
        public float SwayGain = 2.0f;
        /// <summary>NPC siper arama eşiği.</summary>
        public float NpcCoverThreshold = 0.4f;
        /// <summary>Vinyet nabız hızı (Hz).</summary>
        public float PulseHz = 2.2f;
    }

    /// <summary>Saf geometri/kural: mermi hattının hedefe yakın geçişi.</summary>
    public static class SuppressionRules
    {
        /// <summary>
        /// Mermi hattı (o + d*t, 0..uzunluk) ile hedef noktası arasındaki en yakın mesafe.
        /// Dönüş: mesafe; mermi hedefin arkasından başlıyor/uzaklaşıyorsa (t&lt;0) başlangıç mesafesi.
        /// d birim vektör olmalı (değilse normalize edilir).
        /// </summary>
        public static float MissDistance(float ox, float oy, float oz, float dx, float dy, float dz,
            float rayLength, float px, float py, float pz)
        {
            var len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-6f) return float.MaxValue;
            dx /= len; dy /= len; dz /= len;
            var vx = px - ox; var vy = py - oy; var vz = pz - oz;
            var t = vx * dx + vy * dy + vz * dz;
            if (t < 0f) t = 0f;
            if (t > rayLength) t = rayLength;
            var cx = ox + dx * t - px; var cy = oy + dy * t - py; var cz = oz + dz * t - pz;
            return (float)Math.Sqrt(cx * cx + cy * cy + cz * cz);
        }

        /// <summary>Kaçırma mesafesinden tek-atış şiddeti (0..MaxPerShot). Yakın = güçlü, yarıçap dışı = 0.</summary>
        public static float ShotIntensity(float missDistance, SuppressionConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (missDistance < 0f || missDistance >= cfg.NearMissRadius) return 0f;
            var k = 1f - missDistance / cfg.NearMissRadius;
            return cfg.MaxPerShot * k * k * 0.5f + cfg.MaxPerShot * k * 0.5f;
        }

        /// <summary>Kalite kademesine göre vinyet kullanılsın mı (0 = en düşük). Düşük kademede yalnız ses/sallanma.</summary>
        public static bool VignetteEnabled(int pipelineTier) => pipelineTier >= 1;
    }

    /// <summary>
    /// Bir hedefin (oyuncu veya NPC) bastırma durumu. Unity'siz; Presentation her karede Tick çağırır.
    /// </summary>
    public sealed class SuppressionState
    {
        private readonly SuppressionConfig _cfg;
        private float _level;
        private float _muffleLeft;
        private float _pulse;

        public SuppressionState(SuppressionConfig cfg = null) { _cfg = cfg ?? new SuppressionConfig(); }

        /// <summary>0..1 toplam bastırma şiddeti.</summary>
        public float Level => _level;
        /// <summary>Ses boğulması aktif mi.</summary>
        public bool IsMuffled => _muffleLeft > 0f;
        /// <summary>0..1 boğulma gücü (süre boyunca doğrusal azalır) — düşük geçiren filtre için.</summary>
        public float MuffleAmount => _cfg.MuffleSeconds <= 0f ? 0f : Math.Min(1f, _muffleLeft / _cfg.MuffleSeconds);
        /// <summary>Nişangah sallanma çarpanı (>= 1).</summary>
        public float AimSwayMultiplier => 1f + _level * _cfg.SwayGain;

        /// <summary>Ekran kenarı vinyet alfası (0..~0.6): şiddetle ölçekli, nabız gibi atar.</summary>
        public float VignetteAlpha(int pipelineTier = 2)
        {
            if (!SuppressionRules.VignetteEnabled(pipelineTier) || _level <= 0f) return 0f;
            var pulse = 0.85f + 0.15f * (float)Math.Sin(_pulse * 2.0 * Math.PI);
            return Math.Min(0.6f, _level * 0.6f * pulse);
        }

        /// <summary>Mermi yakın geçti. Dönüş: bu atıştan gelen şiddet (0 ise etkisiz).</summary>
        public float RegisterNearMiss(float missDistance)
        {
            var s = SuppressionRules.ShotIntensity(missDistance, _cfg);
            if (s <= 0f) return 0f;
            _level = Math.Min(1f, _level + s);
            if (s >= _cfg.MuffleMinShot) _muffleLeft = _cfg.MuffleSeconds;
            return s;
        }

        /// <summary>Mesafe dışı kaynaklar (gelen MG ateşi, patlama) için doğrudan şiddet ekler; muffle=true ise ses boğulur.</summary>
        public void AddImpulse(float amount, bool muffle = false)
        {
            if (amount <= 0f) return;
            _level = Math.Min(1f, _level + amount);
            if (muffle || amount >= _cfg.MuffleMinShot) _muffleLeft = _cfg.MuffleSeconds;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _level = Math.Max(0f, _level - _cfg.DecayPerSecond * dt);
            _muffleLeft = Math.Max(0f, _muffleLeft - dt);
            _pulse += dt * _cfg.PulseHz;
            if (_pulse > 1000f) _pulse -= 1000f;
        }

        /// <summary>NPC için: bastırma eşiği aşıldıysa siper aramalı.</summary>
        public bool ShouldSeekCover => _level >= _cfg.NpcCoverThreshold;

        public void Reset() { _level = 0f; _muffleLeft = 0f; _pulse = 0f; }
    }
}
