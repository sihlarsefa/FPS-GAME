using System.Collections.Generic;
using Project.Application.Combat.Ballistics;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Infrastructure.Audio.Combat
{
    /// <summary>
    /// Yakın geçen mermi sesi (UnityHitScanner.NearPass, &lt;= 3 m). Süpersonik mermide keskin "crack" mermi yolundan hemen,
    /// namlu "thump"ı SupersonicSoundRules.CrackThumpGap kadar gecikmeyle atış noktasından; ses altı mermide yalnız
    /// alçak perdeli vizlama. Kurallar salt okunur kullanılır. Çalışma anında kendini kurar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletFlybyAudio : MonoBehaviour
    {
        /// <summary>Planlanan ses (saf veri; testlenebilir).</summary>
        public readonly struct Plan
        {
            public readonly bool Crack;
            public readonly float ThumpDelay;
            public readonly float Volume;
            public readonly float WhizPitch;

            public Plan(bool crack, float thumpDelay, float volume, float whizPitch)
            {
                Crack = crack; ThumpDelay = thumpDelay; Volume = volume; WhizPitch = whizPitch;
            }
        }

        private struct Pending { public SoundId Id; public Vector3 Pos; public float Due, Volume, Pitch, Range; }

        private static BulletFlybyAudio _instance;
        private readonly List<Pending> _queue = new List<Pending>(8);

        /// <summary>Yakın geçişten ses planı: crack var mı, thump gecikmesi, ses düzeyi (yakın = yüksek), vizlama perdesi.</summary>
        public static Plan PlanFor(BulletPassInfo info)
        {
            var crack = SupersonicSoundRules.HasCrack(info.MuzzleVelocity, info.Ammo, info.Along, info.MissDistance);
            var gap = crack ? SupersonicSoundRules.CrackThumpGap(info.MuzzleVelocity, info.Ammo, info.Along) : 0f;
            var k = Mathf.Clamp01(1f - info.MissDistance / UnityHitScanner.NearPassRadius);
            return new Plan(crack, gap, 0.35f + 0.65f * k, crack ? 1f : 0.7f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;
            var go = new GameObject("BulletFlybyAudio") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<BulletFlybyAudio>();
        }

        private void OnEnable() { UnityHitScanner.NearPass += OnNearPass; }
        private void OnDisable() { UnityHitScanner.NearPass -= OnNearPass; }
        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void OnNearPass(BulletPassInfo info)
        {
            var plan = PlanFor(info);
            var pitch = Random.Range(0.94f, 1.06f);
            var now = Time.time;
            if (plan.Crack)
            {
                GameAudio.Play(SoundId.BulletCrack, info.ClosestPoint, plan.Volume, pitch, 40f);
                if (plan.ThumpDelay > 0.02f && _queue.Count < 16)
                    _queue.Add(new Pending { Id = SoundId.ShotThump, Pos = info.Origin, Due = now + plan.ThumpDelay,
                        Volume = plan.Volume * 0.6f, Pitch = pitch, Range = 150f });
            }

            GameAudio.Play(SoundId.BulletWhiz, info.ClosestPoint, plan.Volume * (plan.Crack ? 0.6f : 1f),
                pitch * plan.WhizPitch, 25f);
        }

        private void Update()
        {
            for (var i = _queue.Count - 1; i >= 0; i--)
            {
                var p = _queue[i];
                if (Time.time < p.Due)
                    continue;
                GameAudio.Play(p.Id, p.Pos, p.Volume, p.Pitch, p.Range);
                _queue.RemoveAt(i);
            }
        }
    }
}
