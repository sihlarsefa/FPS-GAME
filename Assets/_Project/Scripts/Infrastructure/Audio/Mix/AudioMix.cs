using System;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using UnityEngine;
using UnityEngine.Audio;

namespace Project.Infrastructure.Audio.HdrMix
{
    /// <summary>
    /// Çalışma zamanı miks katmanı: HDR pencere + miks anlık görüntüleri (çınlama, su altı, iç mekân, ADS odağı,
    /// yaralı/ölü boğukluğu). AudioMixer varlığı gerektirmez: Resources/Audio/MainMixer varsa (ve açık parametreleri
    /// "WorldLowpass", "MasterGainDb", "AmbienceGainDb", "ReverbSendDb" tanımlıysa) onu sürer; yoksa dinleyiciye
    /// AudioLowPassFilter ekler ve AudioListener.volume'u kullanır. Başsız süreçte hiçbir nesne oluşturmaz.
    /// </summary>
    public static class AudioMix
    {
        public const string MixerResource = "Audio/MainMixer";
        public const string LowpassParam = "WorldLowpass";
        public const string MasterParam = "MasterGainDb";
        public const string AmbienceParam = "AmbienceGainDb";
        public const string ReverbParam = "ReverbSendDb";

        public static readonly HdrWindow Hdr = new HdrWindow();
        public static readonly MixSnapshotState State = new MixSnapshotState();

        private static AudioMixDirector _director;
        private static IEventBus _bus;
        private static PlayerId _local = PlayerId.Invalid;

        /// <summary>Ortam yatağı yöneticisinin ayrıştırılmış ortam kazancı (0..1) — AmbienceDirector okur.</summary>
        public static float AmbienceGain => State.Current.AmbienceGain;

        public static bool IsActive => _director != null;

