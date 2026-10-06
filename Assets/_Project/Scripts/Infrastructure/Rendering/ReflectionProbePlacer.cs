using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// S3-yansima: Yansıma probu yerleştirici. Her konum için bir dış prob, her bina için (bütçe içinde) kutu izdüşümlü
    /// iç prob. Probalar çalışma zamanında Realtime + ViaScripting modunda yaratılır: Düşük/Orta kademede her prob yalnız BİR kez
    /// çizilir ("pişmiş" gibi), Yüksek/Ultra'da zaman dilimi (gündüz/akşam/gece) değişince isteğe bağlı yeniden çizilir.
    /// Kamera çevresinde yalnız MaxActive prob etkin, aynı anda tek prob çizilir, zaman dilimli (yüzler kareye yayılır).
    /// Hata → uyarı + sessiz geçiş (sahne yansıma probsuz kalır, kırılmaz).
    /// </summary>
    public static class ReflectionProbePlacer
    {
        public const string RootName = "YansimaProblari";

        /// <summary>Dünya üretimi sonrası: WorldMetadata'dan (SceneBuilder kancası).</summary>
        public static ReflectionProbeManager PlaceFromWorld(MapLayout layout, WorldMetadata meta, int tier = -1, Transform parent = null)
        {
            if (layout == null || meta == null) return null;
            var terrain = meta.Terrain;
            System.Func<float, float, float> height = (x, z) => terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : layout.WaterLevel;
            return Place(layout, height, meta.StructureBounds, tier, parent);
        }

        /// <summary>Konumlara ve yapı sınırlarına göre probları kurar. tier &lt; 0 → QualitySettings. Başarısızsa null.</summary>
        public static ReflectionProbeManager Place(MapLayout layout, System.Func<float, float, float> heightAt, IList<Bounds> buildings,
            int tier = -1, Transform parent = null)
        {
            try
            {
                if (layout == null) return null;
                if (tier < 0) tier = ResolveTier();
                var sites = BuildSites(layout, heightAt, buildings);
                var reference = layout.Locations.Count > 0
                    ? new Vector3(layout.Locations[0].Center.x, 0f, layout.Locations[0].Center.y) : Vector3.zero;
                var chosen = ReflectionProbePlanner.Select(sites, tier, reference);
                if (chosen.Count == 0) return null;

                var existing = GameObject.Find(RootName);
                if (existing != null) Object.Destroy(existing);
                var root = new GameObject(RootName);
                if (parent != null) root.transform.SetParent(parent, false);
                var mgr = root.AddComponent<ReflectionProbeManager>();
                mgr.Init(chosen, tier);
                return mgr;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HAREKÂT] ReflectionProbePlacer atlandı: " + e.Message);
                return null;
            }
        }

        /// <summary>Aday listesini üretir (konum başına dış + bina içi).</summary>
        public static List<ProbeSite> BuildSites(MapLayout layout, System.Func<float, float, float> heightAt, IList<Bounds> buildings)
        {
            var sites = new List<ProbeSite>();
            var centers = new List<Vector2>();
            var radii = new List<float>();
            for (var i = 0; i < layout.Locations.Count; i++)
            {
                var loc = layout.Locations[i];
                var y = loc.TargetHeight >= 0f ? loc.TargetHeight : (heightAt != null ? heightAt(loc.Center.x, loc.Center.y) : layout.WaterLevel);
                sites.Add(ReflectionProbePlanner.ExteriorSite(string.IsNullOrEmpty(loc.Name) ? "Konum_" + i : loc.Name, i,
                    new Vector3(loc.Center.x, y, loc.Center.y), loc.Radius));
                centers.Add(loc.Center);
                radii.Add(loc.Radius);
            }

            if (buildings != null)
            {
                for (var i = 0; i < buildings.Count; i++)
                {
                    var b = buildings[i];
                    var li = ReflectionProbePlanner.NearestLocation(new Vector2(b.center.x, b.center.z), centers, radii);
                    if (li < 0) continue;
                    if (ReflectionProbePlanner.TryInteriorSite("Ic_" + i, li, b, out var s)) sites.Add(s);
                }
            }

            return sites;
        }

        private static int ResolveTier()
        {
            try { return PlanarReflectionMath.TierFromQuality(QualitySettings.GetQualityLevel(), QualitySettings.names.Length); }
            catch { return 2; }
        }
    }

    /// <summary>S3-yansima: Yerleştirilmiş probların çalışma zamanı yöneticisi (etkin küme + çizim bütçesi).</summary>
    public sealed class ReflectionProbeManager : MonoBehaviour
    {
        private sealed class Entry
        {
            public GameObject Go;
            public ReflectionProbe Probe;
            public ProbeSite Site;
            public bool Rendered;
            public int RenderId = -1;
            public TimeOfDay RenderedTime;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Vector3> _centers = new List<Vector3>();
        private readonly List<float> _extents = new List<float>();
        private ProbeBudget _budget;
        private int _tier;
        private float _nextTick;
        private float _nextRender;
        private Entry _inFlight;

        public int ProbeCount => _entries.Count;
        public int Tier => _tier;

        internal void Init(List<ProbeSite> sites, int tier)
        {
            _tier = tier;
            _budget = ReflectionProbePlanner.GetBudget(tier);
            for (var i = 0; i < sites.Count; i++)
            {
                var s = sites[i];
                var go = new GameObject("Prob_" + s.Name);
                go.transform.SetParent(transform, false);
                go.transform.position = s.Center;
                var p = go.AddComponent<ReflectionProbe>();
                p.mode = ReflectionProbeMode.Realtime;
                p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                p.timeSlicingMode = _budget.TimeSliced ? ReflectionProbeTimeSlicingMode.IndividualFaces : ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                p.resolution = _budget.Resolution;
                p.hdr = true;
                p.size = s.Size;
                p.center = Vector3.zero;
                var interior = s.Kind == ProbeSiteKind.Interior;
                p.boxProjection = true;
                p.importance = interior ? 10 : 1;
                p.blendDistance = interior ? 1.5f : 8f;
                p.nearClipPlane = 0.3f;
                p.farClipPlane = interior ? Mathf.Max(s.Size.x, s.Size.z) + 10f : 200f;
                // Küçük eşya, mermi, karakter ve viewmodel yansımaya girmez.
                p.cullingMask = PlanarReflectionMath.BuildCullingMask(GameLayers.Default);
                p.clearFlags = ReflectionProbeClearFlags.Skybox;
                go.SetActive(false);
                _entries.Add(new Entry { Go = go, Probe = p, Site = s });
                _centers.Add(s.Center);
                _extents.Add(Mathf.Max(s.Size.x, s.Size.z) * 0.5f);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 0.5f;

            var cam = Camera.main;
            if (cam == null) return;
            var active = ReflectionProbePlanner.ActiveSet(_centers, _extents, cam.transform.position, _budget);
            var set = new HashSet<int>(active);
            for (var i = 0; i < _entries.Count; i++)
            {
                var want = set.Contains(i);
                if (_entries[i].Go.activeSelf != want)
                {
                    _entries[i].Go.SetActive(want);
                    if (want && !_budget.BakeOnce) _entries[i].Rendered = false;
                }
            }

            PumpRender(active);
        }

        private void PumpRender(List<int> active)
        {
            if (_inFlight != null)
            {
                if (_inFlight.Probe != null && !_inFlight.Probe.IsFinishedRendering(_inFlight.RenderId)) return;
                _inFlight = null;
            }

            if (Time.unscaledTime < _nextRender) return;
            var now = Atmosphere.CurrentTime;
            for (var k = 0; k < active.Count; k++)
            {
                var e = _entries[active[k]];
                if (!ReflectionProbePlanner.NeedsRender(e.Rendered, e.Rendered && e.RenderedTime != now, _budget)) continue;
                e.RenderId = e.Probe.RenderProbe();
                e.Rendered = true;
                e.RenderedTime = now;
                _inFlight = e;
                _nextRender = Time.unscaledTime + _budget.RenderIntervalSeconds;
                return;
            }
        }
    }
}
