using System.Collections.Generic;
using UnityEngine;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World.Lighting
{
    /// <summary>Planlanan ışıkları URP additional light olarak üretir; ateş titreşimi ve lamba kafası emissive mesh.</summary>
    public sealed class PoiLights : MonoBehaviour
    {
        private struct Flick { public Light L; public float Base, Amp, Phase; }
        private readonly List<Flick> _flick = new List<Flick>();
        private readonly List<Light> _lights = new List<Light>();
        private readonly List<KeyValuePair<Material, Color>> _heads = new List<KeyValuePair<Material, Color>>();
        private int _active = -1;

        // Harita genelinde toplam ışık/gölge bütçesi (POI başına değil).
        private static int _totalLights, _totalShadows;
        public static void ResetBudget() { _totalLights = 0; _totalShadows = 0; }

        /// <summary>Gece/şafak/akşam aktif, gündüz kapalı (saf kural).</summary>
        public static bool IsActiveAt(TimeOfDay t) => t != TimeOfDay.Gunduz;

        public static PoiLights Build(Transform parent, PoiSiteType type, Vector3 center, float radius, int seed, Vector3 viewer)
        {
            int tier = QualitySettings.GetQualityLevel();
            var specs = PoiLightPlanner.ApplyBudget(PoiLightPlanner.Plan(type, center, radius, seed), viewer, tier);
            int room = Mathf.Max(0, PoiLightPlanner.MaxLights(tier) - _totalLights);
            if (specs.Count > room) specs.RemoveRange(room, specs.Count - room);
            int shRoom = Mathf.Max(0, PoiLightPlanner.MaxShadowCasters(tier) - _totalShadows);
            for (int i = 0; i < specs.Count; i++)
            {
                var sp = specs[i];
                if (sp.Shadow) { if (shRoom > 0) { shRoom--; _totalShadows++; } else { sp.Shadow = false; specs[i] = sp; } }
            }
            _totalLights += specs.Count;
            var go = new GameObject("PoiLights");
            go.transform.SetParent(parent, false);
            var pl = go.AddComponent<PoiLights>();
            foreach (var s in specs) pl.Spawn(s);
            pl.Refresh(true);
            return pl;
        }

        private void Spawn(PoiLightSpec s)
        {
            var go = new GameObject("PoiLight_" + s.Kind);
            go.transform.SetParent(transform, false);
            go.transform.position = s.Position;
            var l = go.AddComponent<Light>();
            l.color = s.Color;
            l.range = s.Range;
            l.intensity = s.Intensity;
            l.renderMode = LightRenderMode.ForcePixel;
            l.shadows = s.Shadow ? LightShadows.Soft : LightShadows.None;
            if (s.Kind == PoiLightKind.Floodlight)
            {
                l.type = LightType.Spot;
                l.spotAngle = 70f;
                go.transform.rotation = Quaternion.LookRotation(s.Direction);
            }
            else l.type = LightType.Point;
            _lights.Add(l);

            if (s.Kind != PoiLightKind.BarrelFire) AddHead(go.transform, s.Color, this);
            if (s.Flicker > 0f)
                _flick.Add(new Flick { L = l, Base = s.Intensity, Amp = s.Flicker, Phase = Mathf.Abs(s.Position.x * 3.7f + s.Position.z) });
        }

        private static void AddHead(Transform t, Color c, PoiLights owner)
        {
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "LampHead";
            Object.Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(t, false);
            head.transform.localScale = Vector3.one * 0.35f;
            var r = head.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = c * 0.4f;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 3f);
            r.sharedMaterial = m;
            owner._heads.Add(new KeyValuePair<Material, Color>(m, c));
        }

        private void Refresh(bool force)
        {
            int a = IsActiveAt(Atmosphere.CurrentTime) ? 1 : 0;
            if (!force && a == _active) return;
            _active = a;
            for (int i = 0; i < _lights.Count; i++) if (_lights[i] != null) _lights[i].enabled = a == 1;
            // Paylaşımlı malzeme emissive'i (MaterialPropertyBlock yok: GRD uyumlu).
            for (int i = 0; i < _heads.Count; i++)
                if (_heads[i].Key != null) _heads[i].Key.SetColor("_EmissionColor", a == 1 ? _heads[i].Value * 3f : Color.black);
        }

        private void Update()
        {
            Refresh(false);
            if (_active == 0) return;
            float t = Time.time;
            for (int i = 0; i < _flick.Count; i++)
            {
                var f = _flick[i];
                if (f.L != null) f.L.intensity = f.Base * PoiLightPlanner.FlickerFactor(t, f.Phase, f.Amp);
            }
        }

        public static void BuildProbes(Transform parent, IList<ProbeSpec> specs, Vector3 viewer)
        {
            var list = ReflectionProbePlanner.ApplyBudget(specs, viewer, QualitySettings.GetQualityLevel());
            foreach (var s in list)
            {
                var go = new GameObject(s.Interior ? "ProbeInterior" : "ProbePoi");
                go.transform.SetParent(parent, false);
                go.transform.position = s.Position;
                var p = go.AddComponent<ReflectionProbe>();
                p.size = s.Size;
                p.importance = s.Importance;
                p.mode = ReflectionProbeMode.Realtime;
                p.refreshMode = ReflectionProbeRefreshMode.OnAwake;
                p.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                p.resolution = s.Interior ? 64 : 128;
            }
        }
    }
}
