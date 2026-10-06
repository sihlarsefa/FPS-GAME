using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Weapons
{
    /// <summary>
    /// Silah modeline eklenen hafif sürücü (WeaponModelFactory ekler):
    ///  - parça pivotunu (model uzayı) ve namlu karbon birikimini MaterialPropertyBlock ile gölgelendiriciye yazar (paylaşılan malzemeler bozulmaz);
    ///  - viewmodel'de ortam (SH/prob) parlaklığına göre taban ışığı küresel değerini günceller (iç mekânda silah koyu kalmasın);
    ///  - atışta karbon artar (<see cref="NotifyShot"/>), zamanla temizlenir.
    /// Kontroller 0.15 sn'de bir yapılır; her karede yalnız bir sayaç karşılaştırması.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponMaterialDriver : MonoBehaviour
    {
        private static readonly int CarbonId = Shader.PropertyToID("_HarekatCarbon");
        private static readonly int PivotId = Shader.PropertyToID("_HarekatPartPivot");
        private static readonly int LowDetailId = Shader.PropertyToID("_HarekatGunLowDetail");
        private static readonly int FillBiasId = Shader.PropertyToID("_HarekatGunFillBias");

        // MaterialPropertyBlock ctor Unity native CreateImpl çağırır — statik alan başlatıcıda yasak
        // (tip ilk yüklenirken MonoBehaviour ctor bağlamı → TypeInitializationException).
        private static MaterialPropertyBlock _block;
        private static MaterialPropertyBlock Block => _block ??= new MaterialPropertyBlock();
        private static readonly Vector3[] ProbeDirs =
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
        };
        private static readonly Color[] ProbeColors = new Color[6];
        private static int _tier = 2;

        private readonly List<Renderer> _renderers = new List<Renderer>(8);
        private string _weaponId;
        private WeaponCategory _category;
        private float _muzzleZ;
        private float _zone;
        private float _appliedCarbon = -1f;
        private int _seenVersion = -1;
        private float _nextCheck;
        private float _nextFill;
        private bool _viewmodel;

        /// <summary>Kalite kademesi (0 Düşük … 3 Ultra). Düşük kademede mikro doku/çizik kapanır. QualityTierApplier çağırır.</summary>
        public static void SetTier(int tier)
        {
            _tier = tier;
            Shader.SetGlobalFloat(LowDetailId, tier <= 0 ? 1f : 0f);
        }

        /// <summary>Atış bildirimi (WeaponViewModel.OnFire): silahın karbonunu artırır. Ucuz (sözlük yazımı).</summary>
        public static void NotifyShot(WeaponDefinitionData weapon)
        {
            if (weapon == null || string.IsNullOrEmpty(weapon.WeaponId))
                return;
            WeaponCarbonState.RegisterShot(weapon.WeaponId, weapon.Category, Time.time);
        }

        /// <summary>Modelin tüm renderer'larını toplayıp ilk değerleri yazar. Factory kurulumdan sonra çağırır.</summary>
        internal void Initialize(WeaponModel model)
        {
            _viewmodel = model.ForViewmodel;
            _weaponId = model.Definition != null ? model.Definition.WeaponId : null;
            _category = model.Definition != null ? model.Definition.Category : WeaponCategory.None;

            _renderers.Clear();
            GetComponentsInChildren(true, _renderers);

            var pistol = WeaponStyles.IsPistol(model.Style);
            var muzzle = model.Muzzle != null ? transform.InverseTransformPoint(model.Muzzle.position).z : (pistol ? 0.17f : 0.6f);
            _muzzleZ = muzzle;
            _zone = WeaponWearMath.CarbonZoneLength(pistol ? 0.1f : 0.4f);
            _seenVersion = -1;
            ApplyAll(WeaponCarbonState.Get(_weaponId, Time.time));
        }

        private void OnEnable()
        {
            _seenVersion = -1;
            _nextCheck = 0f;
        }

        private void Update()
        {
            var now = Time.time;
            if (now >= _nextCheck)
            {
                _nextCheck = now + 0.15f;
                var v = WeaponCarbonState.Version;
                var carbon = WeaponCarbonState.Get(_weaponId, now);
                if (v != _seenVersion || Mathf.Abs(carbon - _appliedCarbon) > 0.004f)
                {
                    _seenVersion = v;
                    ApplyAll(carbon);
                }
            }

            if (_viewmodel && now >= _nextFill)
            {
                _nextFill = now + 0.4f;
                UpdateFill();
            }
        }

        private void ApplyAll(float carbon)
        {
            _appliedCarbon = carbon;
            var root = transform;
            for (var i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null)
                    continue;
                // Parça pivotu: mesh köşeleri parça-yerel; gölgelendirici model uzayına çıkarmak için ekler.
                var pivot = r.transform == root ? Vector3.zero : r.transform.localPosition;
                Block.Clear();
                Block.SetVector(PivotId, new Vector4(pivot.x, pivot.y, pivot.z, 0f));
                Block.SetVector(CarbonId, new Vector4(carbon, _muzzleZ, _zone, WeaponWearMath.SheenMultiplier(carbon)));
                r.SetPropertyBlock(Block);
            }
        }

        private void UpdateFill()
        {
            if (_tier <= 0)
            {
                Shader.SetGlobalFloat(FillBiasId, 0f);
                return;
            }

            try
            {
                SphericalHarmonicsL2 sh;
                LightProbes.GetInterpolatedProbe(transform.position, _renderers.Count > 0 ? _renderers[0] : null, out sh);
                sh.Evaluate(ProbeDirs, ProbeColors);
                var sum = 0f;
                for (var i = 0; i < ProbeColors.Length; i++)
                    sum += WeaponWearMath.Luminance(ProbeColors[i].r, ProbeColors[i].g, ProbeColors[i].b);
                Shader.SetGlobalFloat(FillBiasId, WeaponWearMath.FillBias(sum / ProbeColors.Length));
            }
            catch (System.Exception)
            {
                Shader.SetGlobalFloat(FillBiasId, 0f);
            }
        }
    }
}