        /// <summary>Yöneticiyi oluşturur (başsızda/editör dışı oynatmada yok sayılır). Güvenle tekrar çağrılabilir.</summary>
        public static void Ensure()
        {
            if (_director != null || !UnityEngine.Application.isPlaying || !GameAudio.Enabled)
                return;
            var go = new GameObject("[AudioMix]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _director = go.AddComponent<AudioMixDirector>();
        }

        // ------------------------------------------------------------------ HDR

        /// <summary>HDR pencereye dB cinsinden doğrudan seviye bildirir.</summary>
        public static void ReportLoudDb(float levelDb) => Hdr.Report(levelDb);

        /// <summary>Konumlu yüksek sesli olay (atış, patlama); mesafeye göre seviye hesaplanır.</summary>
        public static void ReportLoudEvent(HdrEventKind kind, Vector3 position)
        {
            Hdr.Report(HdrAudioMath.EventLevelDb(kind, DistanceToListener(position)));
        }

        /// <summary>Kaynak seviyesi (dB) için HDR kazancı (doğrusal). GameAudio sessiz sesleri bununla çarpabilir.</summary>
        public static float HdrGain(float sourceDb) => Hdr.GainLinear(sourceDb);

        /// <summary>Ayak sesi vb. uzak/sessiz kaynak için hazır HDR kazancı (mesafe ve kaynak türünden).</summary>
        public static float HdrGainFor(HdrEventKind kind, Vector3 position) =>
            Hdr.GainLinear(HdrAudioMath.EventLevelDb(kind, DistanceToListener(position)));

        // ------------------------------------------------------------------ anlık görüntüler

        /// <summary>Patlama: HDR bildirir ve yeterince yakınsa kulak çınlaması başlatır. Çınlama süresini (sn) döndürür.</summary>
        public static float OnExplosion(Vector3 position, float radius, bool earProtected = false)
        {
            var d = DistanceToListener(position);
            Hdr.Report(HdrAudioMath.EventLevelDb(HdrEventKind.Explosion, d));
            var severity = MixSnapshotMath.TinnitusSeverity(d, radius, earProtected);
            if (State.Dead)
                return 0f;
            return State.TriggerTinnitus(severity);
        }

        public static void SetUnderwater(bool on) => State.Underwater = on;
        public static void SetIndoor(bool on) => State.Indoor = on;
        public static void SetAdsFocus(bool on) => State.Ads = on;
        public static void SetDowned(bool on) => State.Downed = on;
        public static void SetDead(bool on) => State.Dead = on;

        /// <summary>Yeni maç/sahne: tüm miks durumunu ve HDR'ı sıfırlar.</summary>
        public static void ResetAll()
        {
            State.ResetAll();
            Hdr.Reset();
        }

        // ------------------------------------------------------------------ EventBus

        /// <summary>
        /// EventBus'a bağlanır: patlama, ölüm/yaralı/diriltme ve atış olaylarından miksi sürer; uzak atışları/patlamaları
        /// uzak çatışma planlayıcısına (Ambience) iletir. Tekrar çağrılırsa öncekini bırakır.
        /// </summary>
        public static void Bind(IEventBus bus, PlayerId localPlayer)
        {
            Unbind();
            if (bus == null)
                return;
            _bus = bus;
            _local = localPlayer;
            bus.Subscribe<ExplosionEvent>(OnExplosionEvent);
            bus.Subscribe<WeaponFiredEvent>(OnWeaponFired);
            bus.Subscribe<PlayerDiedEvent>(OnDied);
            bus.Subscribe<DownedEvent>(OnDowned);
            bus.Subscribe<RevivedEvent>(OnRevived);
            bus.Subscribe<MatchPhaseChangedEvent>(OnPhase);
            Ensure();
        }

        public static void Unbind()
        {
            if (_bus == null)
                return;
            _bus.Unsubscribe<ExplosionEvent>(OnExplosionEvent);
            _bus.Unsubscribe<WeaponFiredEvent>(OnWeaponFired);
            _bus.Unsubscribe<PlayerDiedEvent>(OnDied);
            _bus.Unsubscribe<DownedEvent>(OnDowned);
            _bus.Unsubscribe<RevivedEvent>(OnRevived);
            _bus.Unsubscribe<MatchPhaseChangedEvent>(OnPhase);
            _bus = null;
        }

        private static Vector3 ToVec(Float3 f) => new Vector3(f.X, f.Y, f.Z);

        private static void OnExplosionEvent(ExplosionEvent e)
        {
            var pos = ToVec(e.Position);
            OnExplosion(pos, e.Radius);
            // Duyulur mesafedeki patlamayı Acoustics (gecikmeli) çalıyor; uzak çatışma planlayıcısı yalnızca ötesini üstlenir.
            if (!Acoustics.CoversExplosion(DistanceToListener(pos)))
                Ambience.AmbienceDirector.ReportRealEvent(Ambience.DistantKind.Blast, pos);
        }

        private static void OnWeaponFired(WeaponFiredEvent e)
        {
            if (_local.IsValid && e.ShooterId.Equals(_local))
            {
                // Kendi silahımız: HDR penceresini yükseltir (makineli tüfek en çok).
                Hdr.Report(HdrAudioMath.EventLevelDb(HdrAudioMath.KindForWeaponId(e.WeaponId), 1f));
                return;
            }

            if (!e.HasOrigin)
                return;
            var pos = ToVec(e.Origin);
            var kind = HdrAudioMath.KindForWeaponId(e.WeaponId);
            Hdr.Report(HdrAudioMath.EventLevelDb(kind == HdrEventKind.OwnMachineGun ? HdrEventKind.OwnRifle : kind, DistanceToListener(pos)));
            if (!Acoustics.CoversShot(DistanceToListener(pos)))
                Ambience.AmbienceDirector.ReportRealEvent(Ambience.DistantKind.Gunshot, pos);
        }

        private static void OnDied(PlayerDiedEvent e)
        {
            if (_local.IsValid && e.VictimId.Equals(_local))
                State.Dead = true;
        }

        private static void OnDowned(DownedEvent e)
        {
            if (_local.IsValid && e.VictimId.Equals(_local))
                State.Downed = true;
        }

        private static void OnRevived(RevivedEvent e)
        {
            if (_local.IsValid && e.VictimId.Equals(_local))
                State.Downed = false;
        }

        private static void OnPhase(MatchPhaseChangedEvent e) => ResetAll();

        // ------------------------------------------------------------------ yardımcılar

        internal static float DistanceToListener(Vector3 position)
        {
            return _director != null && _director.TryGetListenerPosition(out var l) ? Vector3.Distance(l, position) : 30f;
        }

        internal static bool TryListenerPosition(out Vector3 p)
        {
            if (_director != null)
                return _director.TryGetListenerPosition(out p);
            p = Vector3.zero;
            return false;
        }

        internal static void Clear()
        {
            _director = null;
        }
    }

