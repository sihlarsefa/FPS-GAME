using System;
using System.Collections.Generic;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Infrastructure.Audio
{
    /// <summary>Konuşulan telsiz repliği (altyazı görünümü bunu dinler).</summary>
    public readonly struct RadioSpoken
    {
        public readonly string Speaker;
        public readonly string Text;
        public readonly string Tone;
        public readonly float Seconds;

        public RadioSpoken(string speaker, string text, string tone, float seconds)
        {
            Speaker = speaker;
            Text = text;
            Tone = tone;
            Seconds = seconds;
        }
    }

    /// <summary>
    /// Telsiz muhabbeti: GameContext'teki IEventBus olaylarını (temas, şarjör, yaralı/ölen dost, emir onayı F1-F4,
    /// komuta devri, topçu, daralan çember, tim elendi, zafer) dinler; öncelik + bekleme süresiyle (RadioScheduler)
    /// bir replik seçer, telsiz çıtırtı/bip sesini çalar ve <see cref="LineSpoken"/> ile altyazıyı tetikler.
    /// Kendi kendini kurar (RuntimeInitializeOnLoadMethod); eksik servis/veri durumunda sessizce çalışmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RadioChatterSystem : MonoBehaviour
    {
        public const float SubtitleSeconds = 3f;

        public static event Action<RadioSpoken> LineSpoken;

        /// <summary>Konuşmacı konuşurken öldü: hat yarıda kesildi (argüman: konuşmacı adı). Altyazı görünümü isterse dinler.</summary>
        public static event Action<string> LineCut;

        private static RadioChatterSystem _instance;

        /// <summary>Başka sistemlerin (diyalog v2) altyazı olayını tetiklemesi için.</summary>
        public static void RaiseSpoken(RadioSpoken spoken) => LineSpoken?.Invoke(spoken);

        /// <summary>Diyalog v2 aktifken şu kategorileri o üstlenir (v1 sessiz kalır).</summary>
        private static bool V2Handles => Dialogue.DialogueDirector.Active;

        private static readonly string[] GenericContact =
            { "contact_multiple", "contact_building", "contact_rooftop", "contact_near_tree", "contact_ridge", "contact_bridge", "contact_quarry", "contact_vehicle" };
        private static readonly string[] ReloadKeys = { "reload_mag", "reload_mag_cover", "reload_last_mag", "reload_cover_me" };
        private static readonly string[] WoundKeys = { "wound_hit", "wound_hit_arm", "wound_hit_leg", "wound_bleeding", "wound_critical", "wound_still_fighting" };
        private static readonly string[] MedicCallKeys = { "medic_call", "medic_call_urgent" };

        private readonly RadioLineBook _book = RadioLineBook.LoadFromResources();
        private readonly RadioScheduler _scheduler = new RadioScheduler();
        private readonly List<Combatant> _team = new List<Combatant>(12);
        private readonly string[] _single = new string[1];

        private sealed class Pending
        {
            public string[] Keys;
            public int Team;
            public Combatant Speaker;
            public string Role;
            public RadioPriority Priority;
            public string Category;
            public float Cooldown;
            public Combatant Exclude;
        }

        private readonly RadioDisciplineQueue<Pending> _queue = new RadioDisciplineQueue<Pending>();
        private readonly Dictionary<string, float> _categoryAt = new Dictionary<string, float>(16);
        private Combatant _currentSpeaker;
        private string _currentName;
        private float _lineEndsAt = -1f;
        private float _lastBusyBeep = -999f;
        private PlayerId _commanderId = PlayerId.Invalid;
        private bool _draining;

        private IEventBus _bus;
        private float _beepAt = -1f;
        private RadioVoicePlayer _voice;
        private readonly Dictionary<string, float> _calloutAt = new Dictionary<string, float>(8);

        private readonly Action<PlayerDamagedEvent> _onDamaged;
        private readonly Action<PlayerDiedEvent> _onDied;
        private readonly Action<WeaponReloadStartedEvent> _onReload;
        private readonly Action<SquadOrderIssuedEvent> _onOrder;
        private readonly Action<DownedEvent> _onDowned;
        private readonly Action<RevivedEvent> _onRevived;
        private readonly Action<CommandTransferredEvent> _onCommand;
        private readonly Action<ArtilleryStrikeEvent> _onArtillery;
        private readonly Action<ZoneStageChangedEvent> _onZone;
        private readonly Action<MatchEndedEvent> _onMatchEnded;

        public RadioChatterSystem()
        {
            _onDamaged = e => Guard(() => OnDamaged(e));
            _onDied = e => Guard(() => OnDied(e));
            _onReload = e => Guard(() => OnReload(e));
            _onOrder = e => Guard(() => OnOrder(e));
            _onDowned = e => Guard(() => OnDowned(e));
            _onRevived = e => Guard(() => OnRevived(e));
            _onCommand = e => Guard(() => OnCommand(e));
            _onArtillery = e => Guard(() => OnArtillery(e));
            _onZone = e => Guard(() => OnZone(e));
            _onMatchEnded = e => Guard(() => OnMatchEnded(e));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;

            var go = new GameObject("RadioChatterSystem");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RadioChatterSystem>();
            _instance._voice = go.AddComponent<RadioVoicePlayer>();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            IEventBus bus = null;
            if (GameContext.IsReady)
                GameContext.TryGet(out bus);

            if (!ReferenceEquals(bus, _bus))
            {
                Unsubscribe();
                _scheduler.Reset();
                _queue.Clear();
                _categoryAt.Clear();
                _lineEndsAt = -1f;
                _currentSpeaker = null;
                if (bus != null)
                    Subscribe(bus);
            }

            DrainQueue();

            if (_beepAt > 0f && Time.unscaledTime >= _beepAt)
            {
                _beepAt = -1f;
                Guard(() => GameAudio.Play2D(SoundId.RadioBeep, 0.45f, UnityEngine.Random.Range(0.95f, 1.08f)));
            }
        }

        private void Subscribe(IEventBus bus)
        {
            try
            {
                bus.Subscribe(_onDamaged);
                bus.Subscribe(_onDied);
                bus.Subscribe(_onReload);
                bus.Subscribe(_onOrder);
                bus.Subscribe(_onDowned);
                bus.Subscribe(_onRevived);
                bus.Subscribe(_onCommand);
                bus.Subscribe(_onArtillery);
                bus.Subscribe(_onZone);
                bus.Subscribe(_onMatchEnded);
                _bus = bus;
            }
            catch (Exception)
            {
                _bus = bus;
            }
        }

        private void Unsubscribe()
        {
            var bus = _bus;
            _bus = null;
            if (bus == null)
                return;

            try
            {
                bus.Unsubscribe(_onDamaged);
                bus.Unsubscribe(_onDied);
                bus.Unsubscribe(_onReload);
                bus.Unsubscribe(_onOrder);
                bus.Unsubscribe(_onDowned);
                bus.Unsubscribe(_onRevived);
                bus.Unsubscribe(_onCommand);
                bus.Unsubscribe(_onArtillery);
                bus.Unsubscribe(_onZone);
                bus.Unsubscribe(_onMatchEnded);
            }
            catch (Exception)
            {
            }
        }

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                // Telsiz süs özelliğidir; hata oyunu asla bozmamalı.
            }
        }

        // ------------------------------------------------------------------ olaylar

        private static bool LocalTeam(out int team)
        {
            var local = CombatantRegistry.LocalPlayer;
            if (local != null && local.IsInitialized)
            {
                team = local.Team;
                return true;
            }

            team = -1;
            return false;
        }

        private void OnDamaged(PlayerDamagedEvent e)
        {
            if (!LocalTeam(out var team) || !CombatantRegistry.TryGet(e.VictimId, out var victim) || victim.Team != team)
                return;

            var attacker = CombatantRegistry.Get(e.AttackerId);
            if (attacker != null && attacker.Team != team)
            {
                if (victim.IsLocalPlayer)
                    PlayerCallout("contact", 8f);
                if (V2Handles)
                    return;
                var keys = ContactKeys(e);
                Speak(keys, team, null, null, RadioPriority.Normal, "contact", 8f, victim);
            }

            if (!victim.IsLocalPlayer && !V2Handles && e.RemainingHealth > 0f && e.RemainingHealth <= 30f)
                Speak(WoundKeys, team, victim, victim.Role.ToString(), RadioPriority.Low, "wound_" + victim.Id.Value, 12f, null);
        }

        private string[] ContactKeys(PlayerDamagedEvent e)
        {
            var local = CombatantRegistry.LocalPlayer;
            if (!e.HasSourcePosition || local == null)
                return GenericContact;

            var to = new Vector3(e.SourcePosition.X, 0f, e.SourcePosition.Z) - new Vector3(local.transform.position.x, 0f, local.transform.position.z);
            if (to.sqrMagnitude < 1f)
                return GenericContact;

            _single[0] = DirectionKey(local.transform.eulerAngles.y, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            return _single;
        }

        /// <summary>Bakış yönüne göre kaynak yönü: ön/arka/sol/sağ temas anahtarı.</summary>
        public static string DirectionKey(float yawDegrees, float bearingDegrees)
        {
            var rel = Mathf.DeltaAngle(yawDegrees, bearingDegrees);
            var abs = Mathf.Abs(rel);
            if (abs <= 45f)
                return "contact_front";
            if (abs >= 135f)
                return "contact_rear";
            return rel < 0f ? "contact_left" : "contact_right";
        }

        private void OnDied(PlayerDiedEvent e)
        {
            if (!LocalTeam(out var team) || !CombatantRegistry.TryGet(e.VictimId, out var victim))
                return;

            CutIfSpeaking(victim);

            var alive = 0;
            _team.Clear();
            CombatantRegistry.GetTeam(victim.Team, _team);
            for (var i = 0; i < _team.Count; i++)
                if (_team[i] != victim && _team[i].IsAlive)
                    alive++;
            _team.Clear();

            if (alive == 0)
            {
                if (victim.Team == team)
                    Speak("squad_wiped_us", team, victim, null, RadioPriority.High, "wiped", 20f, null);
                else
                    Speak("squad_wiped_enemy", team, null, "Radioman", RadioPriority.High, "wiped", 20f, null);
                return;
            }

            if (victim.Team == team)
            {
                if (!victim.IsLocalPlayer && !V2Handles)
                    Speak(MedicCallKeys, team, null, null, RadioPriority.Normal, "allydown", 10f, victim);
                return;
            }

            var killer = CombatantRegistry.Get(e.KillerId);
            if (killer != null && killer.Team == team && !killer.IsLocalPlayer && !V2Handles)
                Speak("contact_confirmed_kill", team, killer, killer.Role.ToString(), RadioPriority.Low, "kill", 15f, null);
        }

        private void OnReload(WeaponReloadStartedEvent e)
        {
            if (!LocalTeam(out var team) || !CombatantRegistry.TryGet(e.ShooterId, out var c) || c.Team != team)
                return;

            if (c.IsLocalPlayer)
            {
                PlayerCallout("reload", 10f);
                return;
            }

            if (V2Handles)
                return;

            Speak(ReloadKeys, team, c, c.Role.ToString(), RadioPriority.Low, "reload", 12f, null);
        }

        private void OnDowned(DownedEvent e)
        {
            if (!LocalTeam(out var team) || e.Team != team || !CombatantRegistry.TryGet(e.VictimId, out var victim) || victim.IsLocalPlayer)
                return;

            PlayerCallout("ally_down", 8f);
        }

        private void OnRevived(RevivedEvent e)
        {
            if (!LocalTeam(out _) || !CombatantRegistry.TryGet(e.ReviverId, out var reviver) || !reviver.IsLocalPlayer)
                return;

            PlayerCallout("revive", 6f);
        }

        /// <summary>Yerel oyuncunun sesli çağrısı (filtresiz, perde 0.92). Kategori: reload, grenade, contact, ally_down, revive.</summary>
        public static void CalloutGrenade()
        {
            if (_instance != null)
                _instance.PlayerCallout("grenade", 4f);
        }

        private void PlayerCallout(string category, float cooldown)
        {
            if (_voice == null)
                return;

            var now = Time.unscaledTime;
            if (_calloutAt.TryGetValue(category, out var next) && now < next)
                return;

            if (_voice.PlayPlayer(RadioVoicePlayer.PlayerKey(category, Random.value)))
                _calloutAt[category] = now + cooldown;
        }

        private void OnOrder(SquadOrderIssuedEvent e)
        {
            if (!LocalTeam(out var team) || e.Team != team || V2Handles)
                return;

            string key;
            switch (e.Order)
            {
                case SquadOrder.Follow: key = "order_follow_ack"; break;
                case SquadOrder.HoldPosition: key = "order_hold_ack"; break;
                case SquadOrder.Attack: key = "order_attack_ack"; break;
                case SquadOrder.Regroup: key = "order_regroup_ack"; break;
                default: return;
            }

            Speak(key, team, null, null, RadioPriority.High, "order", 1.5f, null);
        }

        private void OnCommand(CommandTransferredEvent e)
        {
            if (!LocalTeam(out var team) || e.Team != team)
                return;

            _commanderId = e.NewCommanderId;
            if (CombatantRegistry.TryGet(e.NewCommanderId, out var next) && !next.IsLocalPlayer)
                Speak("cmd_takeover", team, next, next.Role.ToString(), RadioPriority.High, "command", 6f, null);
            else
                Speak("cmd_leader_down", team, null, null, RadioPriority.High, "command", 6f, null);
        }

        private void OnArtillery(ArtilleryStrikeEvent e)
        {
            if (!LocalTeam(out var team))
                return;

            if (e.Team == team)
            {
                if (e.IsImpact)
                    Speak("arty_splash", team, null, null, RadioPriority.Normal, "arty_hit", 5f, null);
                else
                    Speak(new[] { "arty_request", "arty_ready", "arty_inbound" }, team, null, "Radioman", RadioPriority.High, "arty_call", 4f, null);
                return;
            }

            if (e.IsImpact)
                return;

            var local = CombatantRegistry.LocalPlayer;
            if (local == null)
                return;

            var p = local.transform.position;
            var dx = e.Target.X - p.x;
            var dz = e.Target.Z - p.z;
            if (dx * dx + dz * dz < 90f * 90f)
                Speak("arty_danger_close", team, null, "Radioman", RadioPriority.Critical, "arty_danger", 8f, null);
        }

        private void OnZone(ZoneStageChangedEvent e)
        {
            if (!LocalTeam(out var team) || e.Stage != ZoneStage.Shrinking)
                return;

            var last = e.PhaseCount > 0 && e.PhaseIndex >= e.PhaseCount - 1;
            if (last)
                Speak("zone_final", team, null, "Leader", RadioPriority.High, "zone", 15f, null);
            else
                Speak(new[] { "zone_warning", "zone_edge", "zone_vehicle_rush" }, team, null, null, RadioPriority.High, "zone", 15f, null);
        }

        private void OnMatchEnded(MatchEndedEvent e)
        {
            if (!LocalTeam(out var team) || e.WinnerTeam != team)
                return;

            Speak("victory", team, null, null, RadioPriority.Critical, "victory", 30f, null);
        }

        // ------------------------------------------------------------------ konuşma

        /// <summary>Konuşmacının can/yakın temas durumundan v2 ses stresi (sakin/çatışma/panik).</summary>
        private static VoiceStress StressOf(Combatant speaker)
        {
            try
            {
                var hp = speaker.State.Normalized;
                var inCombat = Time.time - ExplosionSystem.LastExplosionTime < 4f;
                return VoiceV2Resolver.StressFor(hp, 0f, inCombat, false);
            }
            catch (Exception)
            {
                return VoiceStress.Sakin;
            }
        }

        private void Speak(string key, int team, Combatant fixedSpeaker, string role, RadioPriority priority, string category, float cooldown, Combatant exclude)
        {
            _single[0] = key;
            Speak(_single, team, fixedSpeaker, role, priority, category, cooldown, exclude);
        }

        private void Speak(string[] keys, int team, Combatant fixedSpeaker, string role, RadioPriority priority, string category, float cooldown, Combatant exclude)
        {
            if (_book.Count == 0)
                return;

            Combatant speaker = fixedSpeaker;
            RadioLine line = null;

            if (speaker != null)
            {
                line = _book.Pick(keys, role, Random.value, true);
            }
            else
            {
                _team.Clear();
                CombatantRegistry.GetAliveTeam(team, _team);
                var n = _team.Count;
                var start = n > 0 ? Random.Range(0, n) : 0;
                for (var pass = 0; pass < 2 && line == null; pass++)
                {
                    for (var i = 0; i < n && line == null; i++)
                    {
                        var c = _team[(start + i) % n];
                        if (c == null || c.IsLocalPlayer || c == exclude)
                            continue;
                        if (pass == 0 && !string.IsNullOrEmpty(role) && c.Role.ToString() != role)
                            continue;

                        var lookupRole = pass == 0 ? c.Role.ToString() : null;
                        var picked = _book.Pick(keys, lookupRole ?? c.Role.ToString(), Random.value);
                        if (picked == null && pass == 1)
                            continue;
                        if (picked != null)
                        {
                            speaker = c;
                            line = picked;
                        }
                    }
                }

                _team.Clear();
            }

            if (speaker == null || line == null)
                return;

            var now = Time.unscaledTime;
            var commander = IsCommander(speaker);
            var topic = RadioDisciplineRules.TopicOf(category);
            priority = RadioDisciplineRules.PriorityFor(topic, priority, commander);
            cooldown = RadioDisciplineRules.EffectiveCooldown(category, cooldown);

            // Aynı kategori en az 8 sn içinde tekrar etmez (sessizce düşer, kuyruğa girmez).
            if (!string.IsNullOrEmpty(category) && _categoryAt.TryGetValue(category, out var catAt) && now < catAt + cooldown)
                return;

            if (priority < RadioPriority.High && Dialogue.DialogueDirector.RadioBusyUntil > now)
                return;

            var seconds = SubtitleSeconds;
            var text = line.text;
            var far = FarCallsign(speaker, team, out var callerNo, out var calledNo);
            if (far)
            {
                text = RadioDisciplineRules.WithCallsign(text, callerNo, calledNo);
                seconds += 1f;
            }

            if (!_scheduler.TryAccept(now, priority, category, cooldown, seconds))
            {
                // Hat meşgul: kısa bip + kuyruk (yalnız Normal ve üstü; düşük öncelik kaybolur).
                if (!_draining && priority >= RadioPriority.Normal)
                {
                    var queued = _queue.Enqueue(new RadioQueued(priority, topic, commander, now), new Pending
                    {
                        Keys = (string[])keys.Clone(), Team = team, Speaker = fixedSpeaker, Role = role,
                        Priority = priority, Category = category, Cooldown = cooldown, Exclude = exclude
                    });
                    if (queued && RadioDisciplineRules.ShouldBusyBeep(now, _lastBusyBeep))
                    {
                        _lastBusyBeep = now;
                        Guard(() => GameAudio.Play2D(SoundId.RadioBeep, 0.25f, 1.25f));
                    }
                }

                return;
            }

            if (!string.IsNullOrEmpty(category))
                _categoryAt[category] = now;
            _currentSpeaker = speaker;
            _lineEndsAt = now + seconds;

            string name;
            try
            {
                name = speaker.RankedName;
            }
            catch (Exception)
            {
                name = speaker.DisplayName;
            }

            Guard(() => GameAudio.Play2D(SoundId.RadioChatter, 0.35f, Random.Range(0.95f, 1.05f)));
            _beepAt = Time.unscaledTime + 0.12f;
            if (_voice != null)
                Guard(() => _voice.PlayRadio(line.key, speaker.Role.ToString(), speaker.Id.Value.GetHashCode(), 0.9f, StressOf(speaker)));
            _currentName = name;
            LineSpoken?.Invoke(new RadioSpoken(name ?? string.Empty, text, line.tone, seconds));
        }

        // ------------------------------------------------------------------ disiplin

        private bool IsCommander(Combatant c)
        {
            if (c == null)
                return false;
            return (_commanderId.IsValid && c.Id.Equals(_commanderId)) || c.Role.ToString() == "Leader";
        }

        /// <summary>Konuşmacı yerel oyuncudan 200 m'den uzaksa çağrı işareti numaraları (tim sırasına göre).</summary>
        private bool FarCallsign(Combatant speaker, int team, out int callerNo, out int calledNo)
        {
            callerNo = 1;
            calledNo = 1;
            var local = CombatantRegistry.LocalPlayer;
            if (speaker == null || local == null)
                return false;

            var d = speaker.transform.position - local.transform.position;
            d.y = 0f;
            if (!RadioDisciplineRules.NeedsCallsign(d.magnitude))
                return false;

            _team.Clear();
            CombatantRegistry.GetTeam(team, _team);
            for (var i = 0; i < _team.Count; i++)
            {
                if (_team[i] == speaker) callerNo = i + 1;
                if (_team[i] == local) calledNo = i + 1;
            }

            _team.Clear();
            if (callerNo == calledNo)
                calledNo = callerNo == 1 ? 2 : 1;
            return true;
        }

        /// <summary>Ölen konuşmacının hattı yarıda kesilir: ses durur, kesik bip, bekleyen istekleri düşer.</summary>
        private void CutIfSpeaking(Combatant victim)
        {
            _queue.RemoveWhere(p => p.Speaker == victim);
            if (_currentSpeaker != victim || Time.unscaledTime >= _lineEndsAt)
                return;

            _lineEndsAt = -1f;
            _currentSpeaker = null;
            if (_voice != null)
            {
                Guard(() =>
                {
                    var sources = _voice.GetComponentsInChildren<AudioSource>();
                    for (var i = 0; i < sources.Length; i++)
                        sources[i].Stop();
                });
            }

            Guard(() => GameAudio.Play2D(SoundId.RadioBeep, 0.4f, 0.8f));
            LineCut?.Invoke(_currentName ?? string.Empty);
        }

        private void DrainQueue()
        {
            if (_queue.Count == 0 || Time.unscaledTime < _lineEndsAt + 0.3f)
                return;

            if (!_queue.TryDequeue(Time.unscaledTime, out var p) || (p.Speaker != null && !p.Speaker.IsAlive))
                return;

            _draining = true;
            try
            {
                Speak(p.Keys, p.Team, p.Speaker, p.Role, p.Priority, p.Category, p.Cooldown, p.Exclude);
            }
            finally
            {
                _draining = false;
            }
        }
    }
}
