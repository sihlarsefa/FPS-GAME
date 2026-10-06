using System;
using System.Collections.Generic;

namespace Project.Application.Services
{
    /// <summary>Anahtar kareler arası geçiş eğrisi.</summary>
    public enum PoseEase { Linear = 0, Smooth, EaseIn, EaseOut, Overshoot, Anticipate, Snap }

    /// <summary>Tek kanal anahtarı: zaman (0..1) ve değer.</summary>
    public readonly struct PoseKey
    {
        public readonly float Time;
        public readonly float Value;
        public readonly PoseEase Ease;

        public PoseKey(float time, float value, PoseEase ease = PoseEase.Smooth)
        {
            Time = time; Value = value; Ease = ease;
        }
    }

    /// <summary>Anahtarlı tek kanal (saf matematik). Önceki anahtardan sonrakine, sonraki anahtarın eğrisiyle geçer.</summary>
    public sealed class PoseTrack
    {
        private readonly List<PoseKey> _keys = new List<PoseKey>(8);

        public int Count => _keys.Count;

        public PoseTrack Key(float time, float value, PoseEase ease = PoseEase.Smooth)
        {
            var t = Clamp01(time);
            var i = _keys.Count;
            while (i > 0 && _keys[i - 1].Time > t) i--;
            _keys.Insert(i, new PoseKey(t, value, ease));
            return this;
        }

        public float Evaluate(float t)
        {
            if (_keys.Count == 0) return 0f;
            if (t <= _keys[0].Time) return _keys[0].Value;
            var last = _keys[_keys.Count - 1];
            if (t >= last.Time) return last.Value;
            for (var i = 1; i < _keys.Count; i++)
            {
                var b = _keys[i];
                if (t > b.Time) continue;
                var a = _keys[i - 1];
                var span = b.Time - a.Time;
                var u = span <= 1e-6f ? 1f : (t - a.Time) / span;
                return a.Value + (b.Value - a.Value) * Curve(b.Ease, u);
            }

            return last.Value;
        }

