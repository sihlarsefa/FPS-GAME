using System;

namespace Project.Application.Movement
{
    /// <summary>Koşudan çıkış türü: bekleme süresi buna göre değişir.</summary>
    public enum SprintExitKind
    {
        Sprint = 0,
        Slide = 1,
        Mantle = 2,
        HardLanding = 3,
    }

    /// <summary>
    /// Koşudan ateşe geçiş süresi (CoD "sprint-out time"): silah koşuda aşağıdadır; koşu bırakılınca silah kaldırılana dek
    /// ateş/ADS yoktur. Süre silah ağırlığıyla büyür, uzun (taktik) koşudan sonra uzar: MW2019'da hafif SMG ~0,17 s,
    /// AR ~0,25 s, LMG/keskin ~0,35-0,45 s, taktik koşu çıkışı yaklaşık %50 daha uzundur. Saf, deterministik (yalnız dt).
    /// </summary>
    public sealed class SprintToFireTimer
    {
        /// <summary>Referans (3,5 kg) silahın standart koşu çıkış süresi (s).</summary>
        public const float BaseRecoverSeconds = 0.22f;
        /// <summary>Uzun (taktik) koşu çıkışı için tavan çarpan.</summary>
        public const float TacticalMultiplier = 1.5f;
        /// <summary>Taktik etkisi bu koşu süresinden sonra başlar (s).</summary>
        public const float TacticalStartSeconds = 1.2f;
        /// <summary>Taktik etkisi bu koşu süresinde tavana varır (s).</summary>
        public const float TacticalFullSeconds = 3.5f;

        private float _remaining;
        private float _total;
        private float _sprintTime;
        private bool _wasSprinting;

        /// <summary>Kalan bekleme (s).</summary>
        public float Remaining => _remaining;

        /// <summary>Koşuda ya da çıkış beklemesinde ateş edilemez.</summary>
        public bool CanFire => !_wasSprinting && _remaining <= 0f;

        /// <summary>Silahın kalkma ilerlemesi 0..1 (viewmodel koşu pozundan çıkış için). Koşuda 0.</summary>
        public float ReadyProgress
        {
            get
            {
                if (_wasSprinting) return 0f;
                if (_total <= 0.0001f) return 1f;
                var p = 1f - _remaining / _total;
                return p < 0f ? 0f : p > 1f ? 1f : p;
            }
        }

        /// <summary>Silah ağırlığına göre çarpan: 3,5 kg = 1; her kg +%9 (0,75..1,6).</summary>
        public static float WeaponWeightFactor(float weaponKg)
        {
            var kg = float.IsNaN(weaponKg) ? 3.5f : weaponKg;
            var f = 1f + (kg - 3.5f) * 0.09f;
            return f < 0.75f ? 0.75f : f > 1.6f ? 1.6f : f;
        }

        /// <summary>Çıkış türüne göre taban çarpan (kayma bitişi hızlı, sert iniş yavaş).</summary>
        public static float ExitKindFactor(SprintExitKind kind)
        {
            switch (kind)
            {
                case SprintExitKind.Slide: return 0.7f;
                case SprintExitKind.Mantle: return 1.4f;
                case SprintExitKind.HardLanding: return 1.7f;
                default: return 1f;
            }
        }

        /// <summary>Koşu süresine göre taktik çarpan 1..<see cref="TacticalMultiplier"/> (doğrusal).</summary>
        public static float TacticalFactor(float sprintSeconds)
        {
            var t = (sprintSeconds - TacticalStartSeconds) / (TacticalFullSeconds - TacticalStartSeconds);
            t = float.IsNaN(t) ? 0f : t < 0f ? 0f : t > 1f ? 1f : t;
            return 1f + (TacticalMultiplier - 1f) * t;
        }

        /// <summary>Toplam bekleme (s) hesabı; testlenebilir saf formül.</summary>
        public static float RecoverSeconds(float weaponKg, float sprintSeconds, SprintExitKind kind, float burden)
        {
            var b = float.IsNaN(burden) ? 0f : burden < 0f ? 0f : burden > 1f ? 1f : burden;
            return BaseRecoverSeconds * WeaponWeightFactor(weaponKg) * TacticalFactor(sprintSeconds) * ExitKindFactor(kind) * (1f + 0.15f * b);
        }

        /// <summary>
        /// Kare adımı. <paramref name="sprinting"/> koşu sürüyor mu; koşu bitince bekleme başlar. Koşu yeniden başlarsa bekleme sıfırlanır
        /// (silah tekrar iner). <paramref name="forcedExit"/> koşu dışı (kayma/tırmanma/iniş) bir olay bu karede bittiyse türü.
        /// </summary>
        public void Tick(float dt, bool sprinting, float weaponKg, float burden, SprintExitKind forcedExit = SprintExitKind.Sprint, bool hasForcedExit = false)
        {
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;

            if (sprinting)
            {
                _sprintTime += dt;
                _wasSprinting = true;
                _remaining = 0f;
                _total = 0f;
                return;
            }

            if (_wasSprinting)
            {
                _wasSprinting = false;
                _total = RecoverSeconds(weaponKg, _sprintTime, forcedExit, burden);
                _remaining = _total;
                _sprintTime = 0f;
            }
            else if (hasForcedExit)
            {
                var t = RecoverSeconds(weaponKg, 0f, forcedExit, burden);
                if (t > _remaining)
                {
                    _total = t;
                    _remaining = t;
                }
            }

            _sprintTime = 0f;
            _remaining = Math.Max(0f, _remaining - dt);
        }

        /// <summary>Doğma/ölüm sonrası sıfırlama.</summary>
        public void Reset()
        {
            _remaining = 0f;
            _total = 0f;
            _sprintTime = 0f;
            _wasSprinting = false;
        }
    }
}
