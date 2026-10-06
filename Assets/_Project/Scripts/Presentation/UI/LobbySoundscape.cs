using System;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;
using Project.Infrastructure.Audio;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Saf kurallar: sahne kapısı, rastgele telsiz aralığı, ses düzeyi hesabı (test edilebilir, Unity nesnesi yok).
    /// </summary>
    public static class LobbySoundscapeRules
    {
        public const float RadioMinSeconds = 20f;
        public const float RadioMaxSeconds = 40f;

        /// <summary>Yalnız ana menü sahnesinde çalar.</summary>
        public static bool IsMenuScene(string sceneName) => sceneName == SceneNames.MainMenu;

        /// <summary>Sonraki telsiz cızırtısına kadar süre; <paramref name="unit01"/> 0..1 rastgele değer.</summary>
        public static float NextRadioDelay(float unit01) =>
            Mathf.Lerp(RadioMinSeconds, RadioMaxSeconds, Mathf.Clamp01(unit01));

        /// <summary>Ayar düzeyini 0..1'e kısıtlar; NaN/sonsuz değerde varsayılanı verir.</summary>
        public static float Level(float setting, float fallback = 1f)
        {
            if (float.IsNaN(setting) || float.IsInfinity(setting))
                return Mathf.Clamp01(fallback);
            return Mathf.Clamp01(setting);
        }

        /// <summary>Taban ses x ayar düzeyi (ana ses AudioListener üzerinden ayrıca uygulanır).</summary>
        public static float Scale(float baseVolume, float setting) => Mathf.Clamp01(baseVolume) * Level(setting);
    }

    /// <summary>
    /// Lobi ses manzarası (yalnız ana menü sahnesi): ateşin yanında konumlu çıtırtı, soğuk dağ rüzgârı yatağı,
    /// 20-40 sn'de bir tim yönünden boğuk telsiz cızırtısı + kısa konuşma, OYNA'da derin gümleme + uzak helikopter hazırlığı.
    /// Tüm sesler prosedürel üretilir (yalnız UnityEngine + ayarlar); sahne değişince kendiliğinden yok olur.
    /// Not: DialogueDirector.Say bir Combatant ister, menüde savaşçı yoktur; bu yüzden boğuk konuşma burada sentezlenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbySoundscape : MonoBehaviour
    {
        private const int Rate = 22050;
        private const float FireVolume = 0.55f;
        private const float WindVolume = 0.22f;
        private const float RadioVolume = 0.35f;
        private const float PlayVolume = 0.8f;

        private static LobbySoundscape _instance;
        private static bool _hooked;

        private AudioSource _fire;
        private AudioSource _wind;
        private AudioSource _radio;
        private AudioSource _play;
        private AudioClip _fireClip, _windClip, _thudClip, _heliClip;
        private AudioClip[] _radioClips;
        private float _nextRadio;
        private uint _rng = 0x9E3779B9u;
        private Transform _squad;
        private MenuCampfire _campfire;
        private bool _playPressed;

        /// <summary>OYNA'ya basıldığında çağrılır (LobbyFlow/menü bağlar). Menüde değilse yok sayılır.</summary>
        public static void NotifyPlayPressed()
        {
            if (_instance != null)
                _instance.PlayPress();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!_hooked)
            {
                _hooked = true;
                SceneManager.sceneLoaded += (scene, _) => EnsureFor(scene.name);
            }
            EnsureFor(SceneManager.GetActiveScene().name);
        }

        private static void EnsureFor(string sceneName)
        {
            try
            {
                if (!LobbySoundscapeRules.IsMenuScene(sceneName) || _instance != null || !UnityEngine.Application.isPlaying)
                    return;
                if (!GameAudio.Enabled)
                    return;
                var go = new GameObject("LobiSesManzarası");
                _instance = go.AddComponent<LobbySoundscape>();
            }
            catch (Exception e) { Debug.LogWarning("[LobbySoundscape] " + e.Message); }
        }

        private void Start()
        {
            try
            {
                _rng ^= (uint)(Time.realtimeSinceStartup * 1000f) | 1u;
                _campfire = FindAnyObjectByType<MenuCampfire>();
                var firePos = _campfire != null ? _campfire.transform.position : transform.position;

                _fire = MakeSource("Ates", firePos, true, true, 1.5f, 28f);
                _fire.clip = _fireClip = BuildCrackle();
                _wind = MakeSource("Ruzgar", transform.position, false, true, 1f, 1f);
                _wind.clip = _windClip = BuildWind();
                _radio = MakeSource("Telsiz", SquadPosition(firePos), true, false, 2f, 30f);
                _play = MakeSource("OynaTek", firePos, false, false, 1f, 1f);
                _radioClips = new[] { BuildRadio(0), BuildRadio(1), BuildRadio(2) };
                _thudClip = BuildThud();
                _heliClip = BuildHeliSpool();

                _fire.Play();
                _wind.Play();
                _nextRadio = Time.unscaledTime + LobbySoundscapeRules.NextRadioDelay(Rand());
                ApplyVolumes();
            }
            catch (Exception e) { Debug.LogWarning("[LobbySoundscape] kurulum: " + e.Message); }
        }

        private void Update()
        {
            if (!LobbySoundscapeRules.IsMenuScene(SceneManager.GetActiveScene().name))
            {
                Destroy(gameObject);
                return;
            }

            ApplyVolumes();

            if (!_playPressed && _radio != null && _radioClips != null && Time.unscaledTime >= _nextRadio)
            {
                _nextRadio = Time.unscaledTime + LobbySoundscapeRules.NextRadioDelay(Rand());
                if (_campfire == null)
                    _campfire = FindAnyObjectByType<MenuCampfire>();
                if (_campfire != null)
                    _radio.transform.position = SquadPosition(_campfire.transform.position);
                var clip = _radioClips[(int)(Rand() * _radioClips.Length) % _radioClips.Length];
                _radio.pitch = 0.96f + Rand() * 0.1f;
                _radio.PlayOneShot(clip, LobbySoundscapeRules.Scale(RadioVolume, Sfx()));
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            DestroyClip(_fireClip); DestroyClip(_windClip); DestroyClip(_thudClip); DestroyClip(_heliClip);
            if (_radioClips != null)
                for (var i = 0; i < _radioClips.Length; i++)
                    DestroyClip(_radioClips[i]);
        }

        private static void DestroyClip(AudioClip c)
        {
            if (c != null)
                Destroy(c);
        }

        // ---- ses düzeyleri ----

        private static float Sfx()
        {
            var s = GameSession.Settings;
            return s != null ? LobbySoundscapeRules.Level(s.Current.SfxVolume) : 1f;
        }

        private void ApplyVolumes()
        {
            // Ortam yatakları: ortam ayarı x SFX; ana ses AudioListener.volume ile zaten uygulanır.
            var amb = LobbySoundscapeRules.Level(GameAudio.AmbientVolume);
            var sfx = Sfx();
            if (_fire != null) _fire.volume = LobbySoundscapeRules.Scale(FireVolume, amb * sfx);
            if (_wind != null) _wind.volume = LobbySoundscapeRules.Scale(WindVolume, amb * sfx);
        }

        // ---- OYNA ----

        private void PlayPress()
        {
            if (_playPressed || _play == null)
                return;
            _playPressed = true;
            var v = LobbySoundscapeRules.Scale(PlayVolume, Sfx());
            _play.PlayOneShot(_thudClip, v);
            _play.PlayOneShot(_heliClip, v * 0.6f);
        }

        // ---- kurulum yardımcıları ----

        private AudioSource MakeSource(string name, Vector3 pos, bool spatial, bool loop, float minD, float maxD)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = minD;
            s.maxDistance = maxD;
            s.dopplerLevel = 0f;
            s.volume = 0f;
            return s;
        }

        private Vector3 SquadPosition(Vector3 firePos)
        {
            if (_squad == null)
            {
                var go = GameObject.Find("Tim_Sohbet1");
                if (go != null)
                    _squad = go.transform;
            }
            // Tim ateşin arkasında (kameradan uzakta); bulunamazsa ateşe göre arka-yan ofset.
            return _squad != null ? _squad.position + Vector3.up * 1.5f : firePos + new Vector3(-1.5f, 1.5f, 5f);
        }

        private float Rand()
        {
            _rng = _rng * 1664525u + 1013904223u;
            return (_rng >> 8) * (1f / 16777216f);
        }

        private float Bip() => Rand() * 2f - 1f;

        // ---- sentez ----

        private AudioClip Make(string name, float[] data, int channels = 1)
        {
            var clip = AudioClip.Create(name, data.Length / channels, channels, Rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        /// <summary>4 sn dikişsiz: kısık odun uğultusu + seyrek tıkırtı/çıtırtılar.</summary>
        private AudioClip BuildCrackle()
        {
            const int n = Rate * 4;
            const int x = Rate / 4; // çapraz geçiş
            var raw = new float[n + x];
            float lp = 0f, lp2 = 0f;
            for (var i = 0; i < raw.Length; i++)
            {
                lp += 0.02f * (Bip() - lp);
                lp2 += 0.12f * (Bip() - lp2);
                raw[i] = lp * 2.2f + (lp2 - lp) * 0.15f;
            }
            var pops = 26;
            for (var p = 0; p < pops; p++)
            {
                var at = (int)(Rand() * (raw.Length - 2000));
                var amp = 0.15f + Rand() * Rand() * 0.9f;
                var tau = 40f + Rand() * 220f;
                float f = 0f;
                for (var k = 0; k < 1500 && at + k < raw.Length; k++)
                {
                    var env = Mathf.Exp(-k / tau);
                    f += 0.5f * (Bip() - f);
                    raw[at + k] += (Bip() - f * 0.6f) * env * amp;
                }
            }
            var data = new float[n];
            for (var i = 0; i < n; i++)
                data[i] = raw[i];
            for (var i = 0; i < x; i++)
            {
                var w = i / (float)x;
                data[i] = data[i] * Mathf.Sqrt(w) + raw[n + i] * Mathf.Sqrt(1f - w);
            }
            Normalize(data, 0.7f);
            return Make("lobi_atesi", data);
        }

        /// <summary>8 sn dikişsiz stereo: bant geçiren gürültü, yavaş esintiler, L/R ilintisiz (geniş).</summary>
        private AudioClip BuildWind()
        {
            const int n = Rate * 8;
            var data = new float[n * 2];
            for (var ch = 0; ch < 2; ch++)
            {
                float a = 0f, b = 0f, c = 0f;
                var ph1 = ch * 1.7f; var ph2 = ch * 0.9f + 2f;
                for (var i = 0; i < n; i++)
                {
                    var t = i / (float)n;
                    var gust = 0.55f + 0.3f * Mathf.Sin((t * 3f + ph1) * Mathf.PI * 2f)
                                      + 0.15f * Mathf.Sin((t * 7f + ph2) * Mathf.PI * 2f);
                    var cut = 0.02f + 0.03f * gust;
                    a += cut * (Bip() - a);
                    b += cut * (a - b);
                    c += 0.004f * (b - c);
                    data[i * 2 + ch] = (b - c) * gust * 4f;
                }
            }
            // Dikiş: sona doğru başa yumuşak geçiş (modülasyonlar tam periyotlu).
            const int fade = Rate / 5;
            for (var i = 0; i < fade; i++)
            {
                var w = i / (float)fade;
                for (var ch = 0; ch < 2; ch++)
                {
                    data[i * 2 + ch] *= w;
                    data[(n - 1 - i) * 2 + ch] *= w;
                }
            }
            Normalize(data, 0.6f);
            return Make("lobi_ruzgar", data, 2);
        }

        /// <summary>Boğuk telsiz: cızırtı + kısa heceli konuşma benzeri (formant), bant sınırlı ve hafif kırpılmış.</summary>
        private AudioClip BuildRadio(int variant)
        {
            var seed = _rng + (uint)variant * 7919u;
            var saved = _rng;
            _rng = seed;
            var syl = 5 + variant * 2;
            var len = 0.12f + 0.16f * syl + 0.2f;
            var n = (int)(len * Rate);
            var data = new float[n];

            // Açılış cızırtısı
            float hp = 0f;
            var sq = (int)(0.11f * Rate);
            for (var i = 0; i < sq; i++)
            {
                hp += 0.4f * (Bip() - hp);
                data[i] = (Bip() - hp) * Mathf.Exp(-i / (0.04f * Rate)) * 0.6f;
            }

            var pos = sq + (int)(0.03f * Rate);
            double phase = 0;
            float y1 = 0f, y2 = 0f;
            for (var s = 0; s < syl && pos < n; s++)
            {
                var sylLen = (int)((0.08f + Rand() * 0.1f) * Rate);
                var f0 = 95f + Rand() * 45f;
                var f1 = 350f + Rand() * 450f;
                var f2 = 900f + Rand() * 1100f;
                for (var k = 0; k < sylLen && pos + k < n; k++)
                {
                    var env = Mathf.Sin(Mathf.PI * k / sylLen);
                    phase += f0 / Rate;
                    phase -= Math.Floor(phase);
                    var glottal = (float)(phase * 2.0 - 1.0) + Bip() * 0.15f;
                    // iki kutuplu rezonans (formant): y = g*x + 2r*cos(w)*y1 - r^2*y2
                    var a1 = 2f * 0.93f * Mathf.Cos(2f * Mathf.PI * f1 / Rate);
                    var yn = glottal * 0.06f + a1 * y1 - 0.8649f * y2;
                    y2 = y1;
                    y1 = yn;
                    var formant2 = Mathf.Sin(2f * Mathf.PI * f2 * k / Rate) * glottal * 0.25f;
                    data[pos + k] += (y1 * 3f + formant2) * env;
                }
                pos += sylLen + (int)((0.02f + Rand() * 0.07f) * Rate);
            }

            // Boğuk: alçak geçiren + telefon bandı + hafif kırpma
            float lp = 0f, lp2 = 0f, dc = 0f;
            for (var i = 0; i < n; i++)
            {
                lp += 0.28f * (data[i] - lp);
                lp2 += 0.35f * (lp - lp2);
                dc += 0.01f * (lp2 - dc);
                var v = (lp2 - dc) * 2.2f + Bip() * 0.015f;
                data[i] = Mathf.Clamp(v, -0.5f, 0.5f);
            }
            Normalize(data, 0.8f);
            // kuyruk: kapanış tıkı
            var tail = Mathf.Min(n, (int)(0.04f * Rate));
            for (var i = 0; i < tail; i++)
                data[n - 1 - i] *= i / (float)tail;
            _rng = saved ^ seed;
            return Make("lobi_telsiz_" + variant, data);
        }

        /// <summary>Derin gümleme: 55->32 Hz sinüs düşüşü + yumuşak gürültü darbesi.</summary>
        private AudioClip BuildThud()
        {
            var n = (int)(0.9f * Rate);
            var data = new float[n];
            double ph = 0;
            float lp = 0f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var f = 32f + 28f * Mathf.Exp(-t * 9f);
                ph += f / Rate;
                var env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 4.6f);
                lp += 0.05f * (Bip() - lp);
                data[i] = ((float)Math.Sin(ph * Math.PI * 2.0) * 0.9f + lp * 1.5f * Mathf.Exp(-t * 18f)) * env;
            }
            Normalize(data, 0.9f);
            return Make("lobi_gumleme", data);
        }

        /// <summary>Uzak helikopter hazırlığı (3,6 sn): hızlanan palet vuruşu + yükselen türbin uğultusu, yavaşça belirir.</summary>
        private AudioClip BuildHeliSpool()
        {
            var len = 3.6f;
            var n = (int)(len * Rate);
            var data = new float[n];
            double rotorPh = 0, whinePh = 0;
            float lp = 0f, lp2 = 0f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var u = t / len;
                var rate = 3f + 17f * u * u; // palet vuruş Hz
                rotorPh += rate / Rate;
                var chop = Mathf.Pow(Mathf.Max(0f, (float)Math.Sin(rotorPh * Math.PI * 2.0)), 3f);
                lp += 0.06f * (Bip() - lp);
                lp2 += 0.1f * (lp - lp2);
                whinePh += (180f + 900f * u * u) / Rate;
                var whine = (float)Math.Sin(whinePh * Math.PI * 2.0) * 0.08f * u;
                var env = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 1.2f)) * Mathf.Min(1f, (len - t) / 0.6f);
                data[i] = (lp2 * 6f * (0.3f + chop) + whine) * env * (0.3f + 0.7f * u);
            }
            Normalize(data, 0.7f);
            return Make("lobi_heli", data);
        }

        private static void Normalize(float[] d, float peak)
        {
            var m = 1e-6f;
            for (var i = 0; i < d.Length; i++)
            {
                var a = Mathf.Abs(d[i]);
                if (a > m) m = a;
            }
            var g = peak / m;
            for (var i = 0; i < d.Length; i++)
                d[i] *= g;
        }
    }
}
