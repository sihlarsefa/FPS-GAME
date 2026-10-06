using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Characters;
using Project.Infrastructure.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Project.Infrastructure.AI
{
    /// <summary>
    /// Rehine: silahsız sivil NPC. Esirken yerinde durur; oyuncu F'ye basınca <see cref="HostageState.Following"/> olur ve
    /// NavMesh üzerinde oyuncuyu izler. Vuruş kutuları olan bir <see cref="Combatant"/>'tır (düşman botlar onu hedef alabilir);
    /// ölümü <see cref="Died"/> olayıyla bildirilir. Durum yönetimi (kurallar) <see cref="HostageRules"/>'ta, burası yalnızca hareket.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HostageNpc : MonoBehaviour
    {
        private const float Radius = 0.35f;
        private const float Height = 1.8f;
        private const float WalkSpeed = 2.6f;
        private const float RunSpeed = 5.2f;
        private const float FollowDistance = 2.4f;

        private NavMeshAgent _agent;
        private Transform _target;
        private HostageState _state = HostageState.Captive;
        private bool _deathReported;
        private float _nextRepath;

        public Combatant Combatant { get; private set; }
        public SoldierModel Model { get; private set; }
        public HostageState State => _state;
        public bool IsDead => Combatant == null || !Combatant.IsAlive || Combatant.IsDowned;

        /// <summary>Rehine öldü (bir kez tetiklenir).</summary>
        public event Action<HostageNpc> Died;

        /// <summary>Sivil görünümlü rehine kurar.</summary>
        public static HostageNpc Create(Vector3 position, float yaw, PlayerId id, string displayName, int team, int seed, Transform parent)
        {
            var go = new GameObject("Rehine - " + displayName);
            go.layer = GameLayers.Bot;
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var npc = go.AddComponent<HostageNpc>();
            npc.Setup(id, displayName, team, seed);
            return npc;
        }

        /// <summary>Sivil kıyafet paleti: gri/kahve/lacivert tonları, tim kolluğu yok, bere yok.</summary>
        public static SoldierLook CivilianLook(int seed)
        {
            var rng = new System.Random(seed);
            var look = SoldierLook.ForTeam(3, rng);
            Color[][] outfits =
            {
                new[] { new Color(0.32f, 0.3f, 0.28f), new Color(0.28f, 0.26f, 0.24f), new Color(0.36f, 0.34f, 0.3f), new Color(0.2f, 0.19f, 0.18f) },
                new[] { new Color(0.2f, 0.25f, 0.34f), new Color(0.17f, 0.21f, 0.29f), new Color(0.24f, 0.29f, 0.38f), new Color(0.12f, 0.14f, 0.2f) },
                new[] { new Color(0.45f, 0.38f, 0.28f), new Color(0.4f, 0.33f, 0.24f), new Color(0.5f, 0.43f, 0.32f), new Color(0.3f, 0.25f, 0.18f) }
            };
            var o = outfits[Mathf.Abs(seed) % outfits.Length];
            look.CamoA = o[0];
            look.CamoB = o[1];
            look.CamoC = o[2];
            look.CamoD = o[3];
            look.Gear = o[3];
            look.Armband = new Color(0.8f, 0.8f, 0.78f);
            look.Beret = false;
            look.HasBeretColor = false;
            look.CamoSeed = 700 + Mathf.Abs(seed) % 50;
            look.PaletteIndex = 3;
            return look;
        }

        private void Setup(PlayerId id, string displayName, int team, int seed)
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.radius = Radius;
            col.height = Height;
            col.center = new Vector3(0f, Height * 0.5f, 0f);

            var eye = new GameObject("Eye").transform;
            eye.SetParent(transform, false);
            eye.localPosition = new Vector3(0f, 1.62f, 0f);
            var aim = new GameObject("AimPoint").transform;
            aim.SetParent(transform, false);
            aim.localPosition = new Vector3(0f, 1.62f * 0.76f, 0f);

            IEventBus bus = null;
            IDamageableRegistry registry = null;
            GameContext.TryGet(out bus);
            GameContext.TryGet(out registry);

            Combatant = gameObject.AddComponent<Combatant>();
            Combatant.Initialize(id, displayName, false, true, team, TeamRole.Rifleman, bus, registry, 100f);
            Combatant.EyePoint = eye;
            Combatant.AimPoint = aim;
            Combatant.DropLootOnDeath = false;
            Combatant.Stance = Stance.Standing;
            Combatant.DropState = DropState.Landed;

            try
            {
                Model = SoldierModel.Build(transform, CivilianLook(seed), Combatant, true, GameLayers.Bot);
                Model.AutoSyncWeapon = false; // silahsız
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }

            _agent = gameObject.AddComponent<NavMeshAgent>();
            _agent.radius = Radius;
            _agent.height = Height;
            _agent.baseOffset = 0f;
            _agent.speed = WalkSpeed;
            _agent.angularSpeed = 540f;
            _agent.acceleration = 12f;
            _agent.stoppingDistance = FollowDistance * 0.7f;
            _agent.updateRotation = false;
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            _agent.avoidancePriority = 60;

            if (NavMesh.SamplePosition(transform.position, out var hit, 4f, NavMesh.AllAreas))
                _agent.Warp(hit.position);

            Combatant.Died += OnCombatantDied;
        }

        /// <summary>Takip edilecek hedef (oyuncu).</summary>
        public void SetTarget(Transform target) => _target = target;

        /// <summary>Kural durumunu yansıtır (Captive/Following/Holding).</summary>
        public void SetState(HostageState state)
        {
            _state = state;
            if (_agent != null && _agent.isOnNavMesh && state != HostageState.Following)
                _agent.ResetPath();
        }

        /// <summary>Tahliye: rehineyi sahneden kaldırır (araca bindi).</summary>
        public void Evacuate()
        {
            _state = HostageState.Extracted;
            if (_agent != null)
                _agent.enabled = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_state == HostageState.Extracted)
                return;

            if (IsDead)
            {
                ReportDeath();
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                    _agent.ResetPath();
                return;
            }

            if (_agent == null || !_agent.isOnNavMesh)
                return;

            var velocity = Vector3.zero;
            if (_state == HostageState.Following && _target != null)
            {
                var to = _target.position - transform.position;
                to.y = 0f;
                var dist = to.magnitude;
                _agent.speed = dist > 7f ? RunSpeed : WalkSpeed;
                if (dist > FollowDistance && Time.time >= _nextRepath)
                {
                    _nextRepath = Time.time + 0.3f;
                    _agent.SetDestination(_target.position - to.normalized * (FollowDistance * 0.6f));
                }
                else if (dist <= FollowDistance && _agent.hasPath)
                {
                    _agent.ResetPath();
                }

                velocity = _agent.velocity;
                var face = velocity.sqrMagnitude > 0.2f ? velocity : to;
                if (face.sqrMagnitude > 0.01f)
                {
                    var rot = Quaternion.LookRotation(new Vector3(face.x, 0f, face.z));
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, 360f * Time.deltaTime);
                }
            }

            if (Model != null)
            {
                try { Model.SetLocomotion(velocity, Stance.Standing, true); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        private void OnCombatantDied(Combatant c, DamageInfo info) => ReportDeath();

        private void ReportDeath()
        {
            if (_deathReported)
                return;
            _deathReported = true;
            _state = HostageState.Dead;
            Died?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (Combatant != null)
                Combatant.Died -= OnCombatantDied;
        }
    }
}