        public static float Curve(PoseEase ease, float u)
        {
            u = Clamp01(u);
            switch (ease)
            {
                case PoseEase.Smooth: return u * u * (3f - 2f * u);
                case PoseEase.EaseIn: return u * u;
                case PoseEase.EaseOut: return 1f - (1f - u) * (1f - u);
                case PoseEase.Snap: return u < 0.5f ? 0f : 1f;
                case PoseEase.Overshoot:
                {
                    // easeOutBack: hedefi hafifçe aşıp oturur.
                    const float c1 = 1.9f, c3 = c1 + 1f;
                    var x = u - 1f;
                    return 1f + c3 * x * x * x + c1 * x * x;
                }
                case PoseEase.Anticipate:
                {
                    // easeInBack: önce geri çekilir sonra fırlar.
                    const float c1 = 1.7f, c3 = c1 + 1f;
                    return c3 * u * u * u - c1 * u * u;
                }
                default: return u;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    public enum ReloadKind { Rifle, Machinegun, Pistol, BoltAction, Shotgun }

    /// <summary>Doldurma kanalları: poz eğimi, şarjör yolu, sol el uzanma, mekanik (sürgü/kızak/kurma kolu) ve sarsıntı.</summary>
    public sealed class ViewmodelPoseTimeline
    {
        public ReloadKind Kind;
        public bool Empty;
        /// <summary>Silah gövdesinin eğilme miktarı (0..1).</summary>
        public readonly PoseTrack Tilt = new PoseTrack();
        /// <summary>Şarjör yolu: 0 yuvada, 1 tam çıkmış (düşme).</summary>
        public readonly PoseTrack MagTravel = new PoseTrack();
        /// <summary>Sol elin şarjör cebine/şarjöre uzanma ağırlığı.</summary>
        public readonly PoseTrack HandReach = new PoseTrack();
        /// <summary>Kızak/sürgü/kurma kolu geri çekme (0..1).</summary>
        public readonly PoseTrack Mechanism = new PoseTrack();
        /// <summary>Şarjör oturunca/kapak kapanınca sarsıntı darbesi (0..1).</summary>
        public readonly PoseTrack Jolt = new PoseTrack();
        /// <summary>Şarjörün elde görünür olduğu an (1) / yuvaya girmiş (0 yok = gizle). Cepten yeni şarjör: 1.</summary>
        public readonly PoseTrack NewMagInHand = new PoseTrack();
        /// <summary>Makineli tüfek besleme kapağı açıklığı (0 kapalı, 1 açık).</summary>
        public readonly PoseTrack Cover = new PoseTrack();
        /// <summary>Sol elin kutudan (0) kapak/besleme tablasına (1) geçişi: kemer yerleştirme.</summary>
        public readonly PoseTrack BeltLay = new PoseTrack();
        /// <summary>Yeni şarjör/kutu yuvaya girmeye başladığı an (0..1); yedek şarjör bu aralıkta ele→yuvaya taşınır.</summary>
        public float SeatTime = RifleMagIn;

        // Foley planıyla hizalı anahtar zamanları (ReloadFoleyPlanner; test eşleşmeyi doğrular).
        public const float PistolMagOut = 0.15f, PistolMagIn = 0.62f;
        public const float RifleMagOut = 0.14f, RifleMagIn = 0.66f;

        public static ViewmodelPoseTimeline Build(ReloadKind kind, bool empty)
        {
            var tl = new ViewmodelPoseTimeline { Kind = kind, Empty = empty };
            switch (kind)
            {
                case ReloadKind.Pistol: BuildPistol(tl, empty); break;
                case ReloadKind.Machinegun: BuildMachinegun(tl, empty); break;
                case ReloadKind.BoltAction: BuildBolt(tl, empty); break;
                case ReloadKind.Shotgun: BuildShotgun(tl, empty); break;
                default: BuildRifle(tl, empty); break;
            }

            return tl;
        }

        private static void BuildRifle(ViewmodelPoseTimeline tl, bool empty)
        {
            // Eğ (öngörü: önce hafif ters), şarjör bırak, cepten yenisi, tak, şamar / kurma kolu.
            tl.Tilt.Key(0f, 0f).Key(0.03f, -0.12f, PoseEase.Smooth).Key(0.12f, 1f, PoseEase.Overshoot)
                .Key(0.84f, 1f).Key(1f, 0f, PoseEase.Smooth);
            tl.MagTravel.Key(0f, 0f).Key(RifleMagOut - 0.01f, 0f).Key(0.30f, 1f, PoseEase.EaseIn)
                .Key(0.60f, 1f).Key(RifleMagIn, 0f, PoseEase.Overshoot);
            tl.HandReach.Key(0f, 0f).Key(0.10f, 1f, PoseEase.EaseOut).Key(0.20f, 0.6f).Key(0.40f, 1f)
                .Key(0.72f, 1f).Key(0.80f, 0.55f).Key(empty ? 0.86f : 0.84f, 1f).Key(0.96f, 0f);
            tl.NewMagInHand.Key(0f, 0f).Key(0.38f, 0f, PoseEase.Snap).Key(0.40f, 1f).Key(RifleMagIn, 1f).Key(RifleMagIn + 0.01f, 0f, PoseEase.Snap);
            tl.Jolt.Key(0f, 0f).Key(RifleMagIn - 0.01f, 0f).Key(RifleMagIn + 0.01f, 1f, PoseEase.EaseOut).Key(RifleMagIn + 0.07f, 0f);
            if (empty)
                tl.Mechanism.Key(0f, 0f).Key(0.82f, 0f).Key(0.88f, 1f, PoseEase.Anticipate).Key(0.92f, 0f, PoseEase.Snap);
            else
                tl.Jolt.Key(0.82f, 0f).Key(0.84f, 0.6f, PoseEase.EaseOut).Key(0.90f, 0f);
        }

        private static void BuildPistol(ViewmodelPoseTimeline tl, bool empty)
        {
            tl.Tilt.Key(0f, 0f).Key(0.02f, -0.1f).Key(0.10f, 1f, PoseEase.Overshoot).Key(0.88f, 1f).Key(1f, 0f);
            tl.MagTravel.Key(0f, 0f).Key(PistolMagOut - 0.01f, 0f).Key(0.30f, 1f, PoseEase.EaseIn)
                .Key(0.55f, 1f).Key(PistolMagIn, 0f, PoseEase.Overshoot);
            tl.HandReach.Key(0f, 0f).Key(0.10f, 1f, PoseEase.EaseOut).Key(0.30f, 0.5f).Key(0.40f, 1f).Key(0.72f, 1f).Key(0.90f, 0f);
            tl.SeatTime = PistolMagIn;
            tl.NewMagInHand.Key(0f, 0f).Key(0.36f, 0f, PoseEase.Snap).Key(0.38f, 1f).Key(PistolMagIn, 1f).Key(PistolMagIn + 0.01f, 0f, PoseEase.Snap);
            tl.Jolt.Key(0f, 0f).Key(PistolMagIn - 0.01f, 0f).Key(PistolMagIn + 0.01f, 1f, PoseEase.EaseOut).Key(PistolMagIn + 0.07f, 0f);
            // Kızak bırakma (boşsa): belirgin geri-ileri; taktikte yok.
            if (empty)
                tl.Mechanism.Key(0f, 0f).Key(0.72f, 0f).Key(0.76f, 1f, PoseEase.EaseOut).Key(0.80f, 0f, PoseEase.Snap);
        }

        private static void BuildMachinegun(ViewmodelPoseTimeline tl, bool empty)
        {
            // Üst kapak açılır, boş kutu düşer, yeni kutu cepten gelir, kemer tablaya serilir, kapak kapanır.
            tl.SeatTime = 0.64f;
            tl.Tilt.Key(0f, 0f).Key(0.10f, 1f, PoseEase.Overshoot).Key(0.88f, 1f).Key(1f, 0f);
            tl.Cover.Key(0f, 0f).Key(0.12f, 0f).Key(0.22f, 1f, PoseEase.Overshoot).Key(0.76f, 1f).Key(0.84f, 0f, PoseEase.EaseIn);
            tl.MagTravel.Key(0f, 0f).Key(0.32f, 0f).Key(0.46f, 1f, PoseEase.EaseIn).Key(0.54f, 1f).Key(0.64f, 0f, PoseEase.Overshoot);
            tl.HandReach.Key(0f, 0f).Key(0.14f, 1f, PoseEase.EaseOut).Key(0.42f, 1f).Key(0.76f, 1f).Key(0.94f, 0f);
            tl.BeltLay.Key(0f, 0f).Key(0.24f, 0f).Key(0.32f, 1f).Key(0.40f, 0f).Key(0.66f, 0f).Key(0.72f, 1f, PoseEase.EaseOut).Key(0.80f, 1f).Key(0.86f, 0f);
            tl.NewMagInHand.Key(0f, 0f).Key(0.42f, 0f, PoseEase.Snap).Key(0.44f, 1f).Key(0.62f, 1f).Key(0.64f, 0f, PoseEase.Snap);
            tl.Jolt.Key(0f, 0f).Key(0.62f, 0f).Key(0.64f, 0.7f, PoseEase.EaseOut).Key(0.70f, 0f).Key(0.82f, 0f).Key(0.85f, 1f, PoseEase.EaseOut).Key(0.92f, 0f);
            if (empty)
                tl.Mechanism.Key(0f, 0f).Key(0.88f, 0f).Key(0.92f, 1f, PoseEase.EaseOut).Key(0.96f, 0f, PoseEase.Snap);
        }

        private static void BuildBolt(ViewmodelPoseTimeline tl, bool empty)
        {
            tl.Tilt.Key(0f, 0f).Key(0.10f, 1f, PoseEase.Overshoot).Key(0.88f, 1f).Key(1f, 0f);
            tl.MagTravel.Key(0f, 0f).Key(0.14f, 0f).Key(0.30f, 1f, PoseEase.EaseIn).Key(0.50f, 1f).Key(0.66f, 0f, PoseEase.Overshoot);
            tl.HandReach.Key(0f, 0f).Key(0.10f, 1f, PoseEase.EaseOut).Key(0.70f, 1f).Key(0.84f, 0f);
            tl.SeatTime = 0.66f;
            tl.NewMagInHand.Key(0f, 0f).Key(0.38f, 0f, PoseEase.Snap).Key(0.40f, 1f).Key(0.65f, 1f).Key(0.66f, 0f, PoseEase.Snap);
            tl.Jolt.Key(0f, 0f).Key(0.65f, 0f).Key(0.67f, 1f, PoseEase.EaseOut).Key(0.74f, 0f);
            // Sürgü her zaman bir tur çevrilir (boş/dolu fark etmez; boşsa daha sert).
            tl.Mechanism.Key(0f, 0f).Key(0.72f, 0f).Key(0.78f, 1f, PoseEase.Smooth).Key(0.84f, 1f).Key(empty ? 0.90f : 0.92f, 0f, PoseEase.EaseIn);
        }

        private static void BuildShotgun(ViewmodelPoseTimeline tl, bool empty)
        {
            // Mermi mermi: eğ, fişekleri tek tek besle (ShellStage), sonda pompa. Boşsa pompa sert, doluysa kısa.
            tl.Tilt.Key(0f, 0f).Key(0.08f, 1f, PoseEase.Overshoot).Key(0.88f, 1f).Key(1f, 0f);
            tl.HandReach.Key(0f, 0f).Key(0.08f, 1f, PoseEase.EaseOut).Key(0.86f, 1f).Key(0.94f, 0f);
            tl.Mechanism.Key(0f, 0f).Key(0.90f, 0f).Key(0.94f, 1f, PoseEase.EaseOut).Key(0.98f, 0f, PoseEase.Snap);
            tl.Jolt.Key(0f, 0f).Key(0.94f, 0f).Key(0.95f, empty ? 1f : 0.5f, PoseEase.EaseOut).Key(1f, 0f);
        }

        public const float ShellLoadStart = 0.10f, ShellLoadEnd = 0.84f;

        /// <summary>Pompalı doldurma evresi: t anında kaçıncı mermi (index) ve mermi içi ilerleme (s 0..1); döngü dışında false.</summary>
        public static bool ShellStage(float t, int count, out int index, out float s)
        {
            index = 0; s = 0f;
            if (count <= 0 || t <= ShellLoadStart || t >= ShellLoadEnd) return false;
            var u = (t - ShellLoadStart) / (ShellLoadEnd - ShellLoadStart) * count;
            index = Math.Min(count - 1, (int)u);
            s = u - index;
            return true;
        }

        /// <summary>Doldurma sırasında ateş edilirse kesilebilir mi (en az bir mermi odada, pompa evresi başlamadan).</summary>
        public static bool CanInterruptShotgun(float t, int count) => ShellsLoaded(t, count) >= 1 && t < 0.9f;

        /// <summary>Mermi başına süre ~0.55 sn (FoleyLogic.ShotgunPlan ile aynı hesap).</summary>
        public static int ShotgunShellCount(float durationSeconds)
        {
            var d = durationSeconds > 0.5f ? durationSeconds : 3f;
            var count = (int)(d / 0.55f);
            return Math.Max(2, Math.Min(8, count));
        }

        /// <summary>Pompalıda mermi döngüsü: ilerleme + interrupt için kaç mermi yüklendi (0..count).</summary>
        public static int ShellsLoaded(float t, int count, float start = 0.04f, float spanFraction = 0.9f)
        {
            if (count <= 0 || t <= start) return 0;
            var per = spanFraction / count;
            var n = (int)((t - start) / per);
            return Math.Max(0, Math.Min(count, n));
        }

        // ------------------------------------------------------------------ İnceleme (inspect)

        /// <summary>İki evreli silah inceleme kanalları (süre 0..1).</summary>
        public sealed class InspectTimeline
        {
            public readonly PoseTrack Raise = new PoseTrack();
            public readonly PoseTrack Yaw = new PoseTrack();
            public readonly PoseTrack Roll = new PoseTrack();
            public readonly PoseTrack Pitch = new PoseTrack();
            /// <summary>Sol el kabza/şarjör bölgesine uzanır (evre 2).</summary>
            public readonly PoseTrack Hand = new PoseTrack();
            /// <summary>Kızak/sürgü ile kamara kontrolü (0..1).</summary>
            public readonly PoseTrack Check = new PoseTrack();
        }

        public const float InspectDuration = 3.2f;
        public const float InspectPhase2 = 0.5f;
        private static InspectTimeline _inspect;

        public static InspectTimeline Inspect()
        {
            if (_inspect != null) return _inspect;
            var i = new InspectTimeline();
            i.Raise.Key(0f, 0f).Key(0.14f, 1f, PoseEase.EaseOut).Key(0.86f, 1f).Key(1f, 0f, PoseEase.Smooth);
            // Evre 1: sağa döndür/yatır (sağ yan, namlu ucu görünür). Evre 2: ters yana çevir, kamara kontrolü.
            i.Yaw.Key(0f, 0f).Key(0.14f, 0f).Key(0.40f, 55f, PoseEase.Smooth).Key(InspectPhase2, 55f).Key(0.76f, -40f, PoseEase.Smooth)
                .Key(0.86f, -40f).Key(1f, 0f, PoseEase.Smooth);
            i.Roll.Key(0f, 0f).Key(0.14f, 0f).Key(0.40f, -35f, PoseEase.Smooth).Key(InspectPhase2, -35f).Key(0.76f, 70f, PoseEase.Overshoot)
                .Key(0.86f, 70f).Key(1f, 0f, PoseEase.Smooth);
            i.Pitch.Key(0f, 0f).Key(0.14f, -6f, PoseEase.EaseOut).Key(0.40f, -2f).Key(0.76f, -10f).Key(1f, 0f);
            i.Hand.Key(0f, 0f).Key(0.56f, 0f).Key(0.66f, 1f, PoseEase.EaseOut).Key(0.80f, 1f).Key(0.88f, 0f);
            i.Check.Key(0f, 0f).Key(0.60f, 0f).Key(0.66f, 1f, PoseEase.EaseOut).Key(0.72f, 0f, PoseEase.EaseIn);
            _inspect = i;
            return i;
        }

        // ------------------------------------------------------------------ Kuşanma / indirme eğrileri

        private static readonly PoseTrack DrawTrack = new PoseTrack().Key(0f, 0f).Key(0.72f, 1f, PoseEase.Overshoot).Key(1f, 1f);
        private static readonly PoseTrack HolsterTrack = new PoseTrack().Key(0f, 0f).Key(0.18f, -0.06f, PoseEase.Smooth).Key(1f, 1f, PoseEase.EaseIn);

        /// <summary>Kaldırma ilerlemesi (0..1 giriş): hedefi hafifçe aşıp oturur (silah yukarı sıçrar).</summary>
        public static float DrawProgress(float x) => DrawTrack.Evaluate(x < 0f ? 0f : (x > 1f ? 1f : x));

        /// <summary>İndirme ilerlemesi: önce hafif yukarı (öngörü), sonra hızlanarak iner.</summary>
        public static float HolsterProgress(float x) => HolsterTrack.Evaluate(x < 0f ? 0f : (x > 1f ? 1f : x));

        // ------------------------------------------------------------------ Koşu (taktik poz) geçişi

        private static readonly PoseTrack SprintIn = new PoseTrack().Key(0f, 0f).Key(0.2f, 0.06f, PoseEase.EaseIn).Key(0.85f, 1.06f, PoseEase.EaseOut).Key(1f, 1f, PoseEase.Smooth);
        private static readonly PoseTrack SprintOut = new PoseTrack().Key(0f, 0f).Key(0.8f, 0.9f, PoseEase.EaseOut).Key(1f, 1f, PoseEase.Smooth);

        /// <summary>Koşu poz ağırlığı (blend 0..1 → 0..~1.06). girerken: aşım; çıkarken (rising=false): yumuşak.</summary>
        public static float SprintWeight(float blend, bool rising)
        {
            var b = blend < 0f ? 0f : (blend > 1f ? 1f : blend);
            return rising ? SprintIn.Evaluate(b) : 1f - SprintOut.Evaluate(1f - b);
        }

        /// <summary>Geçiş sırasında namlu eğilip toparlanma darbesi (0..1..0).</summary>
        public static float SprintTransitionBump(float blend)
        {
            var b = blend < 0f ? 0f : (blend > 1f ? 1f : blend);
            return b * (1f - b) * 4f;
        }
    }
}
