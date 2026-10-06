using System;
using System.Collections.Generic;
using Project.Core.Domain;

namespace Project.Infrastructure.Audio.Foley
{
    /// <summary>Silah/teçhizat foley adımları. SADECE sona ekleyin (dosya adı katmanı <see cref="FoleyNaming.StepLayer"/>).</summary>
    public enum FoleyStep
    {
        None = 0,
        MagRelease, MagOut, MagPouch, MagIn, MagSlap, Bolt, ChargingHandle, ShellInsert,
        SelectorClick, DryFire, WeaponEquip, WeaponHolster,
        AdsRustle, SprintRattle, PlateCarrier, ProneDown, LandBody
    }

    /// <summary>Ateş katmanları (Resources klip adı öneki).</summary>
    public enum FireLayer { Bang = 0, Mech, Thump, TailOutdoor, TailIndoor, TailValley, Distant, Suppressed, Shell, Belt }

    /// <summary>Klip adlandırma sözleşmesi: <c>Resources/Audio/Weapons/&lt;weaponId&gt;/&lt;layer&gt;_&lt;n&gt;.wav</c>.</summary>
    public static class FoleyNaming
    {
        public const string Root = "Audio/Weapons";
        public const string ClassFolder = "_class";

        public static string StepLayer(FoleyStep s)
        {
            switch (s)
            {
                case FoleyStep.MagRelease: return "magrelease";
                case FoleyStep.MagOut: return "magout";
                case FoleyStep.MagPouch: return "pouch";
                case FoleyStep.MagIn: return "magin";
                case FoleyStep.MagSlap: return "slap";
                case FoleyStep.Bolt: return "bolt";
                case FoleyStep.ChargingHandle: return "chargehandle";
                case FoleyStep.ShellInsert: return "shellinsert";
                case FoleyStep.SelectorClick: return "selector";
                case FoleyStep.DryFire: return "dryfire";
                case FoleyStep.WeaponEquip: return "equip";
                case FoleyStep.WeaponHolster: return "holster";
                case FoleyStep.AdsRustle: return "ads";
                case FoleyStep.SprintRattle: return "sprint";
                case FoleyStep.PlateCarrier: return "plate";
                case FoleyStep.ProneDown: return "prone";
                case FoleyStep.LandBody: return "land";
                default: return null;
            }
        }

        public static string FireLayerName(FireLayer l)
        {
            switch (l)
            {
                case FireLayer.Bang: return "bang";
                case FireLayer.Mech: return "mech";
                case FireLayer.Thump: return "thump";
                case FireLayer.TailOutdoor: return "tail_outdoor";
                case FireLayer.TailIndoor: return "tail_indoor";
                case FireLayer.TailValley: return "tail_valley";
                case FireLayer.Distant: return "distant";
                case FireLayer.Suppressed: return "supp";
                case FireLayer.Belt: return "belt";
                default: return "shell";
            }
        }

        /// <summary>Silaha özel klasör (Resources yolu, uzantısız).</summary>
        public static string WeaponFolder(string weaponId) => Root + "/" + weaponId;

        /// <summary>Kalibre sınıfı yedek klasörü.</summary>
        public static string ClassFolderPath(string caliberFolder) => Root + "/" + ClassFolder + "/" + caliberFolder;

        /// <summary>Klip adı <c>layer_n</c>; katmanı ayıklar (tanınmayan ad için null). "tail_outdoor_2" -> "tail_outdoor".</summary>
        public static string LayerOfClipName(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return null;
            var i = clipName.LastIndexOf('_');
            if (i <= 0 || i == clipName.Length - 1)
                return clipName.ToLowerInvariant();
            for (var k = i + 1; k < clipName.Length; k++)
                if (clipName[k] < '0' || clipName[k] > '9')
                    return clipName.ToLowerInvariant();
            return clipName.Substring(0, i).ToLowerInvariant();
        }
    }

