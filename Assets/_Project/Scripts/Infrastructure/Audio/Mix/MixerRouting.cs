using System;
using Project.Infrastructure.Audio.Dialogue;
using UnityEngine;
using UnityEngine.Audio;

namespace Project.Infrastructure.Audio.HdrMix
{
    /// <summary>Gerçek AudioMixer kanalları (grup hiyerarşisi: Master → Muzik, Efekt → Silah/Darbe/Adim/Arac, Ortam, Konusma, UI).</summary>
    public enum MixChannel
    {
        Master = 0,
        Muzik,
        Efekt,
        Silah,
        Darbe,
        Adim,
        Arac,
        Ortam,
        Konusma,
        UI
    }

    /// <summary>
    /// AudioSource'ları Resources/Audio/MainMixer (MixerBuilder üretir) gruplarına yönlendirir. Mixer yoksa her çağrı
    /// sessizce hiçbir şey yapmaz; mevcut script tabanlı ses yolları aynen çalışır.
    /// Kullanıcı ses ayarları (SFX/Müzik/Konuşma) açık dB parametrelerine eşlenir; konuşma kısması
    /// (DialogueDirector.DuckGain) Müzik grubuna mixer üzerinden uygulanır. Ortam grubunun kısması betikte zaten
    /// yapıldığından (GameAudio/AmbienceDirector) çift kısmayı önlemek için varsayılan olarak mixer'a yazılmaz.
    /// Not: AudioMixer'da çalışma zamanında anlık görüntü (snapshot) oluşturulamaz; sidechain yerine açık parametre sürülür.
    /// </summary>
    public static class MixerRouting
    {
        public const string MuzikParam = "MuzikVol";
        public const string EfektParam = "EfektVol";
        public const string SilahParam = "SilahVol";
        public const string DarbeParam = "DarbeVol";
        public const string AdimParam = "AdimVol";
        public const string AracParam = "AracVol";
        public const string KonusmaParam = "KonusmaVol";
        public const string UiParam = "UIVol";

        /// <summary>Master lowpass kesim frekansı parametresi (mixer'da açık değilse boğma sessizce uygulanmaz).</summary>
        public const string MuffleCutoffParam = "MuffleCutoffHz";
        // Master lowpass + "MuffleCutoffHz" MixerBuilder.cs tarafından kurulur (SetupAll).
        public const float MuffleOpenHz = 22000f;
        public const float MuffleClosedHz = 1200f;

        /// <summary>Konuşma kısmasını Ortam grubuna da mixer üzerinden uygula (betikteki kısma kaldırılırsa true yapın).</summary>
        public static bool MixerDucksAmbience;

        private static AudioMixer _mixer;
        private static bool _tried;
        private static AudioMixerGroup[] _groups;
        private static readonly float[] Base = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        private static readonly bool[] Driven = new bool[10];
        private static RoutingDriver _driver;

        /// <summary>Mixer varlığı bulundu mu.</summary>
        public static bool HasMixer
        {
            get
            {
                EnsureMixer();
                return _mixer != null;
            }
        }

        public static AudioMixer Mixer
        {
            get
            {
                EnsureMixer();
                return _mixer;
            }
        }

        /// <summary>Kaynağı kanalın grubuna bağlar. Mixer ya da grup yoksa no-op. Null-safe.</summary>
        public static void Route(AudioSource source, MixChannel channel)
        {
            if (ReferenceEquals(source, null) || source == null)
                return;
            var g = GroupOf(channel);
            if (g == null)
                return;
            source.outputAudioMixerGroup = g;
            EnsureDriver();
        }

        /// <summary>Doğrusal 0..1 boğulma gücünden lowpass kesim frekansı (Hz); üstel (algısal) geçiş.</summary>
        public static float MuffleCutoffFor(float amount)
        {
            var a = Mathf.Clamp01(float.IsNaN(amount) ? 0f : amount);
            return MuffleOpenHz * Mathf.Pow(MuffleClosedHz / MuffleOpenHz, a);
        }

        /// <summary>
        /// Bastırma boğulması: SuppressionState.MuffleAmount (0..1, 0.5 sn'de söner) ile master lowpass'ı sürer.
        /// Mixer ya da parametre yoksa no-op (false döner).
        /// </summary>
        public static bool SetMuffle(float amount)
        {
            EnsureMixer();
            return _mixer != null && _mixer.SetFloat(MuffleCutoffParam, MuffleCutoffFor(amount));
        }

        /// <summary>Kanalın grubunu döndürür (yoksa null).</summary>
        public static AudioMixerGroup GroupOf(MixChannel channel)
        {
            EnsureMixer();
            if (_mixer == null)
                return null;
            var i = (int)channel;
            if (i < 0 || i >= _groups.Length)
                return null;
            return _groups[i];
        }

