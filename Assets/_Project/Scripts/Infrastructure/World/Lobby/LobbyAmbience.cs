using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World.Lobby
{
    /// <summary>
    /// Lobi arka planı atmosferi (mavi saat askeri kamp): kıvılcım (ateş ışığı MenuCampfire'da), direk lambaları (ince titreşim),
    /// yer sisi şeridi ve komutan üzerinde kırmızı vurgu kontur ışığı. Kendi kendine yeten bileşen; tüm nesneler çocuk olarak
    /// kurulur ve bileşenle birlikte yok olur. Kalite kademesi <see cref="Tier"/> ile verilir (0 Düşük..3 Ultra).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyAmbience : MonoBehaviour
    {
        private Light _rim;
        private Light[] _lamps = new Light[0];
        private float[] _lampBase = new float[0];
        private float _rimBase;
        private float _seed;
        private int _tier;

        /// <summary>Kırmızı vurgu ışığı rengi (ContentOverrides ile değiştirilebilmesi için dışarıdan atanabilir).</summary>
        public static Color RimColor = new Color(0.95f, 0.16f, 0.12f);

        public int Tier => _tier;
        public int LampCountBuilt => _lamps.Length;

        public static LobbyAmbience Create(Transform parent, Vector3 firePos, Vector3 commanderPos, int tier)
        {
            if (parent == null || UnityEngine.Application.isBatchMode)
                return null;

            var go = new GameObject("LobiAtmosfer");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<LobbyAmbience>();
            a._tier = Mathf.Clamp(tier, 0, 3);
            a._seed = 31.7f;
            try
            {
                a.BuildFire(firePos);
                a.BuildRim(commanderPos, firePos);
                a.BuildLamps(firePos);
                a.BuildGroundFog(firePos);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Lobby] Atmosfer kurulamadı: " + e.Message);
            }

            return a;
        }

        private void BuildFire(Vector3 firePos)
        {
            // Ateş ışığı MenuCampfire'a aittir (çift ışık önlenir); burada yalnızca kıvılcım parçacıkları kurulur.
            // Kıvılcımlar: küçük, sıcak, yukarı süzülür.
            var mat = Project.Infrastructure.Rendering.MaterialLibrary.Unlit(new Color(2.2f, 1.1f, 0.4f));
            var ps = CreateSystem("Kıvılcım", firePos + new Vector3(0f, 0.3f, 0f), mat, LobbyCinematicMath.EmberParticles(_tier));
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.gravityModifier = -0.05f;
            var em = ps.emission;
            em.rateOverTime = LobbyCinematicMath.EmberParticles(_tier) / 3f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 18f;
            sh.radius = 0.25f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private void BuildRim(Vector3 commanderPos, Vector3 firePos)
        {
            // Komutanın arkasından, ateşin karşı yanından kırmızı kontur: omuz/silüet ayrışır.
            var go = new GameObject("KırmızıKontur");
            go.transform.SetParent(transform, false);
            var away = (commanderPos - firePos);
            away.y = 0f;
            away = away.sqrMagnitude < 0.01f ? Vector3.back : away.normalized;
            go.transform.localPosition = commanderPos + away * 1.6f + new Vector3(0.9f, 1.9f, 0.6f);
            go.transform.LookAt(transform.TransformPoint(commanderPos + Vector3.up * 1.2f));
            _rim = go.AddComponent<Light>();
            _rim.type = LightType.Spot;
            _rim.spotAngle = 55f;
            _rim.range = 6f;
            _rimBase = 9f;
            _rim.intensity = _rimBase;
            _rim.color = RimColor;
            _rim.shadows = LightShadows.None;
        }

        private void BuildLamps(Vector3 firePos)
        {
            var count = LobbyCinematicMath.LampCount(_tier);
            _lamps = new Light[count];
            _lampBase = new float[count];
            var poleMat = Project.Infrastructure.Rendering.MaterialLibrary.Unlit(new Color(0.07f, 0.08f, 0.09f));
            var bulbMat = Project.Infrastructure.Rendering.MaterialLibrary.Unlit(new Color(1.7f, 1.45f, 1.05f));
            for (var i = 0; i < count; i++)
            {
                // Kampı yay şeklinde çeviren direkler (arka yarım daire).
                var ang = Mathf.Lerp(-70f, 70f, count == 1 ? 0.5f : i / (float)(count - 1)) * Mathf.Deg2Rad;
                var p = firePos + new Vector3(Mathf.Sin(ang) * 11f, 0f, 7f + Mathf.Cos(ang) * 6f);
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "LobiDirek" + i;
                Destroy(pole.GetComponent<Collider>());
                pole.transform.SetParent(transform, false);
                pole.transform.localPosition = p + new Vector3(0f, 2.4f, 0f);
                pole.transform.localScale = new Vector3(0.1f, 2.4f, 0.1f);
                SetMat(pole, poleMat, false);

                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "LobiLamba" + i;
                Destroy(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(transform, false);
                bulb.transform.localPosition = p + new Vector3(0f, 4.9f, 0f);
                bulb.transform.localScale = Vector3.one * 0.3f;
                SetMat(bulb, bulbMat, false);

                var lg = new GameObject("LobiLambaIşığı" + i);
                lg.transform.SetParent(transform, false);
                lg.transform.localPosition = p + new Vector3(0f, 4.8f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.82f, 0.55f);
                l.range = 10f;
                _lampBase[i] = 3.2f;
                l.intensity = _lampBase[i];
                l.shadows = LobbyCinematicMath.LampShadows(_tier) ? LightShadows.Soft : LightShadows.None;
                _lamps[i] = l;
            }
        }

        private void BuildGroundFog(Vector3 firePos)
        {
            var mat = Project.Infrastructure.Rendering.MaterialLibrary.Transparent(new Color(0.42f, 0.52f, 0.68f, 0.07f), true);
            var ps = CreateSystem("YerSisi", firePos + new Vector3(0f, 0.35f, 6f), mat, LobbyCinematicMath.FogParticles(_tier));
            if (ps == null) return;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.5f, 0.66f, 0.05f), new Color(0.5f, 0.58f, 0.72f, 0.1f));
            var em = ps.emission;
            em.rateOverTime = LobbyCinematicMath.FogParticles(_tier) / 10f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(26f, 0.4f, 14f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        }

        private ParticleSystem CreateSystem(string name, Vector3 localPos, Material mat, int max)
        {
            if (mat == null) return null;
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, max);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(true);
            ps.Play();
            return ps;
        }

        private static void SetMat(GameObject go, Material m, bool shadows)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = shadows;
        }

        private void Update()
        {
            var t = Time.unscaledTime;
            if (_rim != null)
                _rim.intensity = _rimBase * (0.9f + 0.1f * LobbyCinematicMath.FireFlicker(t, _seed + 2f));
            for (var i = 0; i < _lamps.Length; i++)
                if (_lamps[i] != null)
                    _lamps[i].intensity = _lampBase[i] * LobbyCinematicMath.LampFlicker(t, _seed + i * 5.1f);
        }
    }
}