    /// <summary>Adımın çalma özellikleri (taban ses, menzil, perde aralığı).</summary>
    public readonly struct FoleyStepInfo
    {
        public readonly float Volume;
        public readonly float MaxDistance;
        public readonly float PitchJitter;

        public FoleyStepInfo(float volume, float maxDistance, float pitchJitter)
        {
            Volume = volume;
            MaxDistance = maxDistance;
            PitchJitter = pitchJitter;
        }

        public static FoleyStepInfo For(FoleyStep s)
        {
            switch (s)
            {
                case FoleyStep.MagRelease: return new FoleyStepInfo(0.55f, 25f, 0.03f);
                case FoleyStep.MagOut: return new FoleyStepInfo(0.65f, 30f, 0.03f);
                case FoleyStep.MagPouch: return new FoleyStepInfo(0.45f, 20f, 0.05f);
                case FoleyStep.MagIn: return new FoleyStepInfo(0.7f, 32f, 0.03f);
                case FoleyStep.MagSlap: return new FoleyStepInfo(0.6f, 28f, 0.04f);
                case FoleyStep.Bolt:
                case FoleyStep.ChargingHandle: return new FoleyStepInfo(0.8f, 40f, 0.03f);
                case FoleyStep.ShellInsert: return new FoleyStepInfo(0.55f, 25f, 0.05f);
                case FoleyStep.SelectorClick: return new FoleyStepInfo(0.4f, 14f, 0.03f);
                case FoleyStep.DryFire: return new FoleyStepInfo(0.55f, 30f, 0.03f);
                case FoleyStep.WeaponEquip: return new FoleyStepInfo(0.5f, 24f, 0.04f);
                case FoleyStep.WeaponHolster: return new FoleyStepInfo(0.45f, 22f, 0.04f);
                case FoleyStep.AdsRustle: return new FoleyStepInfo(0.28f, 10f, 0.06f);
                case FoleyStep.SprintRattle: return new FoleyStepInfo(0.35f, 30f, 0.06f);
                case FoleyStep.PlateCarrier: return new FoleyStepInfo(0.3f, 16f, 0.06f);
                case FoleyStep.ProneDown: return new FoleyStepInfo(0.6f, 28f, 0.05f);
                case FoleyStep.LandBody: return new FoleyStepInfo(0.55f, 30f, 0.05f);
                default: return new FoleyStepInfo(0.5f, 20f, 0.03f);
            }
        }
    }

    /// <summary>Şarjör değiştirme planındaki tek adım: tamamlanma oranına (0..1) göre zaman.</summary>
    public readonly struct ReloadStepPlan
    {
        public readonly FoleyStep Step;
        public readonly float Fraction;

        public ReloadStepPlan(FoleyStep step, float fraction)
        {
            Step = step;
            Fraction = fraction;
        }
    }

    /// <summary>
    /// Doldurma foley planı (saf): şarjör bırakma, çıkarma, kese, takma, tokat, sürgü/şarjör kolu.
    /// Taktik doldurmada (şarjörde mermi var) sürgü adımı yoktur; boş doldurmada vardır.
    /// </summary>
    public static class ReloadFoleyPlanner
    {
        public static ReloadStepPlan[] Plan(WeaponCategory category, bool emptyReload, float durationSeconds)
        {
            var list = new List<ReloadStepPlan>(8);
            switch (category)
            {
                case WeaponCategory.Pistol:
                    list.Add(new ReloadStepPlan(FoleyStep.MagRelease, 0.04f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagOut, 0.15f));       // ViewmodelPoseTimeline.PistolMagOut/In ile eş
                    list.Add(new ReloadStepPlan(FoleyStep.MagPouch, 0.40f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagIn, 0.62f));
                    if (emptyReload) list.Add(new ReloadStepPlan(FoleyStep.Bolt, 0.86f));
                    else list.Add(new ReloadStepPlan(FoleyStep.MagSlap, 0.82f));
                    break;
                case WeaponCategory.Shotgun:
                    return ShotgunPlan(emptyReload, durationSeconds);
                case WeaponCategory.Lmg:
                    list.Add(new ReloadStepPlan(FoleyStep.MagRelease, 0.06f));   // kapak mandalı
                    list.Add(new ReloadStepPlan(FoleyStep.MagOut, 0.18f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagPouch, 0.42f));     // şerit/kutu
                    list.Add(new ReloadStepPlan(FoleyStep.MagIn, 0.62f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagSlap, 0.76f));      // kapak kapanır
                    if (emptyReload) list.Add(new ReloadStepPlan(FoleyStep.ChargingHandle, 0.90f));
                    break;
                case WeaponCategory.Sniper:
                case WeaponCategory.Dmr:
                    list.Add(new ReloadStepPlan(FoleyStep.MagRelease, 0.05f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagOut, 0.15f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagPouch, 0.38f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagIn, 0.62f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagSlap, 0.72f));
                    if (emptyReload) list.Add(new ReloadStepPlan(FoleyStep.Bolt, 0.88f));
                    break;
                default:
                    list.Add(new ReloadStepPlan(FoleyStep.MagRelease, 0.05f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagOut, 0.14f));       // ViewmodelPoseTimeline.RifleMagOut/In ile eş
                    list.Add(new ReloadStepPlan(FoleyStep.MagPouch, 0.38f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagIn, 0.66f));
                    list.Add(new ReloadStepPlan(FoleyStep.MagSlap, 0.72f));
                    if (emptyReload) list.Add(new ReloadStepPlan(FoleyStep.ChargingHandle, 0.88f));
                    break;
            }

            return list.ToArray();
        }

