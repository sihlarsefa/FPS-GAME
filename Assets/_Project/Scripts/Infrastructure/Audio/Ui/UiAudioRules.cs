using System;

namespace Project.Infrastructure.Audio.Ui
{
    /// <summary>Lobi/arayüz ses katmanları; her katmanın sabit bir mikser payı vardır (bkz. UiAudioRules.LayerDb).</summary>
    public enum UiLayer { Bed, Distant, Radio, UiNav, UiConfirm, Stinger }

    /// <summary>
    /// Saf (Unity'siz) lobi ses kuralları: seviye hiyerarşisi (dB), bastırma (ducking) takipçisi,
    /// tekrar önleyen perde çeşitlemesi, hover perde merdiveni ve uzak topçu zamanlayıcısı.
    /// Kaynak fikirler: UI sesleri yüzlerce kez tekrarlanır (hover neredeyse sessiz, onay yükselir, iptal alçalır);
    /// hiyerarşide ortam en altta, müzik/stinger en üstte durur ve üst katman çalarken alt katman bastırılır.
    /// </summary>
    public static class UiAudioRules
    {
        /// <summary>Katman başına referans düzey (dB, 0 dB = tam ölçek). Ortam -22 ... stinger -8.</summary>
        public static float LayerDb(UiLayer layer)
        {
            switch (layer)
            {
                case UiLayer.Bed: return -22f;
                case UiLayer.Distant: return -16f;
                case UiLayer.Radio: return -14f;
                case UiLayer.UiNav: return -26f;
                case UiLayer.UiConfirm: return -12f;
                default: return -8f;
            }
        }

        public static float LayerGain(UiLayer layer) => (float)Math.Pow(10.0, LayerDb(layer) / 20.0);

        /// <summary>Ortam yatağının (ateş/rüzgâr) bu olay sırasında ne kadar bastırılacağı (dB, negatif). Hover bastırmaz.</summary>
        public static float DuckDepthDb(UiSfx sfx)
        {
            switch (sfx)
            {
                case UiSfx.MatchFound: return -9f;
                case UiSfx.CountdownGo: return -7f;
                case UiSfx.Confirm: return -4f;
                case UiSfx.Error: return -3f;
                case UiSfx.Press: return -2f;
                default: return 0f;
            }
        }

        /// <summary>Olayın bastırmayı ne kadar süre tutacağı (sn); stinger kuyruğu bitene kadar.</summary>
        public static float DuckHoldSeconds(UiSfx sfx)
        {
            switch (sfx)
            {
                case UiSfx.MatchFound: return 0.9f;
                case UiSfx.CountdownGo: return 0.5f;
                case UiSfx.Confirm: return 0.25f;
                case UiSfx.Error: return 0.2f;
                case UiSfx.Press: return 0.12f;
                default: return 0f;
            }
        }

        /// <summary>İki bastırma talebinden derin olanı (dB, negatif sayılar).</summary>
        public static float DeeperDuck(float aDb, float bDb) => Math.Min(Math.Min(aDb, 0f), Math.Min(bDb, 0f));

        /// <summary>Yarı ton farkını frekans çarpanına çevirir.</summary>
        public static float SemitoneRatio(float semitones) => (float)Math.Pow(2.0, semitones / 12.0);

        /// <summary>
        /// Önceki perdeye en az <paramref name="minStep"/> yarı ton uzak, ±<paramref name="spread"/> içinde yeni perde (yarı ton).
        /// Aynı sesin art arda aynı perdede çalıp "makine tüfeği" etkisi yapmasını önler.
        /// </summary>
        public static float NextVariation(float previous, float unit01, float spread, float minStep)
        {
            spread = Math.Max(0f, spread);
            minStep = Math.Max(0f, Math.Min(minStep, spread));
            var u = Math.Min(1f, Math.Max(0f, float.IsNaN(unit01) ? 0.5f : unit01));
            var v = -spread + u * 2f * spread;
            if (Math.Abs(v - previous) >= minStep) return v;
            // Çok yakın: önceki değerden uzaklaştırıp sınırın içinde tut.
            var up = previous + minStep;
            var down = previous - minStep;
            if (v >= previous) return up <= spread ? up : down;
            return down >= -spread ? down : up;
        }

        /// <summary>Hızlı sürüklenen fare için hover perde merdiveni: her ardışık hover +0.5 yarı ton, 3 yarı tonda tavan.</summary>
        public static float HoverSemitone(int chain) => Math.Min(3f, Math.Max(0, chain) * 0.5f);

        /// <summary>Ardışık hover sayacı: aralık <paramref name="chainWindow"/> sn'den uzunsa sıfırlanır.</summary>
        public static int NextHoverChain(int chain, float sinceLast, float chainWindow = 0.6f) =>
            sinceLast > chainWindow ? 0 : Math.Min(chain + 1, 6);

