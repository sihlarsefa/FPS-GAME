using System;
using System.Threading;
using System.Threading.Tasks;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Oyun sesi giriş noktası (statik). <see cref="Initialize"/> tüm <see cref="SoundId"/> seslerini bir kez
    /// prosedürel olarak üretir (iş parçacıklarına bölünmüş, önbellekli) ve "[GameAudio]" adlı DontDestroyOnLoad
    /// nesnesinde ses havuzlarını kurar: 32 adet 3B kanal (logaritmik azalma, spatialBlend 1, doppler 0, mesafe
    /// süzgeci), 2B kanallar, döngü kanalları ve çapraz geçişli ortam sesi.
    /// <para>
    /// Sağlamlık: Initialize çağrılmadan kullanılırsa ilk çağrıda kendini kurar; AudioListener yoksa tek seferlik
    /// sesleri sessizce atlar (asla istisna fırlatmaz); döngüler ve ortam sesi dinleyici olmadan da başlatılabilir.
    /// Yalnızca ana iş parçacığından çağrılmalıdır.
    /// </para>
    /// </summary>
    public static class GameAudio
    {
        public const int SpatialVoiceCount = 32;
        public const int FlatVoiceCount = 16;
        public const int InitialLoopVoiceCount = 8;
        public const int MaxLoopVoiceCount = 64;

        /// <summary>Ses hızı (m/s) — uzak atış seslerinin gecikmesi için.</summary>
        public const float SpeedOfSound = 343f;

        private const float NearFilterDistance = 25f;
        private const float FarCutoffHz = 900f;
        private const float OpenCutoffHz = 22000f;
        private const float DedupeWindow = 0.03f;
        private const float DedupeDistanceSqr = 0.75f * 0.75f;
        private const int RecentCapacity = 16;

        private static float _masterVolume = 1f;
        private static float _ambientVolume = 1f;

        private static AudioClip[] _clips;
        private static AudioHost _host;
        private static bool _initializing;
        private static bool _warnedNoPlayMode;
        private static bool _quitting;

        private static readonly RecentPlay[] Recent = new RecentPlay[RecentCapacity];
        private static int _recentCursor;

        private struct RecentPlay
        {
            public SoundId Id;
            public Vector3 Position;
            public float Time;
        }

        /// <summary>Sesler üretilmiş ve havuz kurulmuşsa true.</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>Ana ses (0..1) — AudioListener.volume üzerinden tüm sese uygulanır.</summary>
        public static float MasterVolume
        {
            get => _masterVolume;
            set
            {
                _masterVolume = Sanitize01(value, _masterVolume);
                AudioListener.volume = _masterVolume;
            }
        }

        /// <summary>Ortam sesi (0..1) — ortam döngüsü, rüzgâr, uzak çatışma ve menü müziğine uygulanır.</summary>
        public static float AmbientVolume
        {
            get => _ambientVolume;
            set
            {
                var old = _ambientVolume;
                _ambientVolume = Sanitize01(value, _ambientVolume);
                if (!Mathf.Approximately(old, _ambientVolume))
                    ApplyAmbientVolumeToLoops(old, _ambientVolume);
            }
        }

        /// <summary>Şu an istenen ortam döngüsü (SoundId.None = yok).</summary>
        public static SoundId CurrentAmbience => HostAlive ? _host.CurrentAmbience : SoundId.None;

        /// <summary>Sahnede etkin bir AudioListener varsa true.</summary>
        public static bool HasListener => HostAlive && _host.HasListener;

        private static bool HostAlive => !ReferenceEquals(_host, null) && _host != null;

        // =====================================================================================
        //  KURULUM
        // =====================================================================================

        /// <summary>
        /// Eşgüçlü (idempotent): tüm sesleri bir kez üretir ve DontDestroyOnLoad havuzunu kurar. Oynatma modu
        /// dışında (editör) yalnızca klipleri üretir.
        /// </summary>
        public static void Initialize()
        {
            if (IsInitialized && HostAlive)
                return;

            if (_initializing)
                return;

            // Kapanış sırasında (OnDestroy'lardan gelen çağrılar) yeni nesne oluşturma.
            if (_quitting && UnityEngine.Application.isPlaying)
                return;

            _initializing = true;
            try
            {
                EnsureClips();
                if (!UnityEngine.Application.isPlaying)
                    return;

                AudioListener.volume = _masterVolume;

                if (!HostAlive)
                {
                    var existing = GameObject.Find(AudioHost.ObjectName);
                    if (existing != null)
                        UnityEngine.Object.Destroy(existing);

                    _host = AudioHost.Create(SpatialVoiceCount, FlatVoiceCount, InitialLoopVoiceCount, MaxLoopVoiceCount);
                }

                IsInitialized = _host != null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                IsInitialized = false;
            }
            finally
            {
                _initializing = false;
            }
        }

        /// <summary>Kimliğin klibi (gerekirse üretir). None/bilinmeyen için null.</summary>
        public static AudioClip GetClip(SoundId id)
        {
            var index = (int)id;
            if (id == SoundId.None || index < 0)
                return null;

            if (_clips == null || index >= _clips.Length)
                EnsureClips();

            if (_clips == null || index >= _clips.Length)
                return null;

            var clip = _clips[index];
            if (clip != null)
                return clip;

            // Klip yok edilmiş olabilir (ör. Resources.UnloadUnusedAssets / editör oturumu) — tek tek yeniden üret.
            try
            {
                clip = ProceduralAudioSynth.CreateClip(id);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                clip = null;
            }

            _clips[index] = clip;
            return clip;
        }

        // =====================================================================================
        //  ÇALMA
        // =====================================================================================

        /// <summary>3B tek seferlik ses. Menzil dışı veya dinleyici yoksa çalınmaz.</summary>
        public static void Play(SoundId id, Vector3 position, float volume = 1f, float pitch = 1f, float maxDistance = 80f)
        {
            if (!(volume > 0.0005f) || !EnsureReady())
                return;

            maxDistance = maxDistance > 1f ? maxDistance : 1f;
            var minDistance = Mathf.Clamp(maxDistance * 0.05f, 1f, 25f);
            PlaySpatial(id, position, volume, pitch, minDistance, maxDistance, false, true);
        }

        /// <summary>2B tek seferlik ses (arayüz, oyuncunun kendi sesleri).</summary>
        public static void Play2D(SoundId id, float volume = 1f, float pitch = 1f)
        {
            if (!(volume > 0.0005f) || !EnsureReady())
                return;

            // Dinleyici yoksa hiçbir ses duyulmaz; kanal harcama.
            if (!_host.HasListener)
                return;

            var clip = GetClip(id);
            if (clip == null)
                return;

            var now = Time.unscaledTime;
            var ui = IsUi(id);
            volume = Mathf.Clamp01(volume);
            // Arayüz sesleri, yoğun ateş sırasında bile düşürülmesin.
            var importance = ui ? 2f : volume;
            var voice = _host.Flat.AcquireOneShot(now, importance);
            if (voice == null)
                return;

            pitch = SanitizePitch(pitch);
            var src = voice.Source;
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.ignoreListenerPause = ui;
            src.priority = ui ? 16 : 64;
            src.Play();

            voice.Id = id;
            voice.Importance = importance;
            voice.StartTime = now;
            voice.EndTime = now + clip.length / pitch + 0.05f;
        }

        /// <summary>
        /// Silah sınıfına göre atış sesi. Yerel atıcı için 2B ve tam ses; diğerleri için 3B, sınıfa göre menzil,
        /// mesafeyle artan alçak geçiren süzgeç ve ses hızına göre gecikme.
        /// </summary>
        public static void PlayGunshot(WeaponDefinitionData weapon, Vector3 position, bool isLocalShooter)
        {
            if (weapon == null || !EnsureReady())
                return;

            var profile = GunshotProfile.For(weapon);
            if (profile.Id == SoundId.None)
                return;

            var pitch = profile.Pitch * UnityEngine.Random.Range(0.965f, 1.035f);
            if (isLocalShooter)
            {
                Play2D(profile.Id, profile.LocalVolume, pitch);
                return;
            }

            var volume = profile.Volume * UnityEngine.Random.Range(0.92f, 1f);
            PlaySpatial(profile.Id, position, volume, pitch, profile.MinDistance, profile.MaxDistance, true, false);
        }

        /// <summary>
        /// Döngü başlatır ve kaynağını döndürür (yoksa null). <paramref name="follow"/> verilirse her karede onu izler
        /// ve o nesne yok edilince döngü kendiliğinden kapanır. <see cref="StopLoop"/> ile durdurun.
        /// </summary>
        public static AudioSource StartLoop(SoundId id, Transform follow, float volume = 1f, bool spatial = true,
            float maxDistance = 200f)
        {
            if (!EnsureReady())
                return null;

            var clip = GetClip(id);
            if (clip == null)
                return null;

            var voice = _host.Loops.AcquireLoop();
            if (voice == null)
            {
                Debug.LogWarning("[GameAudio] Döngü kanalı kalmadı: " + id);
                return null;
            }

            var ambient = IsAmbientCategory(id);
            var baseVolume = Mathf.Clamp01(Sanitize01(volume, 1f));
            maxDistance = maxDistance > 1f ? maxDistance : 1f;

            var src = voice.Source;
            src.clip = clip;
            src.loop = true;
            src.pitch = 1f;
            src.volume = baseVolume * (ambient ? _ambientVolume : 1f);
            src.priority = spatial ? 96 : 48;
            src.spatialBlend = spatial ? 1f : 0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.dopplerLevel = 0f;
            src.minDistance = Mathf.Clamp(maxDistance * 0.05f, 1f, 30f);
            src.maxDistance = maxDistance;
            if (voice.LowPass != null && voice.LowPass.enabled)
                voice.LowPass.enabled = false;

            var hasFollow = !ReferenceEquals(follow, null) && follow != null;
            if (hasFollow)
                voice.Transform.position = follow.position;
            else
                voice.Transform.localPosition = Vector3.zero;

            // Aynı döngüyü çalan birden fazla araç senkron duyulmasın.
            if (spatial && id != SoundId.MenuMusic && clip.samples > 0)
                src.timeSamples = UnityEngine.Random.Range(0, clip.samples);

            voice.Id = id;
            voice.Spatial = spatial;
            voice.Ambient = ambient;
            voice.HasFollow = hasFollow;
            voice.Follow = hasFollow ? follow : null;
            voice.MaxDistance = maxDistance;
            voice.BaseVolume = baseVolume;
            voice.NextDistanceUpdate = 0f;
            voice.Scene = SceneManager.GetActiveScene();
            voice.StartFrame = Time.frameCount;
            voice.StartTime = Time.unscaledTime;

            if (spatial && _host.TryGetListenerPosition(out var listener))
            {
                var d = Vector3.Distance(listener, voice.Transform.position);
                src.mute = d > maxDistance;
                ApplyDistanceFilter(voice.LowPass, d, maxDistance);
            }

            src.Play();
            return src;
        }

        /// <summary>Döngüyü kısa bir kapanışla durdurur ve kanalı havuza geri verir. Null güvenlidir.</summary>
        public static void StopLoop(AudioSource source)
        {
            if (ReferenceEquals(source, null))
                return;

            if (HostAlive)
            {
                var voice = _host.Loops.Find(source);
                if (voice != null)
                {
                    if (voice.InUse)
                        _host.BeginFadeOut(voice);
                    return;
                }
            }

            // Havuza ait olmayan kaynak: sadece durdur.
            if (source != null)
                source.Stop();
        }

        /// <summary>2B ortam döngüsünü çapraz geçişle değiştirir. SoundId.None ortam sesini kapatır.</summary>
        public static void SetAmbience(SoundId loop, float volume)
        {
            if (loop == SoundId.None)
            {
                if (HostAlive)
                    _host.SetAmbience(SoundId.None, null, 0f);
                return;
            }

            if (!EnsureReady())
                return;

            _host.SetAmbience(loop, GetClip(loop), Sanitize01(volume, 1f));
        }

        // =====================================================================================
        //  EK YARDIMCILAR
        // =====================================================================================

        /// <summary>Silahın atış sesi kimliği (yakın dövüş / bilinmeyen için None).</summary>
        public static SoundId GunshotSoundFor(WeaponDefinitionData weapon) =>
            weapon == null ? SoundId.None : GunshotProfile.For(weapon).Id;

        /// <summary>Tüm tek seferlik sesleri ve döngüleri durdurur (ortam sesi hariç).</summary>
        public static void StopAll()
        {
            if (!HostAlive)
                return;

            _host.Spatial.StopAll();
            _host.Flat.StopAll();
            _host.Loops.StopAll();
        }

        /// <summary>Şu an çalan tek seferlik kanal sayısı (hata ayıklama).</summary>
        public static int ActiveVoiceCount
        {
            get
            {
                if (!HostAlive)
                    return 0;

                var now = Time.unscaledTime;
                return _host.Spatial.CountActive(now) + _host.Flat.CountActive(now) + _host.Loops.CountActive(now);
            }
        }

        // =====================================================================================
        //  İÇ
        // =====================================================================================

        private static bool EnsureReady()
        {
            if (IsInitialized && HostAlive)
                return true;

            if (_quitting)
                return false;

            if (!UnityEngine.Application.isPlaying)
            {
                if (!_warnedNoPlayMode)
                {
                    _warnedNoPlayMode = true;
                    Debug.Log("[GameAudio] Oynatma modu dışında ses çalınmaz.");
                }

                return false;
            }

            IsInitialized = false;
            Initialize();
            return IsInitialized && HostAlive;
        }

        private static void PlaySpatial(SoundId id, Vector3 position, float volume, float pitch, float minDistance,
            float maxDistance, bool propagationDelay, bool dedupe)
        {
            if (!_host.TryGetListenerPosition(out var listener))
                return;

            var distance = Vector3.Distance(listener, position);
            if (!(distance <= maxDistance))
                return; // menzil dışı ya da NaN konum

            var now = Time.unscaledTime;
            if (dedupe && IsDuplicate(id, position, now))
                return;

            // Logaritmik azalma maxDistance'ta sıfıra inmez: son %25'te yumuşak kısma ile kesintisiz bitir.
            var edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(maxDistance * 0.75f, maxDistance, distance));
            volume = Mathf.Clamp01(volume) * edge;
            if (volume <= 0.0005f)
                return;

            var clip = GetClip(id);
            if (clip == null)
                return;

            var attenuation = distance <= minDistance ? 1f : minDistance / distance;
            var importance = volume * attenuation;
            var voice = _host.Spatial.AcquireOneShot(now, importance);
            if (voice == null)
                return;

            pitch = SanitizePitch(pitch);
            var src = voice.Source;
            voice.Transform.position = position;
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.dopplerLevel = 0f;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
            src.priority = Mathf.Clamp(Mathf.RoundToInt(200f - importance * 150f), 32, 220);
            ApplyDistanceFilter(voice.LowPass, distance, maxDistance);

            var delay = 0f;
            if (propagationDelay && distance > 30f)
                delay = Mathf.Min(distance / SpeedOfSound, 3f);

            if (delay > 0.005f)
                src.PlayDelayed(delay);
            else
                src.Play();

            voice.Id = id;
            voice.Importance = importance;
            voice.StartTime = now;
            voice.EndTime = now + delay + clip.length / pitch + 0.05f;
        }

        /// <summary>
        /// Mesafeye bağlı hava soğurması: 25 m'ye kadar süzgeç kapalı, sonrası maxDistance'ta ~900 Hz'e iner (üstel).
        /// </summary>
        internal static void ApplyDistanceFilter(AudioLowPassFilter filter, float distance, float maxDistance)
        {
            if (ReferenceEquals(filter, null))
                return;

            if (distance <= NearFilterDistance || maxDistance <= NearFilterDistance)
            {
                if (filter.enabled)
                    filter.enabled = false;
                return;
            }

            var t = Mathf.Clamp01((distance - NearFilterDistance) / (maxDistance - NearFilterDistance));
            var cutoff = OpenCutoffHz * Mathf.Pow(FarCutoffHz / OpenCutoffHz, t);
            if (cutoff >= 20000f)
            {
                if (filter.enabled)
                    filter.enabled = false;
                return;
            }

            filter.cutoffFrequency = cutoff;
            if (!filter.enabled)
                filter.enabled = true;
        }

        /// <summary>Aynı sesin aynı noktada aynı anda tekrar çalınmasını önler (ör. saçma taneleri).</summary>
        private static bool IsDuplicate(SoundId id, Vector3 position, float now)
        {
            for (var i = 0; i < RecentCapacity; i++)
            {
                ref var r = ref Recent[i];
                if (r.Id == id && now - r.Time < DedupeWindow && (r.Position - position).sqrMagnitude < DedupeDistanceSqr)
                    return true;
            }

            ref var slot = ref Recent[_recentCursor];
            slot.Id = id;
            slot.Position = position;
            slot.Time = now;
            _recentCursor = (_recentCursor + 1) % RecentCapacity;
            return false;
        }

        private static void ApplyAmbientVolumeToLoops(float oldValue, float newValue)
        {
            if (!HostAlive)
                return;

            var voices = _host.Loops.Voices;
            for (var i = 0; i < voices.Count; i++)
            {
                var v = voices[i];
                if (!v.InUse || !v.Ambient || v.FadeOutRate > 0f || v.Source == null)
                    continue;

                // Çağıran ses düzeyini değiştirmiş olabilir → mevcut düzeyden tabanı geri çıkar.
                var baseVolume = oldValue > 0.001f ? v.Source.volume / oldValue : v.BaseVolume;
                v.BaseVolume = Mathf.Clamp01(baseVolume);
                v.Source.volume = v.BaseVolume * newValue;
            }
        }

        internal static void NotifyHostDestroyed(AudioHost host)
        {
            if (ReferenceEquals(_host, host))
            {
                _host = null;
                IsInitialized = false;
            }
        }

        private static bool IsAmbientCategory(SoundId id) =>
            id == SoundId.Wind || id == SoundId.Ambience || id == SoundId.DistantBattle || id == SoundId.MenuMusic;

        private static bool IsUi(SoundId id) =>
            id == SoundId.UiClick || id == SoundId.UiHover || id == SoundId.UiConfirm;

        private static float Sanitize01(float value, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? Mathf.Clamp01(fallback) : Mathf.Clamp01(value);

        private static float SanitizePitch(float pitch) =>
            float.IsNaN(pitch) || float.IsInfinity(pitch) ? 1f : Mathf.Clamp(pitch, 0.1f, 3f);

        // =====================================================================================
        //  SES ÜRETİMİ (paralel, önbellekli)
        // =====================================================================================

        /// <summary>Eksik tüm klipleri üretir: PCM iş parçacıklarında, AudioClip'ler ana iş parçacığında.</summary>
        private static void EnsureClips()
        {
            var values = (SoundId[])Enum.GetValues(typeof(SoundId));
            var maxIndex = 0;
            for (var i = 0; i < values.Length; i++)
                maxIndex = Math.Max(maxIndex, (int)values[i]);

            if (_clips == null || _clips.Length <= maxIndex)
            {
                var grown = new AudioClip[maxIndex + 1];
                if (_clips != null)
                    Array.Copy(_clips, grown, _clips.Length);
                _clips = grown;
            }

            var missingCount = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] != SoundId.None && _clips[(int)values[i]] == null)
                    missingCount++;
            }

            if (missingCount == 0)
                return;

            var order = new SoundId[missingCount];
            var k = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] != SoundId.None && _clips[(int)values[i]] == null)
                    order[k++] = values[i];
            }

            // En pahalı sesler önce: kritik yol (16 s müzik) en baştan başlar.
            Array.Sort(order, (a, b) => EstimatedCost(b).CompareTo(EstimatedCost(a)));

            var job = new RenderJob(order);
            job.RunParallel();

            for (var i = 0; i < order.Length; i++)
            {
                var samples = job.Results[i];
                if (samples == null)
                {
                    try
                    {
                        samples = ProceduralAudioSynth.Render(order[i]);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                try
                {
                    _clips[(int)order[i]] = ProceduralAudioSynth.ToClip(order[i], samples);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (job.Error != null)
                Debug.LogWarning("[GameAudio] Paralel ses üretiminde hata, sıralı üretimle tamamlandı: " + job.Error.Message);
        }

        private static int EstimatedCost(SoundId id)
        {
            switch (id)
            {
                case SoundId.MenuMusic: return 1000;
                case SoundId.DistantBattle: return 900;
                case SoundId.Ambience: return 850;
                case SoundId.Wind: return 600;
                case SoundId.HelicopterRotor: return 400;
                case SoundId.VehicleEngine: return 380;
                case SoundId.Explosion: return 300;
                case SoundId.ShotSniper: return 280;
                case SoundId.RadioChatter: return 270;
                case SoundId.ArtilleryWhistle: return 260;
                case SoundId.SmokeHiss: return 250;
                default: return ProceduralAudioSynth.IsLoop(id) ? 350 : 100;
            }
        }

        /// <summary>
        /// İş kuyruğu: ana iş parçacığı ve birkaç arka plan görevi sıradaki kimliği Interlocked ile alır.
        /// Görev başlatılamazsa (iş parçacığı yok) ana iş parçacığı tüm işi yapar; zaman aşımında eksikler sıralı üretilir.
        /// </summary>
        private sealed class RenderJob
        {
            private readonly SoundId[] _ids;
            private int _next = -1;

            public readonly float[][] Results;
            public volatile Exception Error;

            public RenderJob(SoundId[] ids)
            {
                _ids = ids;
                Results = new float[ids.Length][];
            }

            public void RunParallel()
            {
                Task[] tasks = null;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
                var workers = Mathf.Clamp(Environment.ProcessorCount - 1, 0, 7);
                workers = Math.Min(workers, _ids.Length - 1);
                if (workers > 0)
                {
                    try
                    {
                        tasks = new Task[workers];
                        for (var w = 0; w < workers; w++)
                            tasks[w] = Task.Run(Work);
                    }
                    catch (Exception e)
                    {
                        Error = e;
                    }
                }
#endif
                Work();

                if (tasks == null)
                    return;

                try
                {
                    var started = 0;
                    for (var i = 0; i < tasks.Length; i++)
                    {
                        if (tasks[i] != null)
                            started++;
                    }

                    if (started == tasks.Length)
                        Task.WaitAll(tasks, 10000);
                    else
                    {
                        for (var i = 0; i < tasks.Length; i++)
                            tasks[i]?.Wait(10000);
                    }
                }
                catch (Exception e)
                {
                    Error = e;
                }
            }

            private void Work()
            {
                int i;
                while ((i = Interlocked.Increment(ref _next)) < _ids.Length)
                {
                    try
                    {
                        Results[i] = ProceduralAudioSynth.Render(_ids[i]);
                    }
                    catch (Exception e)
                    {
                        Error = e;
                    }
                }
            }
        }

        // =====================================================================================
        //  SİLAH SINIFI PROFİLLERİ
        // =====================================================================================

        private readonly struct GunshotProfile
        {
            public readonly SoundId Id;
            public readonly float MinDistance;
            public readonly float MaxDistance;
            public readonly float Volume;
            public readonly float LocalVolume;
            public readonly float Pitch;

            private GunshotProfile(SoundId id, float minDistance, float maxDistance, float volume, float localVolume,
                float pitch = 1f)
            {
                Id = id;
                MinDistance = minDistance;
                MaxDistance = maxDistance;
                Volume = volume;
                LocalVolume = localVolume;
                Pitch = pitch;
            }

            public static GunshotProfile For(WeaponDefinitionData weapon)
            {
                switch (weapon.Category)
                {
                    case WeaponCategory.Pistol:
                        return new GunshotProfile(SoundId.ShotPistol, 6f, 300f, 0.85f, 0.8f);
                    case WeaponCategory.Smg:
                        return new GunshotProfile(SoundId.ShotSmg, 7f, 320f, 0.85f, 0.78f);
                    case WeaponCategory.AssaultRifle:
                        return weapon.AmmoType == AmmoType.Mm762
                            ? new GunshotProfile(SoundId.ShotRifle762, 14f, 600f, 1f, 0.9f)
                            : new GunshotProfile(SoundId.ShotRifle556, 12f, 520f, 1f, 0.88f);
                    case WeaponCategory.Dmr:
                        return new GunshotProfile(SoundId.ShotDmr, 16f, 700f, 1f, 0.92f);
                    case WeaponCategory.Sniper:
                        return new GunshotProfile(SoundId.ShotSniper, 22f, 1000f, 1f, 0.95f);
                    case WeaponCategory.Shotgun:
                        return new GunshotProfile(SoundId.ShotShotgun, 12f, 420f, 1f, 0.92f);
                    case WeaponCategory.Lmg:
                        return new GunshotProfile(SoundId.ShotMachineGun, 16f, 650f, 1f, 0.85f);
                    case WeaponCategory.Melee:
                        return default;
                    default:
                        // Bilinmeyen sınıf: mühimmata göre tahmin.
                        switch (weapon.AmmoType)
                        {
                            case AmmoType.Mm9: return new GunshotProfile(SoundId.ShotPistol, 6f, 300f, 0.85f, 0.8f);
                            case AmmoType.Mm762: return new GunshotProfile(SoundId.ShotRifle762, 14f, 600f, 1f, 0.9f);
                            case AmmoType.Gauge12: return new GunshotProfile(SoundId.ShotShotgun, 12f, 420f, 1f, 0.92f);
                            case AmmoType.Mm556: return new GunshotProfile(SoundId.ShotRifle556, 12f, 520f, 1f, 0.88f);
                            default: return default;
                        }
                }
            }
        }

        // =====================================================================================
        //  ALAN YENİDEN YÜKLEME KAPALIYKEN (Enter Play Mode Options) DURUM SIFIRLAMA
        // =====================================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _host = null;
            IsInitialized = false;
            _initializing = false;
            _warnedNoPlayMode = false;
            _quitting = false;
            _recentCursor = 0;
            Array.Clear(Recent, 0, Recent.Length);
            // Klipler varlıktır; hâlâ geçerli olanlar korunur, yok edilenler GetClip/EnsureClips ile yeniden üretilir.

            UnityEngine.Application.quitting -= OnApplicationQuitting;
            UnityEngine.Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting() => _quitting = true;

#if UNITY_EDITOR
        /// <summary>
        /// Editörde: oynatma modundan çıkınca ve derleme yeniden yüklenmeden önce üretilen klipleri yok et
        /// (DontUnloadUnusedAsset klipleri aksi halde her oturumda sızar).
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DestroyClips;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DestroyClips;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                DestroyClips();
        }

        private static void DestroyClips()
        {
            var clips = _clips;
            _clips = null;
            if (clips == null)
                return;

            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    UnityEngine.Object.DestroyImmediate(clips[i]);
            }
        }
#endif
    }
}
