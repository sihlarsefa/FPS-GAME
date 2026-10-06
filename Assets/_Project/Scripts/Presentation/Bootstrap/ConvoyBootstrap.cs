using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.AI;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.Vfx;
using Project.Infrastructure.World;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// "KONVOY KORUMA" modu: Kuzgun Vadisi'nde 2 tim × 10. Savunma (tim 0, oyuncu) 3 ikmal kamyonunu Ana Yol kesitinde
    /// güzergâh sonuna götürür; saldırı (tim 1) yol kenarında pusu kurar. Kural mantığı <see cref="ConvoyRules"/>'ta.
    /// Çatışma altyapısı (<see cref="SkirmishBootstrap"/>: doğuş, takviye dalgası, maç sonu) yeniden kullanılır; bu bileşen
    /// onun Start'ında eklenir ve yalnızca kamyonları, kuralları ve arayüzü ekler. Menüye
    /// <see cref="MainMenuController.ExtraButtons"/> ile kaydolur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConvoyBootstrap : MonoBehaviour
    {
        private const int TruckIdOffset = 5000;
        private const float RouteFrom = 0.30f;
        private const float RouteTo = 0.70f;
        private const float AmbushAlong = 0.58f;
        private const float AmbushLateral = 22f;

        // ------------------------------------------------------------------ Mod bayrağı ve menü

        /// <summary>Çatışma altyapısı bu oturumda konvoy kurallarıyla mı çalışsın?</summary>
        public static bool Active { get; private set; }

        private static readonly List<float> RouteX = new List<float>();
        private static readonly List<float> RouteZ = new List<float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            const string label = "KONVOY KORUMA";
            var list = MainMenuController.ExtraButtons;
            for (var i = 0; i < list.Count; i++)
                if (list[i].label == label)
                    return;
            list.Add((label, _ => Begin()));
        }

        /// <summary>Konvoy Koruma maçını başlatır (Çatışma sahnesi + konvoy bayrağı).</summary>
        public static void Begin()
        {
            GameSession.StartSkirmish(); // bayrağı sıfırlar
            Active = true;
        }

        /// <summary>Çatışma modu normal başlatıldığında çağrılır.</summary>
        public static void Reset() => Active = false;

        /// <summary>
        /// Yol kesitinden güzergâhı çıkarır; savunma üssünü başlangıca, saldırı üssünü yol kenarındaki pusu noktasına koyar.
        /// Yol bulunamazsa false döner (rastgele bölge seçimi sürer, kamyonlar kurulmaz).
        /// </summary>
        public static bool TryPlaceBases(WorldMetadata world, Vector3[] bases, float[] yaw)
        {
            RouteX.Clear();
            RouteZ.Clear();
            var layout = (world != null ? world.Layout : null) ?? WorldGenerator.LastLayout;
            if (layout == null || layout.Roads == null || bases == null || yaw == null || bases.Length < 2 || yaw.Length < 2)
                return false;

            RoadSpec best = null;
            var bestLength = 0f;
            for (var i = 0; i < layout.Roads.Count; i++)
            {
                var road = layout.Roads[i];
                if (road == null || road.Points == null || road.Points.Count < 2)
                    continue;

                var xs = new List<float>(road.Points.Count);
                var zs = new List<float>(road.Points.Count);
                for (var p = 0; p < road.Points.Count; p++)
                {
                    xs.Add(road.Points[p].x);
                    zs.Add(road.Points[p].y);
                }

                var length = ConvoyRoute.Length(xs, zs);
                var main = road.Name == "Ana Yol";
                if (main || length > bestLength)
                {
                    best = road;
                    bestLength = length;
                }

                if (main)
                    break;
            }

            if (best == null)
                return false;

            var bx = new List<float>();
            var bz = new List<float>();
            for (var p = 0; p < best.Points.Count; p++)
            {
                bx.Add(best.Points[p].x);
                bz.Add(best.Points[p].y);
            }

            ConvoyRoute.Slice(bx, bz, RouteFrom, RouteTo, RouteX, RouteZ);
            var routeLength = ConvoyRoute.Length(RouteX, RouteZ);
            if (RouteX.Count < 2 || routeLength < 60f)
            {
                RouteX.Clear();
                RouteZ.Clear();
                return false;
            }

            ConvoyRoute.Sample(RouteX, RouteZ, 0f, out var sx, out var sz, out var startYaw);
            var forward = new Vector3(Mathf.Sin(startYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(startYaw * Mathf.Deg2Rad));
            var start = BootstrapUtility.GroundPoint(world, new Vector3(sx, 0f, sz));
            bases[0] = BootstrapUtility.GroundPoint(world, start - forward * 14f);
            yaw[0] = startYaw;

            ConvoyRoute.Sample(RouteX, RouteZ, routeLength * AmbushAlong, out var ax, out var az, out var ayaw);
            var roadForward = new Vector3(Mathf.Sin(ayaw * Mathf.Deg2Rad), 0f, Mathf.Cos(ayaw * Mathf.Deg2Rad));
            var side = new Vector3(roadForward.z, 0f, -roadForward.x);
            var ambush = new Vector3(ax, 0f, az) + side * AmbushLateral;
            bases[1] = BootstrapUtility.GroundPoint(world, ambush);
            yaw[1] = Mathf.Atan2(-side.x, -side.z) * Mathf.Rad2Deg; // yola bak
            return true;
        }

        // ------------------------------------------------------------------ Örnek

        private SkirmishBootstrap _skirmish;
        private MatchService _match;
        private IEventBus _eventBus;
        private IDamageableRegistry _registry;
        private WorldMetadata _world;
        private ConvoyRules _rules;
        private readonly List<ConvoyTruck> _trucks = new List<ConvoyTruck>();
        private Transform _root;
        private float _routeLength;
        private bool _ready;
        private bool _finished;
        private Action<ExplosionEvent> _onExplosion;
        private Text _title;
        private Text _status;
        private Text _trucksText;
        private string _lastTitle, _lastStatus, _lastTrucks;

        private void Start()
        {
            if (!Active)
            {
                enabled = false;
                return;
            }

            _skirmish = GetComponent<SkirmishBootstrap>();
            var services = GameContext.Services;
            if (_skirmish == null || services == null || RouteX.Count < 2)
            {
                enabled = false;
                return;
            }

            services.TryResolve(out _match);
            services.TryResolve(out _eventBus);
            services.TryResolve(out _registry);
            _world = WorldMetadata.Instance != null ? WorldMetadata.Instance : FindAnyObjectByType<WorldMetadata>();
            _rules = new ConvoyRules(ConvoyRules.TruckCount, ConvoyRules.DefaultDurationSeconds);
            _routeLength = ConvoyRoute.Length(RouteX, RouteZ);
            _root = new GameObject("[Konvoy]").transform;

            BootstrapUtility.Try(SpawnTrucks, "Konvoy: kamyonlar");
            BootstrapUtility.Try(BuildHud, "Konvoy: arayüz");
            if (_eventBus != null)
            {
                _onExplosion = OnExplosion;
                _eventBus.Subscribe(_onExplosion);
            }

            _ready = true;
        }

        private void OnDestroy()
        {
            if (_eventBus != null && _onExplosion != null)
                _eventBus.Unsubscribe(_onExplosion);
            for (var i = 0; i < _trucks.Count; i++)
                if (_trucks[i] != null && _registry != null)
                    _registry.Unregister(_trucks[i]);
            if (_root != null)
                Destroy(_root.gameObject);
        }

        private void Update()
        {
            if (!_ready || _finished)
                return;

            var dt = Time.deltaTime;
            var inMatch = _match != null && _match.CurrentPhase == MatchPhase.InMatch;
            if (inMatch)
            {
                _rules.Tick(dt);
                ApplyAmbushFire(dt);
                for (var i = 0; i < _trucks.Count; i++)
                    AdvanceTruck(_trucks[i], dt);

                _rules.NotifyDefendersAlive(CountDefendersAlive());
            }

            if (_rules.IsOver)
            {
                _finished = true;
                var rules = _skirmish != null ? _skirmish.Rules : null;
                if (rules != null)
                    rules.ForceFinish(_rules.WinnerTeam);
            }

            RefreshHud();
        }

        // ------------------------------------------------------------------ Kamyonlar

        private void SpawnTrucks()
        {
            for (var i = 0; i < _rules.Trucks; i++)
            {
                var truck = ConvoyTruck.Create(_root, i, new PlayerId(TruckIdOffset + i), ConvoyRules.TruckMaxHp, TeamOf);
                truck.Distance = (_rules.Trucks - 1 - i) * ConvoyRules.TruckSpacing + 4f;
                Place(truck, 1f);
                _registry?.Register(truck);
                _trucks.Add(truck);
            }
        }

        private int TeamOf(PlayerId id) => _match != null ? _match.GetTeam(id) : -1;

        private void AdvanceTruck(ConvoyTruck truck, float dt)
        {
            if (truck == null || truck.Finished)
                return;

            if (!truck.IsAlive)
            {
                truck.Finished = true;
                _rules.MarkDestroyed(truck.Index);
                truck.Wreck();
                _registry?.Unregister(truck);
                BootstrapUtility.Try(() => GameVfx.Explosion(truck.transform.position + Vector3.up, 4f), "Konvoy: patlama");
                return;
            }

            truck.Distance += ConvoyRules.TruckSpeed * dt;
            if (truck.Distance >= _routeLength)
            {
                truck.Distance = _routeLength;
                truck.Finished = true;
                truck.Arrived = true;
                _rules.MarkArrived(truck.Index);
            }

            Place(truck, Mathf.Clamp01(dt * 6f));
        }

        private void Place(ConvoyTruck truck, float turn)
        {
            if (!ConvoyRoute.Sample(RouteX, RouteZ, truck.Distance, out var x, out var z, out var yaw))
                return;

            var ground = BootstrapUtility.GroundPoint(_world, new Vector3(x, 0f, z));
            var t = truck.transform;
            t.position = ground + Vector3.up * 0.05f;
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, yaw, 0f), turn);
        }

        /// <summary>Pusudaki saldırgan botlar kamyonlara soyut ateş açar (botlar kamyonu hedef olarak tanımaz).</summary>
        private void ApplyAmbushFire(float dt)
        {
            var bots = BotController.All;
            for (var t = 0; t < _trucks.Count; t++)
            {
                var truck = _trucks[t];
                if (truck == null || truck.Finished || !truck.IsAlive)
                    continue;

                var count = 0;
                var origin = truck.transform.position;
                var range2 = ConvoyRules.AmbushRange * ConvoyRules.AmbushRange;
                for (var i = 0; i < bots.Count; i++)
                {
                    var bot = bots[i];
                    if (bot == null || bot.Team != ConvoyRules.AttackerTeam || bot.Combatant == null || !bot.Combatant.IsAlive)
                        continue;
                    if ((bot.transform.position - origin).sqrMagnitude <= range2)
                        count++;
                }

                var dmg = ConvoyRules.AmbushDamage(count, dt);
                if (dmg > 0f)
                    truck.Damage(dmg);
            }
        }

        private void OnExplosion(ExplosionEvent e)
        {
            var center = new Vector3(e.Position.X, e.Position.Y, e.Position.Z);
            var attackerTeam = TeamOf(e.AttackerId);
            for (var i = 0; i < _trucks.Count; i++)
            {
                var truck = _trucks[i];
                if (truck == null || truck.Finished || !truck.IsAlive)
                    continue;

                var d = Vector3.Distance(center, truck.transform.position);
                if (d > e.Radius + 2f)
                    continue;

                var raw = 400f * Mathf.Clamp01(1f - d / (e.Radius + 2f));
                truck.Damage(ConvoyRules.TruckDamage(raw, attackerTeam));
            }
        }

        private int CountDefendersAlive()
        {
            var alive = 0;
            var bots = BotController.All;
            for (var i = 0; i < bots.Count; i++)
            {
                var bot = bots[i];
                if (bot != null && bot.Team == ConvoyRules.DefenderTeam && bot.Combatant != null && bot.Combatant.IsAlive)
                    alive++;
            }

            var player = _skirmish != null ? _skirmish.Player : null;
            if (player != null && player.Combatant != null && player.Combatant.IsAlive)
                alive++;
            return alive;
        }

        // ------------------------------------------------------------------ Arayüz

        private void BuildHud()
        {
            var canvas = UiFactory.CreateCanvas("ConvoyCanvas", 60);
            canvas.transform.SetParent(_root, false);

            var panel = UiFactory.Panel(canvas.transform, UiTheme.PanelDark);
            UiFactory.Anchor(panel, UiAnchor.Top, new Vector2(0f, -14f), new Vector2(640f, 96f));

            _title = UiFactory.Label(panel, "", UiTheme.FontLarge, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_title, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(6f, 0f), new Vector2(-6f, -4f));
            _trucksText = UiFactory.Label(panel, "", UiTheme.FontMedium, TextAnchor.MiddleCenter, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(_trucksText, new Vector2(0f, 0.22f), new Vector2(1f, 0.55f), new Vector2(6f, 0f), new Vector2(-6f, 0f));
            _status = UiFactory.Label(panel, "", UiTheme.FontTiny, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiFactory.SetRect(_status, new Vector2(0f, 0f), new Vector2(1f, 0.25f), new Vector2(6f, 2f), new Vector2(-6f, 0f));
        }

        private void RefreshHud()
        {
            if (_title == null || _rules == null)
                return;

            var t = Mathf.CeilToInt(_rules.TimeRemaining);
            var title = "KONVOY KORUMA   " + (t / 60).ToString("00") + ":" + (t % 60).ToString("00");

            var sb = new System.Text.StringBuilder(64);
            for (var i = 0; i < _trucks.Count; i++)
            {
                if (i > 0)
                    sb.Append("   ");
                var truck = _trucks[i];
                sb.Append("KAMYON ").Append(i + 1).Append(": ");
                if (truck == null || _rules.GetState(i) == ConvoyRules.TruckState.Destroyed)
                    sb.Append("YOK");
                else if (_rules.GetState(i) == ConvoyRules.TruckState.Arrived)
                    sb.Append("ULAŞTI");
                else
                    sb.Append(Mathf.CeilToInt(truck.Hp / ConvoyRules.TruckMaxHp * 100f)).Append('%');
            }

            string status;
            if (_rules.IsOver)
                status = _rules.WinnerTeam == ConvoyRules.DefenderTeam ? "KONVOY ULAŞTI — SAVUNMA KAZANDI" : "KONVOY DURDURULDU — PUSU KAZANDI";
            else
                status = "SAVUNMA: kamyonları koru · TAKVİYE " + Mathf.CeilToInt(_skirmish != null && _skirmish.Rules != null ? _skirmish.Rules.SecondsToNextWave : 0f) + " sn";

            var trucks = sb.ToString();
            if (title != _lastTitle) { _title.text = _lastTitle = title; }
            if (trucks != _lastTrucks) { _trucksText.text = _lastTrucks = trucks; }
            if (status != _lastStatus) { _status.text = _lastStatus = status; }
        }
    }

    /// <summary>Prosedürel ikmal kamyonu: basit kutu gövde, canı olan <see cref="IDamageable"/> (mermi ve patlama hedefi).</summary>
    public sealed class ConvoyTruck : MonoBehaviour, IDamageable
    {
        private Func<PlayerId, int> _teamOf;

        public int Index { get; private set; }
        public PlayerId OwnerId { get; private set; }
        public float Hp { get; private set; }
        public float Distance;
        public bool Finished;
        public bool Arrived;
        public bool IsAlive => Hp > 0f;

        public static ConvoyTruck Create(Transform parent, int index, PlayerId id, float maxHp, Func<PlayerId, int> teamOf)
        {
            var go = new GameObject("KonvoyKamyon" + (index + 1));
            go.transform.SetParent(parent, false);
            var truck = go.AddComponent<ConvoyTruck>();
            truck.Index = index;
            truck.OwnerId = id;
            truck.Hp = maxHp;
            truck._teamOf = teamOf;
            Build(go.transform);

            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return truck;
        }

        public void ApplyDamage(DamageInfo damage)
        {
            var team = _teamOf != null ? _teamOf(damage.AttackerId) : -1;
            Damage(ConvoyRules.TruckDamage(damage.Amount, team));
        }

        public void Damage(float amount)
        {
            if (!IsAlive || !(amount > 0f))
                return;
            Hp = Mathf.Max(0f, Hp - amount);
        }

        /// <summary>Yıkıntı görünümü: kararır; yeni isabet almaz.</summary>
        public void Wreck()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.07f, 0.07f, 0.07f));
            block.SetColor("_Color", new Color(0.07f, 0.07f, 0.07f));
            for (var i = 0; i < renderers.Length; i++)
                renderers[i].SetPropertyBlock(block);
        }

        private static void Build(Transform root)
        {
            StructureKit.CreateBox(root, "Sasi", new Vector3(0f, 0.85f, 0f), new Vector3(2.4f, 0.4f, 7.2f), Quaternion.identity, MaterialId.VehicleDark);
            StructureKit.CreateBox(root, "Kabin", new Vector3(0f, 1.9f, 2.5f), new Vector3(2.4f, 1.7f, 1.9f), Quaternion.identity, MaterialId.VehicleOlive);
            StructureKit.CreateBox(root, "Cam", new Vector3(0f, 2.25f, 3.47f), new Vector3(2.0f, 0.8f, 0.06f), Quaternion.identity, MaterialId.Windshield, false);
            StructureKit.CreateBox(root, "Kasa", new Vector3(0f, 2.05f, -1.2f), new Vector3(2.5f, 2.0f, 4.4f), Quaternion.identity, MaterialId.VehicleOlive);
            StructureKit.CreateBox(root, "Ponco", new Vector3(0f, 3.15f, -1.2f), new Vector3(2.3f, 0.35f, 4.2f), Quaternion.identity, MaterialId.VehicleTan, false);
            var wheelZ = new[] { 2.5f, -0.6f, -2.6f };
            for (var i = 0; i < wheelZ.Length; i++)
            {
                StructureKit.CreateBox(root, "TekerSol" + i, new Vector3(-1.2f, 0.6f, wheelZ[i]), new Vector3(0.45f, 1.2f, 1.2f), Quaternion.identity, MaterialId.Tire, false);
                StructureKit.CreateBox(root, "TekerSag" + i, new Vector3(1.2f, 0.6f, wheelZ[i]), new Vector3(0.45f, 1.2f, 1.2f), Quaternion.identity, MaterialId.Tire, false);
            }
        }
    }
}
