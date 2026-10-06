using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Su yüzeyi canlandırıcı: iki kayan prosedürel normal harita (ana + detay), kameraya göre fresnel benzeri pürüzsüzlük,
    /// kıyı köpüğü nabzı ve kamera su altındayken sis tonu. Malzeme tüm su yüzeylerinde ortaktır.
    /// </summary>
    public sealed class WaterSurface : MonoBehaviour
    {
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int DetailNormalId = Shader.PropertyToID("_DetailNormalMap");
        private static readonly int DetailAlbedoId = Shader.PropertyToID("_DetailAlbedoMap");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");

        private Material _water;
        private Material _foam;
        private Material _foam2;
        private WaterSurfaceShafts _shafts;
        private float _windAngle = WaterSurfaceMath.ReferenceWindDeg;
        private bool _windInit;
        private int _tier = -1;
        private static readonly int WaveRotId = Shader.PropertyToID("_WaveRot");
        private static readonly int GlitterId = Shader.PropertyToID("_GlitterStrength");
        private float _level;
        private TerrainModel _model;
        private bool _underwater;
        private Color _savedFog;
        private float _savedDensity;

        /// <summary>Su malzemesinin hareketli normal harita kopyasını üretir (MaterialLibrary örneği değişmez).</summary>
        public static Material CreateMaterial()
        {
            var wave = Shader.Find(WaveShaderName);
            if (wave != null)
                return CreateWaveMaterial(wave);
            var source = MaterialLibrary.Get(MaterialId.Water);
            if (source == null)
                return null;
            var m = new Material(source) { name = "HK_WaterAnimated" };
            if (m.HasProperty(BumpMapId))
            {
                m.SetTexture(BumpMapId, WaterSurfaceTextures.NormalA);
                m.SetTextureScale(BumpMapId, new Vector2(1f / 9f, 1f / 9f));
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1.2f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.06f, 0.30f, 0.38f, 0.82f));
                m.EnableKeyword("_NORMALMAP");
            }

            if (m.HasProperty(DetailNormalId))
            {
                m.SetTexture(DetailNormalId, WaterSurfaceTextures.NormalB);
                m.SetTexture(DetailAlbedoId, WaterSurfaceTextures.Gray);
                m.SetTextureScale(DetailAlbedoId, new Vector2(1f / 5f, 1f / 5f));
                if (m.HasProperty("_DetailNormalMapScale")) m.SetFloat("_DetailNormalMapScale", 0.7f);
                m.EnableKeyword("_DETAIL_MULX2");
            }

            return m;
        }

        private const string WaveShaderName = "HAREKAT/Water/Surface";

        /// <summary>Gerstner dalgalı özel su gölgelendiricisi malzemesi (Shader.Find ile bulunursa).</summary>
        private static Material CreateWaveMaterial(Shader shader)
        {
            var m = new Material(shader) { name = "HK_WaterWaves", renderQueue = 2990 };
            m.SetTexture(BumpMapId, WaterSurfaceTextures.NormalA);
            m.SetTextureScale(BumpMapId, new Vector2(1f / 9f, 1f / 9f));
            m.SetTexture(DetailNormalId, WaterSurfaceTextures.NormalB);
            m.SetTextureScale(DetailNormalId, new Vector2(1f / 5f, 1f / 5f));
            // Derin mavi-yeşil gövde, belirgin dalga normali, ölçülü fresnel (gri yansıma lekesini önler).
            m.SetColor("_BaseColor", new Color(0.06f, 0.34f, 0.40f, 0.80f));
            m.SetColor("_DeepColor", new Color(0.01f, 0.10f, 0.20f, 0.96f));
            m.SetFloat("_BumpScale", 1.3f);
            m.SetFloat("_DetailScale", 0.9f);
            m.SetFloat("_WaveAmp", 0.16f);
            m.SetFloat("_ReflectStrength", 0.62f);
            return m;
        }

        /// <summary>Kök nesneye bileşeni ve kıyı köpüğü şeritlerini ekler (hatalar yutulur).</summary>
        public static void Attach(GameObject root, MapLayout layout, TerrainModel model, Material water)
        {
            if (root == null || layout == null)
                return;
            var ws = root.AddComponent<WaterSurface>();
            ws._water = water;
            ws._level = layout.WaterLevel;
            ws._model = model;
            try
            {
                ws._foam = WaterSurfaceShore.Build(root.transform, layout, model, out ws._foam2);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            try { ws._shafts = new WaterSurfaceShafts(root.transform); }
            catch (System.Exception e) { Debug.LogWarning("[HAREKÂT] Su altı huzme atlandı: " + e.Message); }

            try { WaterSurfaceSplash.Install(root, layout.WaterLevel); }
            catch (System.Exception e) { Debug.LogWarning("[HAREKÂT] Su sıçrama atlandı: " + e.Message); }

            try { PlanarReflection.Install(ViewCamera()); }
            catch (System.Exception e) { Debug.LogWarning("[HAREKÂT] PlanarReflection.Install atlandı: " + e.Message); }
        }

        private static Camera ViewCamera()
        {
            var rigs = CameraRig.Active;
            if (rigs != null && rigs.Count > 0 && rigs[0] != null && rigs[0].WorldCamera != null)
                return rigs[0].WorldCamera;
            return Camera.main;
        }

        private void Update()
        {
            var t = Time.time;
            if (_water != null)
            {
                if (_water.HasProperty(BumpMapId))
                    _water.SetTextureOffset(BumpMapId, new Vector2(t * 0.012f, t * 0.007f));
                if (_water.HasProperty(DetailAlbedoId))
                    _water.SetTextureOffset(DetailAlbedoId, new Vector2(-t * 0.02f, t * 0.015f));
                else if (_water.HasProperty(DetailNormalId))
                    _water.SetTextureOffset(DetailNormalId, new Vector2(-t * 0.02f, t * 0.015f));
            }

            var cam = ViewCamera();
            UpdateFoam(t, cam);
            if (cam == null)
                return;

            if (_water != null)
            {
                var s = SkyWaterRules.WaterSmoothness(cam.transform.forward.y);
                if (_water.HasProperty(SmoothnessId)) _water.SetFloat(SmoothnessId, s);
                if (_water.HasProperty(GlossinessId)) _water.SetFloat(GlossinessId, s);
            }

            UpdateWaves(cam);
            UpdateUnderwater(cam);
        }

        /// <summary>Kıyı köpüğü: iki kaydırma fazı (yarım periyot arayla) çapraz solar; bant genişliği piksel-kararlı (1,2-2 m).</summary>
        private void UpdateFoam(float t, Camera cam)
        {
            if (_foam == null)
                return;
            var pulse = Mathf.Sin(t * 1.4f);
            var width = WaterSurfaceMath.FoamMinWidth;
            if (cam != null)
            {
                var dist = Mathf.Clamp(cam.transform.position.y - _level, 2f, 60f) + 12f;
                width = WaterSurfaceMath.FoamWidth(dist, cam.fieldOfView, cam.pixelHeight);
            }

            var sx = WaterSurfaceMath.FoamScaleX(width);
            Foam(_foam, t, 0, pulse, sx);
            if (_foam2 != null)
                Foam(_foam2, t, 1, pulse, sx * 0.85f);
        }

        private static void Foam(Material m, float t, int layer, float pulse, float scaleX)
        {
            var ph = WaterSurfaceMath.FoamPhase(t, 9f, layer);
            var w = WaterSurfaceMath.FoamWeight(ph);
            var c = m.color;
            c.a = WaterSurfaceMath.FoamAlpha(0.35f + 0.65f * w, pulse);
            m.color = c;
            // Kıyıya doğru gelip çekilen kayma: faz boyunca enine ofset, boyuna yavaş akış.
            m.mainTextureScale = new Vector2(scaleX, 1f);
            m.mainTextureOffset = new Vector2((ph - 0.5f) * 0.14f, t * (layer == 0 ? 0.05f : -0.035f) + layer * 0.37f);
        }

        /// <summary>Dalga yönü rüzgâra yavaşça (30 sn) uyar; güneş parıltısı kademe ile sınırlanır.</summary>
        private void UpdateWaves(Camera cam)
        {
            if (_water == null || !_water.HasProperty(WaveRotId))
                return;
            var w = WindSystem.Current;
            var target = WaterSurfaceMath.WindAngleDeg(w.DirX, w.DirZ);
            if (!_windInit) { _windAngle = target; _windInit = true; }
            else _windAngle = WaterSurfaceMath.ApproachAngle(_windAngle, target, Time.deltaTime);
            _water.SetFloat(WaveRotId, WaterSurfaceMath.WaveRotationRad(_windAngle));
            if (_tier < 0)
            {
                try { _tier = PlanarReflectionMath.TierFromQuality(QualitySettings.GetQualityLevel(), QualitySettings.names.Length); }
                catch { _tier = 2; }
                if (_water.HasProperty(GlitterId)) _water.SetFloat(GlitterId, WaterSurfaceMath.GlitterStrength(_tier));
            }
        }

        private void UpdateUnderwater(Camera cam)
        {
            var pos = cam.transform.position;
            var inWater = _model != null && _model.IsWater(pos.x, pos.z) || _model == null;
            var blend = inWater ? SkyWaterRules.UnderwaterBlend(pos.y, _level) : 0f;
            if (_shafts != null)
                _shafts.Tick(cam, _level, blend, _tier < 0 ? 2 : _tier, Time.time);
            if (blend > 0f)
            {
                if (!_underwater)
                {
                    _underwater = true;
                    _savedFog = RenderSettings.fogColor;
                    _savedDensity = RenderSettings.fogDensity;
                }

                var tint = new Color(0.06f, 0.24f, 0.28f);
                if (Atmosphere.CurrentTime == TimeOfDay.Gece)
                    tint *= 0.15f;
                RenderSettings.fogColor = Color.Lerp(_savedFog, tint, blend);
                RenderSettings.fogDensity = SkyWaterRules.UnderwaterFogDensity(_savedDensity, blend);
            }
            else if (_underwater)
            {
                _underwater = false;
                RenderSettings.fogColor = _savedFog;
                RenderSettings.fogDensity = _savedDensity;
            }
        }

        private void OnDestroy() => _shafts?.Dispose();

        private void OnDisable()
        {
            if (_underwater)
            {
                _underwater = false;
                RenderSettings.fogColor = _savedFog;
                RenderSettings.fogDensity = _savedDensity;
            }
        }
    }
}