        /// <summary>Kanal temel ses seviyesi (0..1). Konuşma kısması ayrıca uygulanır.</summary>
        public static void SetVolume(MixChannel channel, float linear)
        {
            var i = (int)channel;
            if (i < 0 || i >= Base.Length)
                return;
            Base[i] = Mathf.Clamp01(float.IsNaN(linear) ? 1f : linear);
            Push();
        }

        /// <summary>Ayarlar: SFX → Efekt, Müzik → Muzik, Konuşma → Konusma, menü sesi → UI. Mixer yoksa no-op.</summary>
        public static void ApplySettings(float sfx, float music, float voice, float ui = 1f)
        {
            Base[(int)MixChannel.Efekt] = Mathf.Clamp01(sfx);
            Base[(int)MixChannel.Muzik] = Mathf.Clamp01(music);
            Base[(int)MixChannel.Konusma] = Mathf.Clamp01(voice);
            Base[(int)MixChannel.UI] = Mathf.Clamp01(ui);
            Push();
        }

        /// <summary>Doğrusal kazancı dB'ye çevirir (0 → -80 dB).</summary>
        public static float ToDb(float linear) => linear <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(linear));

        /// <summary>
        /// Kanalın ses düzeyini mixer şu an gerçekten sürüyor mu (mixer var + açık parametre yazıldı). Betik tarafı (ör. müzik
        /// sağlayıcıları) kullanıcı seviyesini ikinci kez uygulamasın diye buna bakar; mixer yoksa/parametre eksikse false.
        /// </summary>
        public static bool DrivesVolume(MixChannel channel)
        {
            var i = (int)channel;
            return _mixer != null && i >= 0 && i < Driven.Length && Driven[i];
        }

        /// <summary>Mixer parametrelerini güncel taban seviye × kısma ile yazar.</summary>
        public static void Push()
        {
            EnsureMixer();
            if (_mixer == null)
                return;
            var duck = Mathf.Clamp01(DialogueDirector.DuckGain);
            Set(MixChannel.Muzik, MuzikParam, Base[(int)MixChannel.Muzik] * duck);
            Set(MixChannel.Efekt, EfektParam, Base[(int)MixChannel.Efekt]);
            Set(MixChannel.Silah, SilahParam, Base[(int)MixChannel.Silah]);
            Set(MixChannel.Darbe, DarbeParam, Base[(int)MixChannel.Darbe]);
            Set(MixChannel.Adim, AdimParam, Base[(int)MixChannel.Adim]);
            Set(MixChannel.Arac, AracParam, Base[(int)MixChannel.Arac]);
            Set(MixChannel.Konusma, KonusmaParam, Base[(int)MixChannel.Konusma]);
            Set(MixChannel.UI, UiParam, Base[(int)MixChannel.UI]);
            if (MixerDucksAmbience)
                Set(MixChannel.Ortam, AudioMix.AmbienceParam, Base[(int)MixChannel.Ortam] * duck);
        }

        private static void Set(MixChannel channel, string param, float linear)
        {
            // Parametre mixer'da açık değilse SetFloat false döner; sorun değil (DrivesVolume false kalır).
            Driven[(int)channel] = _mixer.SetFloat(param, ToDb(linear));
        }

        private static void EnsureMixer()
        {
            if (_tried)
                return;
            _tried = true;
            _groups = new AudioMixerGroup[Enum.GetValues(typeof(MixChannel)).Length];
            try
            {
                _mixer = Resources.Load<AudioMixer>(AudioMix.MixerResource);
            }
            catch (Exception)
            {
                _mixer = null;
            }

            if (_mixer == null)
                return;
            foreach (MixChannel c in Enum.GetValues(typeof(MixChannel)))
            {
                try
                {
                    var found = _mixer.FindMatchingGroups(c == MixChannel.Master ? "Master" : c.ToString());
                    if (found != null && found.Length > 0)
                        _groups[(int)c] = found[0];
                }
                catch (Exception)
                {
                    // grup yok → yönlendirme yok.
                }
            }
        }

        private static void EnsureDriver()
        {
            if (_driver != null || !UnityEngine.Application.isPlaying)
                return;
            var go = new GameObject("[MixerRouting]");
            go.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _driver = go.AddComponent<RoutingDriver>();
        }

        /// <summary>Test/sahne sıfırlama: önbelleği temizler.</summary>
        internal static void ResetForTests()
        {
            _tried = false;
            _mixer = null;
            _groups = null;
            for (var i = 0; i < Base.Length; i++)
            {
                Base[i] = 1f;
                Driven[i] = false;
            }
        }

        [DisallowMultipleComponent]
        [AddComponentMenu("")]
        private sealed class RoutingDriver : MonoBehaviour
        {
            private float _last = -1f;

            private void Update()
            {
                var d = DialogueDirector.DuckGain;
                if (Mathf.Abs(d - _last) < 0.002f)
                    return;
                _last = d;
                Push();
            }
        }
    }
}
