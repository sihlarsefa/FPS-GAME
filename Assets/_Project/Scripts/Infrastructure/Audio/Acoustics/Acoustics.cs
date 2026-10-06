using System.Collections.Generic;
using Project.Infrastructure.Audio.HdrMix;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Balistik akustik giriş noktası (statik). Atış ve mermi geçişi bildirimlerini alır; ses hızı gecikmesi,
    /// süpersonik crack / whiz, vadi yankısı ve kapalı alan slapback katmanlarını kendi küçük ses havuzundan
    /// (alçak geçiren süzgeçli) çalar. Klip yoksa (GameAudio.GetClip null) o katman sessizce atlanır; ana atış sesi
    /// GameAudio'da (prosedürel yedek dahil) kalır. Yalnızca ana iş parçacığı. Sunucuda (GameAudio.Enabled false) no-op.
    /// </summary>
    public static class Acoustics
    {
        public static bool Enabled = true;

        private const float EchoCooldown = 0.35f;
        private const float SlapCooldown = 0.12f;
        private const float MaxShooterDistance = 900f;

        private static readonly RayBudget Budget = new RayBudget(40f, 16f);
        private static readonly float[] LHit = new float[5];
        private static readonly float[] SHit = new float[5];
        private static readonly EchoPlan[] Plans = new EchoPlan[5];
        private static readonly float[] EchoAngles = { 0f, -35f, 35f, -70f, 70f };
        private static readonly Vector3[] WallDirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        private static float _lastEcho = -10f;
        private static float _lastSlap = -10f;

        /// <summary>Bu mesafedeki (m) atışı Acoustics zaten çalıyor mu (uzak çatışma planlayıcısı çift çalmasın).</summary>
        public static bool CoversShot(float distance) => Enabled && distance <= MaxShooterDistance;

        /// <summary>Bu mesafedeki patlamayı Acoustics zaten çalıyor mu.</summary>
        public static bool CoversExplosion(float distance) =>
            Enabled && distance <= AcousticsMath.Profile(CaliberClass.Explosion).AudibleRange;

        /// <summary>Ses hızına göre atış gecikmesi (sn) — GameAudio mevcut gecikmesi yerine bunu kullanabilir.</summary>
        public static float ReportDelay(Vector3 origin)
        {
            return TryGetListener(out var lp) ? AcousticsMath.Delay(Vector3.Distance(lp, origin)) : 0f;
        }

        /// <summary>
        /// Atış bildirimi. Yankı (vadi/bina) ve kapalı alan slapback planlar. playMainReport true ise ana namlu raporunu da
        /// ses hızı gecikmesi + hava sönümüyle bu sınıf çalar (GameAudio kendi çalmasını atlamalı).
        /// </summary>
        public static void OnShot(Vector3 origin, Vector3 dir, CaliberClass caliber, bool suppressed, bool playMainReport = false)
        {
            Project.Infrastructure.World.AmbientLife.NotifyGunfire(origin);
            if (!Ready(out var lp, out var now))
                return;
            var direct = Vector3.Distance(lp, origin);
            if (direct > MaxShooterDistance)
                return;
            var near = direct < 3f;
            var directEff = near ? 0f : direct;

            if (playMainReport && !near)
            {
                var g = AcousticsMath.DistanceGain(caliber, direct, suppressed);
                Schedule(MainSound(caliber, direct), origin, now + AcousticsMath.Delay(direct), g,
                    AcousticsMath.CutoffHz(caliber, direct, suppressed), 8f, AcousticsMath.EffectiveRange(caliber, suppressed));
            }

            if (suppressed && caliber != CaliberClass.Sniper)
            {
                // Susturuculu atış: yankı ve slapback ihmal edilebilir.
                return;
            }

            // (5) Kapalı alan slapback
            if (now - _lastSlap >= SlapCooldown)
            {
                _lastSlap = now;
                TrySlapback(lp, origin, caliber, suppressed, direct, now);
            }

            // (4) Arazi/vadi yankısı
            if (now - _lastEcho >= EchoCooldown)
            {
                _lastEcho = now;
                TryEchoes(lp, origin, dir, caliber, suppressed, directEff, now);
            }
        }

        /// <summary>Patlama: ses hızı gecikmeli patlama raporu + yankılar (şiddete göre).</summary>
        public static void OnExplosion(Vector3 position, float power = 1f)
        {
            Project.Infrastructure.World.AmbientLife.NotifyGunfire(position);
            if (!Ready(out var lp, out var now))
                return;
            var d = Vector3.Distance(lp, position);
            var g = AcousticsMath.DistanceGain(CaliberClass.Explosion, d) * Mathf.Clamp(power, 0.3f, 2f);
            if (d > 15f)
                Schedule(SoundId.Explosion, position, now + AcousticsMath.Delay(d), g,
                    AcousticsMath.CutoffHz(CaliberClass.Explosion, d), 20f, AcousticsMath.Profile(CaliberClass.Explosion).AudibleRange);
            if (now - _lastEcho >= EchoCooldown)
            {
                _lastEcho = now;
                var dir = (position - lp);
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
                TryEchoes(lp, position, dir.normalized, CaliberClass.Explosion, false, d, now);
            }
        }

        /// <summary>Yerel oyuncunun yakınından geçen mermi (ıska noktası, çap mm, mesafe m); Presentation bastırma sistemi dinler.</summary>
        public static event System.Action<Vector3, float, float> NearMiss;

        private static float CaliberMm(CaliberClass c)
        {
            switch (c)
            {
                case CaliberClass.Pistol: return 9f;
                case CaliberClass.MachineGun: return 7.62f;
                case CaliberClass.Sniper: return 8.6f;
                default: return 5.56f;
            }
        }

        /// <summary>
        /// Mermi geçişi: ışının dinleyiciye en yakın yaklaşması 6 m'den azsa süpersonik crack (mermi yolundan, atıcıdan değil)
        /// ve whiz çalar. Yerel oyuncunun kendi mermisi (başlangıç &lt;2 m) atlanır.
        /// </summary>
        public static void OnBulletPassed(Vector3 rayOrigin, Vector3 rayDir, float maxDist,
            CaliberClass caliber = CaliberClass.Rifle)
        {
            if (!Ready(out var lp, out var now))
                return;
            if ((rayOrigin - lp).sqrMagnitude < 4f)
                return;
            if (!AcousticsMath.ClosestApproach(rayOrigin, rayDir, maxDist, lp, out var along, out var miss, out var cp))
                return;
            if (miss < 6f)
                NearMiss?.Invoke(cp, CaliberMm(caliber), miss);
            var v = AcousticsMath.Profile(caliber).MuzzleVelocity;
            AcousticsMath.NearMissLayers(miss, v, out var crack, out var whiz);
            var due = now + AcousticsMath.BulletTravelDelay(along, v);
            var pitch = Random.Range(0.94f, 1.06f);
            if (crack)
                Schedule(SoundId.BulletCrack, cp, due, AcousticsMath.CrackGain(miss), 22000f, 2f, 60f, pitch);
            if (whiz)
                Schedule(SoundId.BulletWhiz, cp, due, AcousticsMath.WhizGain(miss), 22000f, 2f, 40f, pitch);
        }

        // ------------------------------------------------------------------------------------------

        private static void TryEchoes(Vector3 lp, Vector3 origin, Vector3 dir, CaliberClass c, bool suppressed,
            float direct, float now)
        {
            if (!Budget.TryConsume(now, EchoAngles.Length))
                return;
            var flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 1e-4f)
                flat = origin - lp;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-4f)
                flat = Vector3.forward;
            flat.Normalize();
            var from = lp + Vector3.up * 1.5f;
            for (var i = 0; i < EchoAngles.Length; i++)
            {
                LHit[i] = 0f; SHit[i] = 0f;
                var rd = Quaternion.AngleAxis(EchoAngles[i], Vector3.up) * flat;
                if (Physics.Raycast(from, rd, out var hit, AcousticsMath.EchoMaxDistance, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore) && hit.distance >= AcousticsMath.EchoMinDistance)
                {
                    LHit[i] = hit.distance;
                    SHit[i] = Vector3.Distance(origin, hit.point);
                }
            }
            var n = AcousticsMath.PlanEchoes(LHit, SHit, EchoAngles.Length, direct, c, suppressed, Plans, 3);
            for (var k = 0; k < n; k++)
            {
                var p = Plans[k];
                var rd = Quaternion.AngleAxis(EchoAngles[p.RayIndex], Vector3.up) * flat;
                var pos = from + rd * LHit[p.RayIndex];
                // Daha uzun yankı için vadi kuyruğu, kısa ise orta mesafe uzak atış.
                var id = p.Delay > 0.35f ? SoundId.ShotTailValley : SoundId.ShotDistantMid;
                Schedule(id, pos, now + p.Delay, p.Gain, p.CutoffHz, 15f, 450f);
            }
        }

        private static void TrySlapback(Vector3 lp, Vector3 origin, CaliberClass c, bool suppressed, float direct, float now)
        {
            if (direct > 150f || !Budget.TryConsume(now, 5))
                return;
            var from = origin + Vector3.up * 1.2f;
            var ceil = 0f;
            if (Physics.Raycast(from, Vector3.up, out var up, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                ceil = up.distance;
            if (ceil <= 0f)
                return;
            var walls = 0;
            var nearest = 12f;
            for (var i = 0; i < WallDirs.Length; i++)
            {
                if (Physics.Raycast(from, WallDirs[i], out var h, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    walls++;
                    if (h.distance < nearest) nearest = h.distance;
                }
            }
            var score = AcousticsMath.IndoorScore(ceil, walls);
            var gain = AcousticsMath.SlapbackGain(c, score, direct, suppressed);
            if (gain <= 0f)
                return;
            var arrive = AcousticsMath.Delay(direct);
            Schedule(SoundId.ShotTailIndoor, origin, now + arrive + AcousticsMath.SlapbackDelay(nearest), gain,
                Mathf.Min(6000f, AcousticsMath.CutoffHz(c, Mathf.Max(direct, 10f), suppressed)), 5f, 150f);
        }

        private static SoundId MainSound(CaliberClass c, float distance)
        {
            // Uzak atışlar yakın namlu sesi gibi değil, mesafe katmanı (orta/uzak) olarak duyulur.
            if (c != CaliberClass.Explosion)
            {
                if (distance >= 150f) return SoundId.ShotDistantFar;
                if (distance >= 70f) return SoundId.ShotDistantMid;
            }

            switch (c)
            {
                case CaliberClass.Pistol: return SoundId.ShotPistol;
                case CaliberClass.MachineGun: return SoundId.ShotMachineGun;
                case CaliberClass.Sniper: return SoundId.ShotSniper;
                case CaliberClass.Explosion: return SoundId.Explosion;
                default: return SoundId.ShotRifle556;
            }
        }

        private static void Schedule(SoundId id, Vector3 pos, float due, float volume, float cutoff, float minD, float maxD,
            float pitch = 1f)
        {
            var host = AcousticsHost.Get();
            if (host == null)
                return;
            host.Queue.Enqueue(new AcousticEvent
            {
                DueTime = due, Id = id, Position = pos, Volume = Mathf.Clamp(volume, 0f, 1.5f),
                Pitch = pitch, CutoffHz = cutoff, MinDistance = minD, MaxDistance = Mathf.Max(maxD, minD + 1f)
            });
        }

        private static bool Ready(out Vector3 listenerPos, out float now)
        {
            now = Time.time;
            listenerPos = default;
            return Enabled && UnityEngine.Application.isPlaying && GameAudio.Enabled && TryGetListener(out listenerPos);
        }

        private static AudioListener _listener;
        private static float _listenerCheck = -10f;

        private static bool TryGetListener(out Vector3 pos)
        {
            pos = default;
            if (!UnityEngine.Application.isPlaying)
                return false;
            if ((_listener == null || !_listener.isActiveAndEnabled) && Time.unscaledTime - _listenerCheck > 1f)
            {
                _listenerCheck = Time.unscaledTime;
                _listener = Object.FindAnyObjectByType<AudioListener>();
            }
            if (_listener == null || !_listener.isActiveAndEnabled)
                return false;
            pos = _listener.transform.position;
            return true;
        }
    }

    /// <summary>Gecikmeli akustik olayları çalan sahne nesnesi (DontDestroyOnLoad, 10 kanallı havuz).</summary>
    internal sealed class AcousticsHost : MonoBehaviour
    {
        private const int VoiceCount = 10;
        private static AcousticsHost _instance;

        public readonly AcousticQueue Queue = new AcousticQueue(48);
        private readonly List<AcousticEvent> _due = new List<AcousticEvent>(8);
        private AudioSource[] _sources;
        private AudioLowPassFilter[] _filters;
        private float[] _endTimes;

        public static AcousticsHost Get()
        {
            if (_instance != null)
                return _instance;
            if (!UnityEngine.Application.isPlaying)
                return null;
            var go = new GameObject("[Acoustics]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AcousticsHost>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            _sources = new AudioSource[VoiceCount];
            _filters = new AudioLowPassFilter[VoiceCount];
            _endTimes = new float[VoiceCount];
            for (var i = 0; i < VoiceCount; i++)
            {
                var child = new GameObject("Voice" + i);
                child.transform.SetParent(transform, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.dopplerLevel = 0f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.priority = 80;
                _sources[i] = s;
                _filters[i] = child.AddComponent<AudioLowPassFilter>();
            }
        }

        private void Update()
        {
            if (Queue.Count == 0)
                return;
            var now = Time.time;
            _due.Clear();
            Queue.PopDue(now, _due);
            for (var i = 0; i < _due.Count; i++)
                Play(_due[i], now);
        }

        private void Play(AcousticEvent e, float now)
        {
            var clip = GameAudio.GetClip(e.Id);
            if (clip == null)
                return;
            var slot = -1;
            var oldest = 0;
            for (var i = 0; i < VoiceCount; i++)
            {
                if (_endTimes[i] <= now) { slot = i; break; }
                if (_endTimes[i] < _endTimes[oldest]) oldest = i;
            }
            if (slot < 0)
                slot = oldest;
            var s = _sources[slot];
            s.Stop();
            // Gerçek mikser: patlama Darbe, atış/yankı/çatlak-vızıltı Silah grubuna (ikisi de Efekt'in altında; mixer yoksa no-op).
            MixerRouting.Route(s, e.Id == SoundId.Explosion ? MixChannel.Darbe : MixChannel.Silah);
            s.transform.position = e.Position;
            s.minDistance = e.MinDistance;
            s.maxDistance = e.MaxDistance;
            s.clip = clip;
            s.volume = Mathf.Clamp01(e.Volume);
            s.pitch = Mathf.Clamp(e.Pitch, 0.5f, 2f);
            var cut = Mathf.Clamp(e.CutoffHz, 200f, 22000f);
            _filters[slot].cutoffFrequency = cut;
            _filters[slot].enabled = cut < 20000f; // yakın/açık kaynak: süzgeç tamamen devre dışı.
            s.Play();
            _endTimes[slot] = now + clip.length / s.pitch + 0.05f;
        }
    }
}
