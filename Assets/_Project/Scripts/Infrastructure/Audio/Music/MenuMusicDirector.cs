using System;
using System.Threading.Tasks;
using Project.Infrastructure.Audio.HdrMix;
using UnityEngine;

namespace Project.Infrastructure.Audio.Music
{
    /// <summary>
    /// Ana menü müziği: tema ilk menü yüklemesinde arka planda üretilir (sonra önbellekten okunur), döngüde çalınır.
    /// Ses düzeyi <c>volume</c> sağlayıcısını (MusicVolume x MasterVolume) her karede izler. Sahneyle birlikte yok olur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuMusicDirector : MonoBehaviour
    {
        private const float BaseGain = 0.6f;
        private const float FadeInSeconds = 3f;

        private static MenuMusicDirector _instance;

        private Func<float> _volume = () => 1f;
        private AudioSource _source;
        private Task<StereoBuffer> _task;
        private AudioClip _clip;
        private float _fade;
        private bool _failed;

        public bool IsPlaying => _source != null && _source.isPlaying;

        /// <summary>Null-güvenli giriş: ana menü bootstrap'inden tek satırla çağrılır.</summary>
        public static MenuMusicDirector Ensure(Func<float> volume)
        {
            if (_instance == null)
            {
                var go = new GameObject("[Menü Müziği]");
                _instance = go.AddComponent<MenuMusicDirector>();
            }
            if (volume != null)
            {
                _instance._volume = volume;
                MusicStings.VolumeProvider = volume;
            }
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = 0f;
            MixerRouting.Route(_source, MixChannel.Muzik);
        }

        private void Start()
        {
            var dir = MusicCache.Directory; // ana iş parçacığında al
            _task = Task.Run(() => MusicCache.LoadOrRender(dir));
            MusicStings.Warmup();
        }

        private void Update()
        {
            if (_failed || _source == null) return;

            if (_task != null && _task.IsCompleted)
            {
                if (_task.IsFaulted || _task.IsCanceled)
                {
                    _failed = true;
                    Debug.LogWarning("[Müzik] Menü teması üretilemedi: " + _task.Exception?.GetBaseException().Message);
                    _task = null;
                    return;
                }
                var buf = _task.Result;
                _task = null;
                _clip = MusicClips.Create("HarekatMenuTheme", buf);
                _source.clip = _clip;
                _source.Play();
            }

            if (_clip == null) return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / FadeInSeconds);
            float v;
            try { v = Mathf.Clamp01(_volume()); } catch (Exception) { v = 1f; }
            _source.volume = v * BaseGain * _fade;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_clip != null) Destroy(_clip);
        }
    }

    internal static class MusicClips
    {
        public static AudioClip Create(string name, StereoBuffer buf)
        {
            var clip = AudioClip.Create(name, buf.Frames, 2, buf.SampleRate, false);
            clip.SetData(buf.Interleave(), 0);
            return clip;
        }
    }
}
