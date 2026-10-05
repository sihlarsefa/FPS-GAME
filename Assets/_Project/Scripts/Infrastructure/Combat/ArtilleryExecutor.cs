using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Topçu desteğinin altyapı uygulayıcısı. Otoritede her kare <see cref="ArtilleryService.DueImpacts"/>'i okur ve temizler;
    /// her mermi için hedefte gelen-mermi ıslığını çalar, ~1.2 sn sonra (küçük rastgele kaymayla) zemine oturtulmuş
    /// noktada <see cref="ExplosionSystem.Explode"/> çağırır (ShellRadius, ShellDamage, çağıran = saldırgan).
    /// </summary>
    public sealed class ArtilleryExecutor : MonoBehaviour
    {
        /// <summary>Islık ile patlama arasındaki süre (sn).</summary>
        public const float WhistleLeadSeconds = 1.2f;

        private const float MaxStagger = 0.35f;
        private const float GroundProbeHeight = 600f;
        private const float GroundProbeDistance = 1500f;

        private struct PendingShell
        {
            public Vector3 Position;
            public PlayerId CallerId;
            public int Team;
            public float ExplodeTime;
        }

        private readonly List<PendingShell> _pending = new(16);
        private ArtilleryService _artillery;
        private bool _loggedError;

        public static ArtilleryExecutor Instance { get; private set; }

        /// <summary>Islığı çalınmış, patlamayı bekleyen mermi sayısı.</summary>
        public int PendingCount => _pending.Count;

        public static ArtilleryExecutor Create(ArtilleryService artillery)
        {
            var executor = Instance;
            if (executor == null)
            {
                var go = new GameObject("ArtilleryExecutor");
                executor = go.AddComponent<ArtilleryExecutor>();
            }

            executor._artillery = artillery;
            return executor;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!GameContext.HasAuthority)
                return;

            if (_artillery == null)
                GameContext.TryGet(out _artillery);

            if (_artillery != null)
                CollectDueImpacts();

            ExplodeDueShells();
        }

        private void CollectDueImpacts()
        {
            List<ArtilleryImpact> due;
            try
            {
                due = _artillery.DueImpacts;
            }
            catch (Exception e)
            {
                LogOnce(e);
                return;
            }

            if (due == null || due.Count == 0)
                return;

            var now = Time.time;
            for (var i = 0; i < due.Count; i++)
            {
                var impact = due[i];
                var position = SnapToGround(CombatContext.ToVector3(impact.Position));
                var stagger = due.Count > 1 ? UnityEngine.Random.Range(0f, MaxStagger) : 0f;
                _pending.Add(new PendingShell
                {
                    Position = position,
                    CallerId = impact.CallerId,
                    Team = impact.Team,
                    ExplodeTime = now + WhistleLeadSeconds + stagger
                });

                try
                {
                    GameAudio.Play(SoundId.ArtilleryWhistle, position + Vector3.up * 25f, 1f,
                        UnityEngine.Random.Range(0.93f, 1.07f), 320f);
                }
                catch (Exception e)
                {
                    LogOnce(e);
                }
            }

            due.Clear();
        }

        private void ExplodeDueShells()
        {
            if (_pending.Count == 0)
                return;

            var now = Time.time;
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var shell = _pending[i];
                if (now < shell.ExplodeTime)
                    continue;

                _pending.RemoveAt(i);
                try
                {
                    ExplosionSystem.Explode(shell.Position, ArtilleryService.ShellRadius, ArtilleryService.ShellDamage,
                        shell.CallerId, DamageSourceIds.Artillery);
                }
                catch (Exception e)
                {
                    LogOnce(e);
                }
            }
        }

        private static Vector3 SnapToGround(Vector3 position)
        {
            var probe = new Vector3(position.x, Mathf.Max(position.y, 0f) + GroundProbeHeight, position.z);
            if (Physics.Raycast(probe, Vector3.down, out var hit, GroundProbeDistance, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.1f;

            return position;
        }

        private void LogOnce(Exception e)
        {
            if (_loggedError)
                return;

            _loggedError = true;
            Debug.LogException(e, this);
        }
    }
}