    /// <summary>[AudioMix] nesnesi: HDR/anlık görüntü durumunu ilerletir ve sonucu sese uygular.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class AudioMixDirector : MonoBehaviour
    {
        private const float TinnitusBaseVolume = 0.22f;

        private AudioListener _listener;
        private AudioLowPassFilter _lowpass;
        private AudioSource _ring;
        private AudioMixer _mixer;
        private bool _mixerTried;
        private bool _mixerDrives;
        private float _nextSearch;
        private float _lastVolume = -1f;

        public bool TryGetListenerPosition(out Vector3 position)
        {
            if (EnsureListener())
            {
                position = _listener.transform.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        private bool EnsureListener()
        {
            if (_listener != null && _listener.isActiveAndEnabled)
                return true;
            if (Time.unscaledTime < _nextSearch)
                return false;
            _nextSearch = Time.unscaledTime + 0.5f;
            _listener = FindAnyObjectByType<AudioListener>();
            _lowpass = null;
            return _listener != null;
        }

        private void Awake()
        {
            var go = new GameObject("TinnitusRing");
            go.transform.SetParent(transform, false);
            _ring = go.AddComponent<AudioSource>();
            _ring.playOnAwake = false;
            _ring.loop = true;
            _ring.spatialBlend = 0f;
            _ring.bypassListenerEffects = true;
            _ring.bypassReverbZones = true;
            _ring.priority = 8;
            _ring.volume = 0f;
        }

        private void OnDestroy() => AudioMix.Clear();

        private void TryLoadMixer()
        {
            _mixerTried = true;
            try
            {
                _mixer = Resources.Load<AudioMixer>(AudioMix.MixerResource);
            }
            catch (Exception)
            {
                _mixer = null;
            }
        }

        private void EnsureRingClip()
        {
            if (_ring.clip != null)
                return;
            try
            {
                var samples = MixSynth.RenderTinnitusRing();
                var clip = AudioClip.Create("mix_tinnitus", samples.Length, 1, SynthSampleRate, false);
                clip.SetData(samples, 0);
                _ring.clip = clip;
            }
            catch (Exception)
            {
                // Sessizce yoksay: çınlama olmadan da miks çalışır.
            }
        }

        private const int SynthSampleRate = ProceduralAudioSynth.SampleRate;

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            AudioMix.Hdr.Tick(dt);
            var p = AudioMix.State.Tick(dt);

            if (!_mixerTried)
                TryLoadMixer();

            var master = p.MasterGain * AudioMix.Hdr.HeadroomLinear;
            ApplyLowpass(p.LowpassHz);
            ApplyMasterAndMixer(p, master);
            ApplyRing(p.TinnitusGain);
        }

        private void ApplyLowpass(float hz)
        {
            if (_mixer != null && _mixerDrives)
                return; // mixer filtreyi kendisi uyguluyor.

            if (!EnsureListener())
                return;
            if (_lowpass == null)
            {
                _lowpass = _listener.GetComponent<AudioLowPassFilter>();
                if (_lowpass == null)
                {
                    if (hz >= 21000f)
                        return;
                    _lowpass = _listener.gameObject.AddComponent<AudioLowPassFilter>();
                }
            }

            var open = hz >= 21000f;
            if (_lowpass.enabled == open)
                _lowpass.enabled = !open;
            if (!open)
                _lowpass.cutoffFrequency = Mathf.Clamp(hz, 150f, 22000f);
        }

        private void ApplyMasterAndMixer(MixParams p, float master)
        {
            if (_mixer != null)
            {
                // Her parametre için ayrı dene: tanımlı değilse SetFloat false döner → dinleyici yoluna düş.
                var lp = _mixer.SetFloat(AudioMix.LowpassParam, Mathf.Clamp(p.LowpassHz, 150f, 22000f));
                var m = _mixer.SetFloat(AudioMix.MasterParam, HdrAudioMath.LinearToDb(master));
                _mixer.SetFloat(AudioMix.AmbienceParam, HdrAudioMath.LinearToDb(p.AmbienceGain));
                _mixer.SetFloat(AudioMix.ReverbParam, Mathf.Lerp(-80f, -6f, p.ReverbSend));
                _mixerDrives = lp;
                if (m)
                    return;
            }

            var v = Mathf.Clamp01(GameAudio.MasterVolume * master);
            if (Mathf.Abs(v - _lastVolume) > 0.001f)
            {
                _lastVolume = v;
                AudioListener.volume = v;
            }
        }

        private void ApplyRing(float gain)
        {
            if (_ring == null)
                return;
            var v = Mathf.Clamp01(gain * TinnitusBaseVolume);
            if (v < 0.001f)
            {
                if (_ring.isPlaying)
                    _ring.Stop();
                _ring.volume = 0f;
                return;
            }

            EnsureRingClip();
            if (_ring.clip == null)
                return;
            _ring.volume = v;
            if (!_ring.isPlaying)
                _ring.Play();
        }
    }
}
