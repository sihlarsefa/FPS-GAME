using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Infrastructure.Support
{
    /// <summary>
    /// T-129 ATAK helikopteri (prosedürel): kenardan gelir, hedef bölgeyi 25 sn çevreler, görebildiği düşmanlara
    /// 20 mm topla (BallisticsSystem) ve iki füze salvosuyla (ExplosionSystem) ateş eder, sonra çekilir.
    /// Gövde canı 1500; düşürülürse döner ve düşer. Yalnızca otoritede çalışır.
    /// </summary>
    public sealed class SupportHelicopter : MonoBehaviour
    {
        public const string CannonId = "support_cannon_20mm";
        public const string RocketSourceId = "support_rocket";
        private const float Altitude = 60f;
        private const float OrbitRadius = 55f;
        private const float OrbitAngularSpeed = 0.28f;   // rad/sn
        private const float CruiseSpeed = 60f;
        private const float EngageRange = 170f;
        private const float ScanInterval = 0.5f;
        private const float BurstInterval = 2.6f;
        private const int BurstRounds = 12;
        private const float RoundInterval = 0.07f;
        private const float RocketSpeed = 130f;
        private const int RocketsPerSalvo = 4;
        private const float RocketInterval = 0.22f;
        private const float RocketRadius = 7f;
        private const float RocketDamage = 120f;

        private enum Phase { Inbound, Orbit, Outbound, Falling }

        private struct Rocket
        {
            public Transform Visual;
            public Vector3 Target;
            public float Speed;
        }

        private static WeaponDefinitionData _cannon;

        private static WeaponDefinitionData Cannon => _cannon ??= new WeaponDefinitionData(
            CannonId, WeaponCategory.Smg, 24f, 9999, RoundInterval, 0f, 700f, 1f)
        {
            DisplayName = "T-129 20 mm Top",
            AmmoType = AmmoType.Mm762,
            MuzzleVelocity = 900f,
            FalloffStart = 250f,
            FalloffEnd = 700f,
            MinDamageFactor = 0.7f,
            LimbMultiplier = 1f
        };

        private int _team;
        private PlayerId _caller;
        private Vector3 _center;
        private Phase _phase;
        private float _orbitAngle;
        private float _orbitElapsed;
        private float _nextScan;
        private float _nextBurst;
        private int _burstLeft;
        private float _nextRound;
        private Combatant _target;
        private int _salvosFired;
        private int _rocketsLeft;
        private float _nextRocket;
        private Vector3 _rocketAim;
        private Vector3 _exitDir;
        private float _fallTime;
        private float _fallSpin;
        private bool _finished;
        private SupportHeliHull _hull;
        private Transform _rotor;
        private Transform _tailRotor;
        private Transform _muzzle;
        private AudioSource _loop;
        private readonly List<Rocket> _rockets = new(8);

        public static SupportHelicopter Spawn(int team, PlayerId caller, Vector3 target)
        {
            var go = new GameObject("T129_Attack");
            var h = go.AddComponent<SupportHelicopter>();
            h._team = team;
            h._caller = caller;
            h._center = target;

            // Haritanın kenarından hedefe: rastgele yönden gelir.
            var ang = UnityEngine.Random.value * Mathf.PI * 2f;
            var from = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            h._orbitAngle = ang;
            h._exitDir = -from;
            go.transform.position = target + from * SupportAbilitySystem.SpawnDistance + Vector3.up * Altitude;
            h.BuildModel();
            h._phase = Phase.Inbound;
            h._nextBurst = 0f;
            try
            {
                h._loop = GameAudio.StartLoop(SoundId.HelicopterRotor, go.transform, 1f, true, 420f);
                if (h._loop != null) h._loop.dopplerLevel = 1.3f; // geçişte frekans kayması (ENTEGRASYON: GameAudio.cs havuz kaynağında doppler 0 sıfırlaması)
            }
            catch (Exception) { h._loop = null; }
            // Tehlike bölgesi işareti: hedefte duman (düşman/dost görür, uyarı).
            try { if (VfxQuality.AllowOptional(VfxQuality.Tier)) GameVfx.SmokeCloud(target, 3.5f, 9f); }
            catch (Exception) { }
            return h;
        }

        // ------------------------------------------------------------------ model

        private void BuildModel()
        {
            var body = MaterialLibrary.Lit(new Color(0.16f, 0.18f, 0.15f), 0.35f, 0.3f);
            var dark = MaterialLibrary.Lit(new Color(0.07f, 0.07f, 0.08f), 0.3f, 0.5f);
            var glass = MaterialLibrary.Lit(new Color(0.1f, 0.18f, 0.22f), 0.9f, 0.1f);

            Part(PrimitiveType.Capsule, "Fuselage", new Vector3(0f, 0f, 0.6f), new Vector3(1.4f, 3.2f, 1.6f), body, transform, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Sphere, "Canopy", new Vector3(0f, 0.45f, 2.6f), new Vector3(0.9f, 0.7f, 1.6f), glass, transform, Vector3.zero);
            Part(PrimitiveType.Cube, "Tail", new Vector3(0f, 0.35f, -4.2f), new Vector3(0.35f, 0.45f, 5.2f), body, transform, Vector3.zero);
            Part(PrimitiveType.Cube, "Fin", new Vector3(0f, 1.0f, -6.5f), new Vector3(0.12f, 1.3f, 0.9f), body, transform, Vector3.zero);
            Part(PrimitiveType.Cube, "StubWingL", new Vector3(-1.5f, -0.1f, 0.3f), new Vector3(1.8f, 0.12f, 0.9f), dark, transform, Vector3.zero);
            Part(PrimitiveType.Cube, "StubWingR", new Vector3(1.5f, -0.1f, 0.3f), new Vector3(1.8f, 0.12f, 0.9f), dark, transform, Vector3.zero);
            Part(PrimitiveType.Cylinder, "RocketPodL", new Vector3(-2.1f, -0.2f, 0.4f), new Vector3(0.35f, 0.7f, 0.35f), dark, transform, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "RocketPodR", new Vector3(2.1f, -0.2f, 0.4f), new Vector3(0.35f, 0.7f, 0.35f), dark, transform, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "Mast", new Vector3(0f, 1.0f, 0.4f), new Vector3(0.18f, 0.35f, 0.18f), dark, transform, Vector3.zero);

            var gun = Part(PrimitiveType.Cylinder, "Cannon", new Vector3(0f, -0.75f, 2.5f), new Vector3(0.12f, 0.6f, 0.12f), dark, transform, new Vector3(90f, 0f, 0f));
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(gun, false);
            _muzzle.localPosition = new Vector3(0f, 1.05f, 0f);
            _muzzle.SetParent(transform, true);

            _rotor = new GameObject("Rotor").transform;
            _rotor.SetParent(transform, false);
            _rotor.localPosition = new Vector3(0f, 1.4f, 0.4f);
            for (var i = 0; i < 4; i++)
                Part(PrimitiveType.Cube, "Blade" + i, Vector3.zero, new Vector3(0.28f, 0.04f, 7.2f), dark, _rotor, new Vector3(0f, i * 45f, 0f));

            _tailRotor = new GameObject("TailRotor").transform;
            _tailRotor.SetParent(transform, false);
            _tailRotor.localPosition = new Vector3(0.3f, 1.0f, -6.6f);
            Part(PrimitiveType.Cube, "TBlade", Vector3.zero, new Vector3(0.05f, 1.6f, 0.2f), dark, _tailRotor, Vector3.zero);
            Part(PrimitiveType.Cube, "TBlade2", Vector3.zero, new Vector3(0.05f, 0.2f, 1.6f), dark, _tailRotor, Vector3.zero);

            // Mermi isabet gövdesi (Default katman, katı).
            var hullGo = new GameObject("Hull");
            hullGo.transform.SetParent(transform, false);
            hullGo.layer = 0;
            var box = hullGo.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.2f, -0.6f);
            box.size = new Vector3(2.6f, 2.0f, 8.5f);
            _hull = hullGo.AddComponent<SupportHeliHull>();
            _hull.Team = _team;
            _hull.Destroyed += OnHullDestroyed;
        }

        private static Transform Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent, Vector3 euler)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = name;
            var col = p.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = p.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = Quaternion.Euler(euler);
            p.transform.localScale = scale;
            return p.transform;
        }

        // ------------------------------------------------------------------ yaşam döngüsü

        private void Update()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_rotor != null) _rotor.Rotate(0f, 1500f * dt, 0f, Space.Self);
            if (_tailRotor != null) _tailRotor.Rotate(1800f * dt, 0f, 0f, Space.Self);

            UpdateRockets(dt);
            UpdateRotorWash(dt);

            switch (_phase)
            {
                case Phase.Inbound: UpdateInbound(dt); break;
                case Phase.Orbit: UpdateOrbit(dt); break;
                case Phase.Outbound: UpdateOutbound(dt); break;
                case Phase.Falling: UpdateFalling(dt); break;
            }
        }

        private float _washTimer;
        private bool _washFailed;

        /// <summary>Zemine 15 m'den yakınken (düşerken vb.) rotor akışı; throttled.</summary>
        private void UpdateRotorWash(float dt)
        {
            if (_washFailed)
                return;
            _washTimer -= dt;
            if (_washTimer > 0f)
                return;
            _washTimer = 0.5f;
            try
            {
                var pos = transform.position;
                if (Physics.Raycast(pos, Vector3.down, out var hit, 15f, ~0, QueryTriggerInteraction.Ignore))
                    GameVfx.RotorWash(hit.point + Vector3.up * 0.1f, 7f);
            }
            catch (Exception e)
            {
                _washFailed = true;
                Debug.LogException(e, this);
            }
        }

        private Vector3 OrbitPos(float angle)
        {
            SupportAbilityService.OrbitPoint(_center.x, _center.z, OrbitRadius, angle, out var x, out var z);
            return new Vector3(x, _center.y + Altitude, z);
        }

        private void UpdateInbound(float dt)
        {
            var goal = OrbitPos(_orbitAngle);
            MoveToward(goal, CruiseSpeed, dt);
            if ((goal - transform.position).sqrMagnitude < 25f)
            {
                _phase = Phase.Orbit;
                _nextBurst = Time.time + 1f;
            }
        }

        private void UpdateOrbit(float dt)
        {
            _orbitElapsed += dt;
            _orbitAngle += OrbitAngularSpeed * dt;
            var goal = OrbitPos(_orbitAngle);
            MoveToward(goal, CruiseSpeed * 1.4f, dt, _target != null ? _target.transform.position : _center);

            if (_orbitElapsed >= SupportAbilityService.OrbitSeconds)
            {
                _phase = Phase.Outbound;
                SupportAbilitySystem.Finished(_team, false, transform.position);
                _finished = true;
                return;
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                if (_target == null || !IsValidTarget(_target)) _target = FindTarget();
            }

            UpdateCannon();
            UpdateSalvos();
        }

        private void UpdateOutbound(float dt)
        {
            var goal = transform.position + _exitDir * 200f + Vector3.up * 10f;
            MoveToward(goal, CruiseSpeed * 1.5f, dt);
            if ((transform.position - _center).sqrMagnitude > SupportSqr(SupportAbilitySystem.SpawnDistance * 0.9f))
                Destroy(gameObject);
        }

        private static float SupportSqr(float v) => v * v;

        private void MoveToward(Vector3 goal, float speed, float dt, Vector3? lookAt = null)
        {
            var pos = transform.position;
            var to = goal - pos;
            var step = speed * dt;
            transform.position = to.magnitude <= step ? goal : pos + to.normalized * step;

            var look = lookAt.HasValue ? lookAt.Value - pos : to;
            look.y = 0f;
            if (look.sqrMagnitude > 0.5f)
            {
                var desired = Quaternion.LookRotation(look.normalized, Vector3.up);
                // Hafif yatış (viraj hissi).
                desired *= Quaternion.Euler(6f, 0f, _phase == Phase.Orbit ? -12f : 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, 1.8f * dt);
            }
        }

        // ------------------------------------------------------------------ hedefleme

        private bool IsValidTarget(Combatant c)
        {
            if (c == null || !c.IsAlive || !c.IsTargetable || c.Team == _team || c.Team < 0)
                return false;
            var d = c.transform.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= EngageRange * EngageRange;
        }

        private Combatant FindTarget()
        {
            var all = CombatantRegistry.All;
            var origin = transform.position;
            Combatant best = null;
            var bestSqr = float.MaxValue;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!IsValidTarget(c)) continue;
                var aim = c.GetAimPosition(BodyPart.Torso);
                var sqr = (c.transform.position - _center).sqrMagnitude; // yörünge bölgesine yakın olana öncelik
                if (sqr >= bestSqr) continue;
                if (Physics.Linecast(origin, aim, out var hit, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore)
                    && !hit.collider.transform.IsChildOf(transform))
                    continue;
                best = c;
                bestSqr = sqr;
            }

            return best;
        }

        // ------------------------------------------------------------------ top

        private void UpdateCannon()
        {
            if (_target == null || !IsValidTarget(_target))
            {
                _burstLeft = 0;
                return;
            }

            var now = Time.time;
            if (_burstLeft <= 0)
            {
                if (now < _nextBurst) return;
                _burstLeft = BurstRounds;
                _nextBurst = now + BurstInterval;
                _nextRound = now;
            }

            if (now < _nextRound) return;
            _nextRound = now + RoundInterval;
            _burstLeft--;

            var muzzle = _muzzle != null ? _muzzle.position : transform.position;
            var aim = _target.GetAimPosition(BodyPart.Torso) + _target.Velocity * (Vector3.Distance(muzzle, _target.transform.position) / 900f);
            var dir = BallisticsSystem.ApplySpread((aim - muzzle).normalized, 1.6f);
            try
            {
                BallisticsSystem.GetOrCreate().SpawnProjectile(_caller, Cannon, muzzle, dir, muzzle, true, transform);
                GameAudio.Play(SoundId.ShotMachineGun, muzzle, 1f, 0.6f, 600f);
                CannonVisuals(muzzle, dir);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                _burstLeft = 0;
            }
        }

        private int _roundCounter;

        /// <summary>Namlu alevi (gece bulutları aydınlatır), kovan yağmuru, zemine düşen 20 mm izi (toz hattı). Katman sınırlı.</summary>
        private void CannonVisuals(Vector3 muzzle, Vector3 dir)
        {
            _roundCounter++;
            var tier = VfxQuality.Tier;
            var step = VfxQuality.AllowOptional(tier) ? 1 : 3;
            if (_roundCounter % step != 0) return;
            GameVfx.MuzzleFlash(muzzle, dir, 2.2f, WeaponCategory.Smg, false);
            GameVfx.EjectShell(muzzle, -transform.up + transform.right * 0.5f, WeaponCategory.Smg);
            if (_roundCounter % (step * 2) != 0) return;
            if (Physics.Raycast(muzzle, dir, out var hit, 400f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                GameVfx.Impact(hit.point, hit.normal, SurfaceKind.Dirt);
                GameVfx.Dust(hit.point, 1.4f);
            }
        }

        // ------------------------------------------------------------------ füze

        private void UpdateSalvos()
        {
            var now = _orbitElapsed;
            if (_rocketsLeft > 0)
            {
                if (Time.time >= _nextRocket)
                {
                    _nextRocket = Time.time + RocketInterval;
                    _rocketsLeft--;
                    LaunchRocket();
                }

                return;
            }

            if (_salvosFired >= SupportAbilityService.RocketSalvos) return;
            var due = SupportAbilityService.SalvoTime(_salvosFired, SupportAbilityService.OrbitSeconds);
            if (due < 0f || now < due) return;
            if (_target == null || !IsValidTarget(_target)) return; // hedef yoksa salvo bekler (süre bitene dek)

            _salvosFired++;
            _rocketsLeft = RocketsPerSalvo;
            _nextRocket = Time.time;
            _rocketAim = _target.transform.position;
        }

        private void LaunchRocket()
        {
            var side = (_rocketsLeft % 2 == 0) ? -1f : 1f;
            var start = transform.TransformPoint(new Vector3(2.1f * side, -0.2f, 1.2f));
            var jitter = UnityEngine.Random.insideUnitCircle * 6f;
            var aim = _rocketAim + new Vector3(jitter.x, 0f, jitter.y);
            if (Physics.Raycast(aim + Vector3.up * 80f, Vector3.down, out var hit, 200f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                aim = hit.point;

            var vis = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            vis.name = "T129_Rocket";
            var col = vis.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = vis.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = MaterialLibrary.Lit(new Color(1f, 0.55f, 0.15f), 0.2f, 0f);
            vis.transform.position = start;
            vis.transform.localScale = new Vector3(0.18f, 0.5f, 0.18f);
            vis.transform.rotation = Quaternion.LookRotation((aim - start).normalized) * Quaternion.Euler(90f, 0f, 0f);

            AddRocketTrail(vis);
            try
            {
                if (VfxQuality.AllowOptional(VfxQuality.Tier)) GameVfx.SmokeCloud(start - transform.forward * 1.5f, 1.6f, 2.5f); // arka alev dumanı
                GameVfx.MuzzleFlash(start, (aim - start).normalized, 1.6f, WeaponCategory.Smg, false);
            }
            catch (Exception) { }

            _rockets.Add(new Rocket { Visual = vis.transform, Target = aim, Speed = RocketSpeed });
            try { GameAudio.Play(SoundId.GrenadePin, start, 0.9f, 0.4f, 400f); }
            catch (Exception) { }
        }

        private static Material _trailMat;

        private static void AddRocketTrail(GameObject vis)
        {
            try
            {
                if (_trailMat == null)
                {
                    var sh = Shader.Find("Sprites/Default");
                    if (sh == null) return;
                    _trailMat = new Material(sh) { color = new Color(0.82f, 0.82f, 0.8f, 0.55f) };
                }

                var tr = vis.AddComponent<TrailRenderer>();
                tr.sharedMaterial = _trailMat;
                tr.time = VfxQuality.AllowOptional(VfxQuality.Tier) ? 1.4f : 0.6f;
                tr.startWidth = 0.35f;
                tr.endWidth = 1.1f;
                tr.minVertexDistance = 0.8f;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.startColor = new Color(1f, 0.85f, 0.5f, 0.8f);
                tr.endColor = new Color(0.6f, 0.6f, 0.6f, 0f);
            }
            catch (Exception) { }
        }

        private void UpdateRockets(float dt)
        {
            for (var i = _rockets.Count - 1; i >= 0; i--)
            {
                var r = _rockets[i];
                if (r.Visual == null) { _rockets.RemoveAt(i); continue; }

                var pos = r.Visual.position;
                var to = r.Target - pos;
                var step = r.Speed * dt;
                if (to.magnitude <= step)
                {
                    var point = r.Target;
                    Destroy(r.Visual.gameObject);
                    _rockets.RemoveAt(i);
                    try
                    {
                        ExplosionSystem.Explode(point, RocketRadius, RocketDamage, _caller, RocketSourceId);
                        if (VfxQuality.AllowOptional(VfxQuality.Tier)) GameVfx.SmokeCloud(point, 4f, 8f); // kalıcı duman
                    }
                    catch (Exception e) { Debug.LogException(e, this); }
                    continue;
                }

                r.Visual.position = pos + to.normalized * step;
                r.Visual.rotation = Quaternion.LookRotation(to.normalized) * Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // ------------------------------------------------------------------ düşürülme

        private void OnHullDestroyed(SupportHeliHull hull)
        {
            if (_phase == Phase.Falling) return;
            _phase = Phase.Falling;
            _fallSpin = 220f;
            if (!_finished)
            {
                _finished = true;
                SupportAbilitySystem.Finished(_team, true, transform.position);
            }

            try { ExplosionSystem.Explode(transform.position, 9f, 60f, hull.Killer, RocketSourceId); }
            catch (Exception) { }
        }

        private void UpdateFalling(float dt)
        {
            _fallTime += dt;
            _fallSpin += 140f * dt;
            transform.Rotate(0f, _fallSpin * dt, 0f, Space.World);
            var fallSpeed = 8f + _fallTime * 22f;
            transform.position += Vector3.down * (fallSpeed * dt);

            var grounded = Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out var hit, fallSpeed * dt + 6f,
                GameLayers.GroundMask, QueryTriggerInteraction.Ignore);
            if (grounded || _fallTime > 8f)
            {
                var point = grounded ? hit.point : transform.position;
                try { ExplosionSystem.Explode(point, 14f, 150f, _hull != null ? _hull.Killer : PlayerId.Invalid, RocketSourceId); }
                catch (Exception e) { Debug.LogException(e, this); }
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            for (var i = 0; i < _rockets.Count; i++)
                if (_rockets[i].Visual != null) Destroy(_rockets[i].Visual.gameObject);
            _rockets.Clear();
            if (_loop != null) { _loop.dopplerLevel = 0f; _loop.Stop(); }
            if (!_finished)
            {
                _finished = true;
                SupportAbilitySystem.Finished(_team, false, transform.position);
            }
        }
    }
}
