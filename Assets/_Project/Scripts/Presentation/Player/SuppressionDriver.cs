using Project.Infrastructure.AI;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.HdrMix;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Rendering;
using Project.Presentation.Bootstrap;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Bastırma + düşük can + sersemletme girdilerini her karede <see cref="CombatScreenFx"/>'e yazar ve küçük kamera
    /// sarsıntısı verir. Çalışma anında kendini kurar (sahne kurulumu gerekmez); yerel oyuncu yoksa sıfırlar.
    /// Yoğunluk ayarı: <c>GameSettings.CameraShakeIntensity</c> (0..1,5).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuppressionDriver : MonoBehaviour
    {
        private static SuppressionDriver _instance;
        private float _time;
        private float _shakeCooldown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;
            var go = new GameObject("SuppressionDriver") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SuppressionDriver>();
        }

        private void OnEnable()
        {
            Acoustics.NearMiss += OnNearMiss;
            UnityHitScanner.NearPass += OnNearPass;
            BotController.IncomingFireAtLocalPlayer += OnIncomingFire;
            ExplosionSystem.Exploded += OnExploded;
        }

        private void OnDisable()
        {
            Acoustics.NearMiss -= OnNearMiss;
            UnityHitScanner.NearPass -= OnNearPass;
            MixerRouting.SetMuffle(0f);
            BotController.IncomingFireAtLocalPlayer -= OnIncomingFire;
            ExplosionSystem.Exploded -= OnExploded;
        }

        private static bool LocalAlive(out PlayerController player)
        {
            player = PlayerController.Local;
            return player != null && player.Combatant != null && player.Combatant.IsAlive;
        }

        private static void OnNearMiss(Vector3 point, float caliberMm, float distance)
        {
            if (LocalAlive(out _))
                Suppression.ReportNearMiss(point, caliberMm, distance);
        }

        private static void OnNearPass(BulletPassInfo info)
        {
            if (LocalAlive(out _))
                Suppression.ReportNearMiss(info.ClosestPoint, 5.56f, info.MissDistance);
        }

        private static void OnIncomingFire(float distance, float caliberMm)
        {
            if (LocalAlive(out _))
                Suppression.ReportIncomingFire(distance, caliberMm);
        }

        private static void OnExploded(Vector3 point, float radius)
        {
            if (LocalAlive(out var player))
                Suppression.ReportExplosion(point, radius, Vector3.Distance(player.transform.position, point));
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void LateUpdate()
        {
            var dt = Mathf.Min(Time.deltaTime, 0.1f);
            var player = PlayerController.Local;
            var combatant = player != null ? player.Combatant : null;
            if (player == null || combatant == null || !combatant.IsAlive)
            {
                CombatScreenFx.Downed01 = 0f;
                if (Suppression.Value > 0f || Suppression.Concussion > 0f || CombatScreenFx.Instance != null)
                {
                    Suppression.Clear();
                    MixerRouting.SetMuffle(0f);
                }
                return;
            }

            CombatScreenFx.EnsureInstalled();
            Suppression.Tick(dt);
            MixerRouting.SetMuffle(Suppression.MuffleAmount);
            _time += dt;

            CombatScreenFx.Intensity = ReadIntensity();
            var low = CombatScreenFxMath.LowHealth01(combatant.State.Normalized);
            CombatScreenFx.Suppression01 = Suppression.Value;
            CombatScreenFx.LowHealth01 = low;
            CombatScreenFx.Pulse01 = CombatScreenFxMath.HeartPulse(_time, low);
            CombatScreenFx.Concussion01 = Suppression.Concussion;
            CombatScreenFx.Tinnitus01 = ReadTinnitus();
            CombatScreenFx.Downed01 = combatant.IsDowned ? 1f : 0f;

            _shakeCooldown -= dt;
            var shake = CombatScreenFxMath.ShakeFor(Suppression.Value);
            if (shake > 0.01f && _shakeCooldown <= 0f && player.CameraController != null)
            {
                player.CameraController.Shake(shake, 0.2f);
                _shakeCooldown = 0.15f;
            }
        }

        private static float ReadIntensity()
        {
            try
            {
                var s = GameSession.Settings != null ? GameSession.Settings.Current : null;
                if (s != null)
                    return Mathf.Clamp(s.CameraShakeIntensity, 0f, 1.5f);
            }
            catch (System.Exception) { }

            return 1f;
        }

        /// <summary>Kulak çınlaması anlık görüntüsü (yalnız okuma): 0..1.</summary>
        private static float ReadTinnitus()
        {
            try { return Mathf.Clamp01(AudioMix.State.Current.TinnitusGain); }
            catch (System.Exception) { return 0f; }
        }
    }
}
