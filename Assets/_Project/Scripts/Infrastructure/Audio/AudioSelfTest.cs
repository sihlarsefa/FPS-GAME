using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Audio.Music;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>Ses öz-testi adım türleri.</summary>
    public enum AudioSelfStep
    {
        OwnShot, DistantShot, ReloadStart, Footstep, AmbienceOn, AmbienceOff, MenuSting, IdleCheck, End
    }

    /// <summary>Zamanlanmış tek adım.</summary>
    public readonly struct AudioSelfEvent
    {
        public readonly float Time;
        public readonly AudioSelfStep Step;
        public readonly int Index;

        public AudioSelfEvent(float time, AudioSelfStep step, int index)
        {
            Time = time;
            Step = step;
            Index = index;
        }
    }

    /// <summary>Bir 100 ms örneği (CSV satırı).</summary>
    public struct AudioSelfSample
    {
        public float Time;
        public int Voices;
        public float MixCutoffHz;
        public float ListenerCutoffHz;
        public float HdrTopDb;
        public float HdrGain;
        public float MasterVolume;
        public float AmbientVolume;
        public float UiVolume;
        public float MusicVolume;
        public float ListenerVolume;
        public float MixMasterGain;
        public float MixAmbienceGain;
        public string Event;
    }

    /// <summary>
    /// "-otoses" 25 sn'lik betikli ses dizisi: kendi tüfek atışı x3, 300 m uzak atış (Acoustics), tam şarjör değiştirme,
    /// 10 ayak sesi, 5 sn ortam yatağı, menü sting'i. Saf kısım (zamanlama, CSV, boğukluk denetimi) Unity'ye dokunmaz
    /// (EditMode testli); Unity çağrıları <see cref="AudioSelfTestProbe"/> içindedir.
    /// </summary>
    public static class AudioSelfTest
    {
        public const float DurationSeconds = 25f;
        public const float SampleIntervalSeconds = 0.1f;
        public const float OpenCutoffHz = 22000f;
        public const float DistantShotMeters = 300f;
        public const int FootstepCount = 10;
        public const string CsvFileName = "ses_rapor.csv";
        public const string Header =
            "t_s,voices,mix_cutoff_hz,listener_cutoff_hz,hdr_top_db,hdr_gain,master_vol,ambient_vol,ui_vol,music_vol,listener_vol,mix_master_gain,mix_ambience_gain,event";

        /// <summary>Betik: zaman sıralı adımlar (0..25 sn).</summary>
        public static List<AudioSelfEvent> BuildSchedule()
        {
            var list = new List<AudioSelfEvent>
            {
                new AudioSelfEvent(0.3f, AudioSelfStep.IdleCheck, 0),
                new AudioSelfEvent(1.0f, AudioSelfStep.OwnShot, 0),
                new AudioSelfEvent(1.8f, AudioSelfStep.OwnShot, 1),
                new AudioSelfEvent(2.6f, AudioSelfStep.OwnShot, 2),
                new AudioSelfEvent(4.5f, AudioSelfStep.DistantShot, 0),
                new AudioSelfEvent(7.0f, AudioSelfStep.ReloadStart, 0)
            };
            for (var i = 0; i < FootstepCount; i++)
                list.Add(new AudioSelfEvent(12f + i * 0.5f, AudioSelfStep.Footstep, i));
            list.Add(new AudioSelfEvent(17f, AudioSelfStep.AmbienceOn, 0));
            list.Add(new AudioSelfEvent(22f, AudioSelfStep.AmbienceOff, 0));
            list.Add(new AudioSelfEvent(22.2f, AudioSelfStep.MenuSting, 0));
            list.Add(new AudioSelfEvent(24.8f, AudioSelfStep.IdleCheck, 1));
            list.Add(new AudioSelfEvent(DurationSeconds, AudioSelfStep.End, 0));
            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            return list;
        }

        public static string StepLabel(AudioSelfEvent e)
        {
            switch (e.Step)
            {
                case AudioSelfStep.OwnShot: return "own_shot_" + (e.Index + 1).ToString(CultureInfo.InvariantCulture);
                case AudioSelfStep.DistantShot: return "distant_shot_300m";
                case AudioSelfStep.ReloadStart: return "reload";
                case AudioSelfStep.Footstep: return "footstep_" + (e.Index + 1).ToString(CultureInfo.InvariantCulture);
                case AudioSelfStep.AmbienceOn: return "ambience_on";
                case AudioSelfStep.AmbienceOff: return "ambience_off";
                case AudioSelfStep.MenuSting: return "menu_sting";
                case AudioSelfStep.IdleCheck: return "idle_check";
                default: return "end";
            }
        }

        private static string F(float v, string fmt = "0.###")
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                return "nan";
            return v.ToString(fmt, CultureInfo.InvariantCulture);
        }

        public static string FormatRow(AudioSelfSample s)
        {
            var sb = new StringBuilder(128);
            sb.Append(F(s.Time, "0.00")).Append(',')
              .Append(s.Voices.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(F(s.MixCutoffHz, "0")).Append(',')
              .Append(F(s.ListenerCutoffHz, "0")).Append(',')
              .Append(F(s.HdrTopDb, "0.0")).Append(',')
              .Append(F(s.HdrGain)).Append(',')
              .Append(F(s.MasterVolume)).Append(',')
              .Append(F(s.AmbientVolume)).Append(',')
              .Append(F(s.UiVolume)).Append(',')
              .Append(F(s.MusicVolume)).Append(',')
              .Append(F(s.ListenerVolume)).Append(',')
              .Append(F(s.MixMasterGain)).Append(',')
              .Append(F(s.MixAmbienceGain)).Append(',')
              .Append(s.Event ?? string.Empty);
            return sb.ToString();
        }

        /// <summary>Boğukluk denetimi: boştayken miks ve dinleyici süzgeci açık (>= 21000 Hz) olmalı.</summary>
        public static bool IsOpen(float mixCutoffHz, float listenerCutoffHz)
        {
            return mixCutoffHz >= 21000f && listenerCutoffHz >= 21000f;
        }

        /// <summary>Denetim günlük satırı. Açıkken tam olarak "[SES] boğukluk kontrol: cutoff=22000 idle".</summary>
        public static string IdleCheckLine(float mixCutoffHz, float listenerCutoffHz)
        {
            if (IsOpen(mixCutoffHz, listenerCutoffHz))
                return "[SES] boğukluk kontrol: cutoff=22000 idle";
            return "[SES] boğukluk kontrol: BAŞARISIZ cutoff=" + F(Math.Min(mixCutoffHz, listenerCutoffHz), "0") + " idle";
        }

        /// <summary>Özet satırı (en yüksek ses sayısı, en düşük kesim).</summary>
        public static string SummaryLine(int samples, int peakVoices, float minCutoffHz, float minHdrGain)
        {
            return "[SES] özet: örnek=" + samples.ToString(CultureInfo.InvariantCulture)
                   + " tepeSes=" + peakVoices.ToString(CultureInfo.InvariantCulture)
                   + " enDüşükKesim=" + F(minCutoffHz, "0") + " Hz enDüşükHdrKazanç=" + F(minHdrGain);
        }

        /// <summary>Kesim değeri sürekli en düşük hangi seviyede (boş liste = açık).</summary>
        public static float MinCutoff(IReadOnlyList<float> values)
        {
            var m = OpenCutoffHz;
            if (values == null)
                return m;
            for (var i = 0; i < values.Count; i++)
                if (!float.IsNaN(values[i]) && values[i] < m)
                    m = values[i];
            return m;
        }

        /// <summary>
        /// Çalışan oturum: <see cref="Tick"/> her karede çağrılır; vadesi gelen adımları tetikler, 100 ms'de bir örnek alır.
        /// </summary>
        public sealed class Session
        {
            private readonly List<AudioSelfEvent> _events = BuildSchedule();
            private readonly StringBuilder _csv = new StringBuilder(4096);
            private int _next;
            private int _samples;
            private int _peakVoices;
            private float _minCutoff = OpenCutoffHz;
            private float _minGain = float.MaxValue;
            private float _nextSample;
            private string _pendingEvent = string.Empty;

            public string Csv => _csv.ToString();
            public bool Done { get; private set; }
            public int Samples => _samples;
            public bool IdleOk { get; private set; } = true;

            public Session()
            {
                _csv.AppendLine(Header);
            }

            public void Tick(float t)
            {
                if (Done)
                    return;
                while (_next < _events.Count && _events[_next].Time <= t)
                {
                    var e = _events[_next++];
                    _pendingEvent = _pendingEvent.Length == 0 ? StepLabel(e) : _pendingEvent + "+" + StepLabel(e);
                    if (e.Step == AudioSelfStep.End)
                    {
                        Done = true;
                        break;
                    }

                    AudioSelfTestProbe.Execute(e);
                    if (e.Step == AudioSelfStep.IdleCheck)
                    {
                        AudioSelfTestProbe.Read(t, string.Empty, out var s0);
                        var ok = IsOpen(s0.MixCutoffHz, s0.ListenerCutoffHz);
                        if (!ok)
                            IdleOk = false;
                        var line = IdleCheckLine(s0.MixCutoffHz, s0.ListenerCutoffHz);
                        if (ok) Debug.Log(line); else Debug.LogWarning(line);
                    }
                }

                if (t >= _nextSample || Done)
                {
                    _nextSample = t + SampleIntervalSeconds;
                    AudioSelfTestProbe.Read(t, _pendingEvent, out var s);
                    _pendingEvent = string.Empty;
                    _csv.AppendLine(FormatRow(s));
                    _samples++;
                    if (s.Voices > _peakVoices) _peakVoices = s.Voices;
                    var c = Math.Min(s.MixCutoffHz, s.ListenerCutoffHz);
                    if (c < _minCutoff) _minCutoff = c;
                    if (s.HdrGain < _minGain) _minGain = s.HdrGain;
                }
            }

            public string Summary() =>
                SummaryLine(_samples, _peakVoices, _minCutoff, _minGain == float.MaxValue ? 1f : _minGain);
        }
    }

    /// <summary>Ses öz-testinin Unity'ye dokunan kısmı (oyun içi; EditMode'da çağrılmaz).</summary>
    internal static class AudioSelfTestProbe
    {
        private static AudioListener _listener;
        private static AudioLowPassFilter _lowpass;
        private static WeaponDefinitionData _rifle;

        private static WeaponDefinitionData Rifle()
        {
            if (_rifle != null)
                return _rifle;
            foreach (var w in WeaponCatalog.All)
                if (w != null && w.Category == WeaponCategory.AssaultRifle)
                {
                    _rifle = w;
                    return w;
                }

            _rifle = WeaponDefinitionData.AssaultRifle;
            return _rifle;
        }

        private static AudioListener Listener()
        {
            if (_listener == null || !_listener.isActiveAndEnabled)
            {
                _listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
                _lowpass = null;
            }

            return _listener;
        }

        private static Vector3 ListenerPos(out Vector3 forward)
        {
            var l = Listener();
            if (l != null)
            {
                forward = l.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
                forward.Normalize();
                return l.transform.position;
            }

            forward = Vector3.forward;
            return Vector3.zero;
        }

        public static void Execute(AudioSelfEvent e)
        {
            try
            {
                var pos = ListenerPos(out var fwd);
                switch (e.Step)
                {
                    case AudioSelfStep.OwnShot:
                    {
                        var w = Rifle();
                        AudioMix.ReportLoudDb(HdrAudioMath.EventLevelDb(HdrEventKind.OwnRifle, 1f));
                        GameAudio.PlayGunshot(w, pos + fwd * 0.5f, true, false, fwd);
                        Acoustics.OnShot(pos + fwd * 0.5f, fwd, CaliberClass.Rifle, false, false);
                        break;
                    }
                    case AudioSelfStep.DistantShot:
                    {
                        var origin = pos + fwd * AudioSelfTest.DistantShotMeters;
                        var dir = (pos - origin).normalized;
                        AudioMix.ReportLoudDb(HdrAudioMath.EventLevelDb(HdrEventKind.OwnRifle, AudioSelfTest.DistantShotMeters));
                        GameAudio.PlayGunshot(Rifle(), origin, false, false, dir);
                        Acoustics.OnShot(origin, dir, CaliberClass.Rifle, false, true);
                        break;
                    }
                    case AudioSelfStep.ReloadStart:
                        GameAudio.PlayReload(Rifle(), pos, true);
                        break;
                    case AudioSelfStep.Footstep:
                    {
                        var feet = pos + Vector3.down * 1.6f;
                        var vol = 0.55f * AudioMix.HdrGainFor(HdrEventKind.Footstep, feet);
                        GameAudio.Play(SoundId.FootstepGrass, feet, vol, 1f + (e.Index % 2 == 0 ? -0.03f : 0.03f), 25f);
                        break;
                    }
                    case AudioSelfStep.AmbienceOn:
                        GameAudio.SetAmbience(SoundId.Ambience, 0.8f);
                        break;
                    case AudioSelfStep.AmbienceOff:
                        GameAudio.SetAmbience(SoundId.None, 0f);
                        break;
                    case AudioSelfStep.MenuSting:
                        MusicStings.Play(StingKind.MatchStart);
                        UiSounds.Play(UiSfx.Press);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SES] adım hatası " + e.Step + ": " + ex.Message);
            }
        }

        public static void Read(float t, string evt, out AudioSelfSample s)
        {
            s = new AudioSelfSample { Time = t, Event = evt, MixCutoffHz = AudioSelfTest.OpenCutoffHz, ListenerCutoffHz = AudioSelfTest.OpenCutoffHz };
            try
            {
                s.Voices = GameAudio.ActiveVoiceCount;
                var cur = AudioMix.State.Current;
                s.MixCutoffHz = cur.LowpassHz;
                s.MixMasterGain = cur.MasterGain;
                s.MixAmbienceGain = cur.AmbienceGain;
                if (Listener() != null)
                {
                    if (_lowpass == null)
                        _lowpass = _listener.GetComponent<AudioLowPassFilter>();
                    if (_lowpass != null && _lowpass.enabled)
                        s.ListenerCutoffHz = _lowpass.cutoffFrequency;
                }

                s.HdrTopDb = AudioMix.Hdr.TopDb;
                s.HdrGain = AudioMix.HdrGain(HdrAudioMath.SourceLevelDb(HdrEventKind.Footstep));
                s.MasterVolume = GameAudio.MasterVolume;
                s.AmbientVolume = GameAudio.AmbientVolume;
                s.UiVolume = UiSounds.VolumeProvider != null ? UiSounds.VolumeProvider() : 0f;
                s.MusicVolume = MusicStings.VolumeProvider != null ? MusicStings.VolumeProvider() : 0f;
                s.ListenerVolume = AudioListener.volume;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SES] örnek hatası: " + ex.Message);
            }
        }
    }
}