        private static ReloadStepPlan[] ShotgunPlan(bool emptyReload, float durationSeconds)
        {
            // Mermi başına ~0.55 sn; son kısımda pompa (boşsa).
            var d = durationSeconds > 0.5f ? durationSeconds : 3f;
            var count = (int)(d / 0.55f);
            if (count < 2) count = 2;
            if (count > 8) count = 8;
            var list = new List<ReloadStepPlan>(count + 1);
            var span = emptyReload ? 0.82f : 0.94f;
            for (var i = 0; i < count; i++)
                list.Add(new ReloadStepPlan(FoleyStep.ShellInsert, 0.04f + span * i / count));
            if (emptyReload)
                list.Add(new ReloadStepPlan(FoleyStep.Bolt, 0.92f));
            return list.ToArray();
        }
    }

    /// <summary>
    /// İlerlemeye (0..1) bağlı adım tetikleyici (saf). <see cref="Begin"/> ile plan yüklenir; <see cref="Advance"/>
    /// geçilen adımları bir kez döndürür. İlerleme geri giderse (iptal) yeniden Begin gerekir.
    /// </summary>
    public sealed class ReloadFoleyTracker
    {
        private ReloadStepPlan[] _plan = Array.Empty<ReloadStepPlan>();
        private int _next;

        public bool Active { get; private set; }
        public int Remaining => Active ? _plan.Length - _next : 0;

        public void Begin(ReloadStepPlan[] plan)
        {
            _plan = plan ?? Array.Empty<ReloadStepPlan>();
            _next = 0;
            Active = _plan.Length > 0;
        }

        public void Cancel()
        {
            Active = false;
            _next = 0;
        }

        /// <summary>İlerleme <paramref name="progress"/> olunca vadesi gelen adımları <paramref name="due"/> listesine ekler.</summary>
        public void Advance(float progress, List<FoleyStep> due)
        {
            if (!Active || due == null)
                return;
            while (_next < _plan.Length && progress >= _plan[_next].Fraction)
                due.Add(_plan[_next++].Step);
            if (progress >= 1f || _next >= _plan.Length)
                Active = false;
        }
    }

    /// <summary>
    /// Koşu teçhizat tıkırtısı modeli (saf): adım mesafesine göre tetikler; hız arttıkça yoğunluk ve sıklık artar.
    /// Plaka taşıyıcı sesi koşuya başlarken ve yön/duruş değişince tek seferlik çıkar.
    /// </summary>
    public sealed class GearRattleModel
    {
        public const float SprintSpeed = 5.2f;
        public const float WalkSpeed = 1.5f;
        public const float StrideSprint = 1.9f;

        private float _stride;
        private bool _wasSprinting;

        /// <summary>Kare güncellemesi; tıkırtı tetiklenirse true ve [0..1] yoğunluk döner.</summary>
        public bool Tick(float horizontalSpeed, float dt, bool grounded, out float intensity, out bool plateShift)
        {
            intensity = 0f;
            plateShift = false;
            var sprinting = grounded && horizontalSpeed >= SprintSpeed * 0.9f;
            if (sprinting && !_wasSprinting)
                plateShift = true;
            _wasSprinting = sprinting;

            if (!grounded || !(horizontalSpeed > WalkSpeed) || !(dt > 0f))
            {
                return false;
            }

            _stride += horizontalSpeed * dt;
            var stride = sprinting ? StrideSprint : StrideSprint * 1.6f;
            if (_stride < stride)
                return false;
            _stride -= stride;
            var t = (horizontalSpeed - WalkSpeed) / (SprintSpeed - WalkSpeed);
            intensity = t < 0f ? 0f : t > 1f ? 1f : t;
            intensity = 0.25f + 0.75f * intensity;
            return true;
        }

        public void Reset()
        {
            _stride = 0f;
            _wasSprinting = false;
        }
    }

    /// <summary>Düşme hızına göre iniş gövde sesi şiddeti (saf).</summary>
    public static class BodyFoleyRules
    {
        public static bool LandingAudible(float fallSpeed) => fallSpeed >= 3f;

        /// <summary>0..1: 3 m/s'de 0.25, 14 m/s'de 1.</summary>
        public static float LandingIntensity(float fallSpeed)
        {
            if (!(fallSpeed > 3f)) return 0.25f;
            var t = (fallSpeed - 3f) / 11f;
            return 0.25f + 0.75f * (t > 1f ? 1f : t);
        }
    }
}