        /// <summary>Uzak patlamanın alçak geçiren kesim frekansı (Hz): 1.5 km'de 1800, 6 km'de ~300 (üstel).</summary>
        public static float DistanceCutoffHz(float km)
        {
            km = Math.Min(8f, Math.Max(0.5f, km));
            var t = (km - 1.5f) / 4.5f;
            return (float)(1800.0 * Math.Pow(300.0 / 1800.0, Math.Min(1.4f, Math.Max(-0.3f, t))));
        }

        /// <summary>Uzaklıkla düzey kaybı (dB, negatif): km başına ~2.5 dB (yalnız uzak katman; hava sönümü ayrıca kesimle verilir).</summary>
        public static float DistanceLossDb(float km) => -2.5f * Math.Min(8f, Math.Max(0f, km));

        /// <summary>Sonraki topçu salvosuna kadar sn: üstel (Poisson) dağılım, 18..90 sn'ye kısıtlı. unit01 0..1.</summary>
        public static float NextArtilleryDelay(float unit01, float meanSeconds = 38f)
        {
            var u = Math.Min(0.999f, Math.Max(0.001f, float.IsNaN(unit01) ? 0.5f : unit01));
            var t = -(float)Math.Log(1.0 - u) * meanSeconds;
            return Math.Min(90f, Math.Max(18f, t));
        }

        /// <summary>Salvodaki atış sayısı: 1 (%50), 2 (%32), 3 (%18).</summary>
        public static int SalvoCount(float unit01) => unit01 < 0.5f ? 1 : unit01 < 0.82f ? 2 : 3;

        /// <summary>Salvodaki atışlar arası boşluk (sn), 0.7..2.2.</summary>
        public static float SalvoGap(float unit01) => 0.7f + 1.5f * Math.Min(1f, Math.Max(0f, unit01));

        /// <summary>Telsiz ile topçu üst üste binmesin: telsiz sonrası topçu için en az bekleme.</summary>
        public static bool ArtilleryAllowed(float now, float lastRadioEnd, float clearance = 4f) => now - lastRadioEnd >= clearance;
    }

    /// <summary>
    /// Bastırma zarfı takipçisi (dB alanında): hedef derinliğe hızlı iner (atak), bırakınca yavaş döner (salıverme).
    /// Üst katman (stinger/onay) çalarken ateş-rüzgâr yatağı geri çekilir, sonra nefes alarak geri gelir.
    /// </summary>
    public sealed class DuckFollower
    {
        private readonly float _attackSeconds;
        private readonly float _releaseSeconds;

        /// <summary>Anlık bastırma (dB, 0 veya negatif).</summary>
        public float CurrentDb { get; private set; }

        public DuckFollower(float attackSeconds = 0.06f, float releaseSeconds = 0.8f)
        {
            _attackSeconds = Math.Max(1e-3f, attackSeconds);
            _releaseSeconds = Math.Max(1e-3f, releaseSeconds);
        }

        /// <summary>dt saniye ilerletir; hedef (dB, ≤0) yönünde atak/salıverme sabitiyle yaklaşır. Çarpan döner.</summary>
        public float Step(float dt, float targetDb)
        {
            dt = Math.Max(0f, Math.Min(0.25f, dt));
            targetDb = Math.Min(0f, float.IsNaN(targetDb) ? 0f : targetDb);
            var tau = targetDb < CurrentDb ? _attackSeconds : _releaseSeconds;
            var k = 1f - (float)Math.Exp(-dt / tau);
            CurrentDb += (targetDb - CurrentDb) * k;
            if (CurrentDb > -0.01f && targetDb >= 0f) CurrentDb = 0f;
            return Gain;
        }

        public float Gain => (float)Math.Pow(10.0, CurrentDb / 20.0);

        public void Reset() => CurrentDb = 0f;
    }

    /// <summary>Hover merdiveni ve perde çeşitlemesi için küçük durum tutucusu (Unity'siz; zaman dışarıdan verilir).</summary>
    public sealed class UiPitchState
    {
        private float _lastHover = -999f;
        private int _chain;
        private float _lastVar;
        private uint _rng;

        public UiPitchState(uint seed = 0xA5F10u) { _rng = seed | 1u; }

        private float Rand()
        {
            _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
            return (_rng & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>Hover için perde çarpanı: merdiven + küçük (±0.3 yarı ton) çeşitleme.</summary>
        public float HoverPitch(float now)
        {
            _chain = UiAudioRules.NextHoverChain(_chain, now - _lastHover);
            _lastHover = now;
            return UiAudioRules.SemitoneRatio(UiAudioRules.HoverSemitone(_chain) + (Rand() - 0.5f) * 0.6f);
        }

        /// <summary>Tıklama/onay gibi sesler için tekrar önleyen ±0.7 yarı ton çeşitleme çarpanı.</summary>
        public float VariedPitch()
        {
            _lastVar = UiAudioRules.NextVariation(_lastVar, Rand(), 0.7f, 0.25f);
            return UiAudioRules.SemitoneRatio(_lastVar);
        }
    }
}
