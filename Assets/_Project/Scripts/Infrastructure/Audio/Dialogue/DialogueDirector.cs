using System;
using System.Collections.Generic;
using Project.Application.Dialogue;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Combat;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Infrastructure.Audio.Dialogue
{
    /// <summary>
    /// Diyalog v2 yönetmeni: takım arkadaşlarının stres hâli (sakin/çatışma/panik), bağırma-telsiz yönlendirmesi,
    /// bağlamsal replikler (şarjör, el bombası, yaralanma, düşman düştü, ört, ilerliyorum, sis...), saat yönü + mesafe
    /// ile birleşik tespit cümlesi, 8 ses kimliği, rütbe duyarlı yanıt, öncelik/ducking. Kendi kendini kurar.
    /// Eksik klip/kitap: prosedürel konuşma yedeği (ProceduralVoice) çalışır. Klip örneklenemiyorsa (DecompressOnLoad değil)
    /// birleştirme atlanır ve klip doğrudan çalınır (bkz. <see cref="DialogueClipLibrary"/>). Sunucuda (ses yok) sessizce çalışmaz.
    /// Dış giriş noktaları: <see cref="Say"/>, <see cref="SaySpotted"/>, <see cref="Active"/>, <see cref="DuckGain"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueDirector : MonoBehaviour
    {
        private sealed class Utterance
        {
            public Combatant Speaker;
            public int SpeakerId;
            public string Category;
            public DialogueLine Line;
            public ComposedPhrase Phrase;
            public string Text;
            public DialogueChannel Channel;
            public RouteDecision Route;
            public DialogueStress Stress;
            public VoiceIdentity Voice;
            public bool InCombat;
            public float EstimatedSeconds;
        }

        private struct Delayed
        {
            public float At;
            public Combatant Speaker;
            public string Category;
        }

        public const float SubtitleMin = 1.2f;

        private static DialogueDirector _instance;

        /// <summary>v2 sistemi çalışıyor ve kullanılabilir mi (eski telsiz bu kategorileri bırakır).</summary>
        public static bool Active => _instance != null && _instance._ready;

        /// <summary>Diğer sesleri (müzik/ortam/çatışma) kısmak için yumuşatılmış kazanç (1 = kısma yok).</summary>
        public static float DuckGain { get; private set; } = 1f;

        /// <summary>Telsiz hattının v2 tarafından meşgul olduğu son an (Time.unscaledTime).</summary>
        public static float RadioBusyUntil { get; private set; }

        private readonly DialogueArbiter _arbiter = new DialogueArbiter();
        private readonly DuckSmoother _duck = new DuckSmoother();
        private readonly BarkMemory _memory = new BarkMemory();
        private readonly VoiceAssigner _voices = new VoiceAssigner();
        private readonly Dictionary<int, StressTracker> _trackers = new Dictionary<int, StressTracker>(16);
        private readonly Dictionary<int, DialogueChannel> _lastChannel = new Dictionary<int, DialogueChannel>(16);
        private readonly List<Combatant> _team = new List<Combatant>(12);
        private readonly List<Combatant> _squad = new List<Combatant>(12);
        private readonly List<Delayed> _delayed = new List<Delayed>(4);
        private readonly List<DialogueLine> _scratch = new List<DialogueLine>(16);

        private DialoguePlayback _playback;
        private IEventBus _bus;
        private bool _ready;
        private int _teamId = -1;
        private float _nextSquadRefresh;
        private float _nextAmbient = 6f;
        private float _nextSpottedCheck;
        private int _seedCounter;

        private readonly Action<PlayerDamagedEvent> _onDamaged;
        private readonly Action<PlayerDiedEvent> _onDied;
        private readonly Action<WeaponReloadStartedEvent> _onReload;
        private readonly Action<WeaponFiredEvent> _onFired;
        private readonly Action<ExplosionEvent> _onExplosion;
        private readonly Action<ItemUsedEvent> _onItem;
        private readonly Action<SquadOrderIssuedEvent> _onOrder;
        private readonly Action<RevivedEvent> _onRevived;

        public DialogueDirector()
        {
            _onDamaged = e => Guard(() => OnDamaged(e));
            _onDied = e => Guard(() => OnDied(e));
            _onReload = e => Guard(() => OnReload(e));
            _onFired = e => Guard(() => OnFired(e));
            _onExplosion = e => Guard(() => OnExplosion(e));
            _onItem = e => Guard(() => OnItem(e));
            _onOrder = e => Guard(() => OnOrder(e));
            _onRevived = e => Guard(() => OnRevived(e));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
                return;

            var go = new GameObject("DialogueDirector");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DialogueDirector>();
            _instance._playback = new DialoguePlayback(go.transform);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            _playback?.StopAll();
            if (_instance == this)
            {
                _instance = null;
                DuckGain = 1f;
            }
        }

        // ------------------------------------------------------------------ dış API

        /// <summary>Bir askerin kategoriden replik söylemesini ister (öncelik/bekleme/yönlendirme uygulanır).</summary>
        public static bool Say(Combatant speaker, string category)
        {
            return _instance != null && _instance.TrySay(speaker, category, null);
        }

        /// <summary>Dünya konumunda düşman tespiti: saat yönü + mesafe + hedef cümlesi (bakış: yerel oyuncu).</summary>
        public static bool SaySpotted(Combatant speaker, Vector3 enemyPosition, string targetId = null)
        {
            return _instance != null && _instance.TrySpotted(speaker, enemyPosition, targetId);
        }

        // ------------------------------------------------------------------ döngü

        private void Update()
        {
            var now = Time.unscaledTime;
            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.25f);

            IEventBus bus = null;
            if (GameContext.IsReady)
                GameContext.TryGet(out bus);

            if (!ReferenceEquals(bus, _bus))
            {
                Unsubscribe();
                _arbiter.Reset();
                _trackers.Clear();
                _lastChannel.Clear();
                _delayed.Clear();
                _memory.Clear();
                _voices.Clear();
                if (bus != null)
                    Subscribe(bus);
            }

            _ready = _bus != null && CanPlay() && DialogueClipLibrary.Book != null;
            if (!_ready)
            {
                DuckGain = _duck.Step(dt, 1f);
                return;
            }

            if (now >= _nextSquadRefresh)
            {
                _nextSquadRefresh = now + 0.5f;
                RefreshSquad();
            }

            for (var i = 0; i < _squad.Count; i++)
            {
                var c = _squad[i];
                if (c == null)
                    continue;
                var t = Tracker(c);
                t.Tick(dt, c.State.Normalized);
            }

            _playback.Update(now);

            if (_arbiter.TryDequeue(now, out var queued) && queued.Payload is Utterance qu)
                Play(qu, now);

            for (var i = _delayed.Count - 1; i >= 0; i--)
            {
                if (now < _delayed[i].At)
                    continue;
                var d = _delayed[i];
                _delayed.RemoveAt(i);
                if (d.Speaker != null && d.Speaker.IsAlive)
                    TrySay(d.Speaker, d.Category, null);
            }

            if (now >= _nextAmbient)
            {
                _nextAmbient = now + Random.Range(3.5f, 7f);
                Guard(AmbientBark);
            }

            DuckGain = _duck.Step(dt, _arbiter.DuckTarget(now));
        }

        private static bool CanPlay()
        {
            try
            {
                return GameAudio.Enabled && GameAudio.HasListener;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Guard(Action a)
        {
            try
            {
                a();
            }
            catch (Exception)
            {
                // Diyalog süs özelliğidir; hata oyunu asla bozmamalı.
            }
        }

        private void Subscribe(IEventBus bus)
        {
            _bus = bus;
            try
            {
                bus.Subscribe(_onDamaged);
                bus.Subscribe(_onDied);
                bus.Subscribe(_onReload);
                bus.Subscribe(_onFired);
                bus.Subscribe(_onExplosion);
                bus.Subscribe(_onItem);
                bus.Subscribe(_onOrder);
                bus.Subscribe(_onRevived);
            }
            catch (Exception)
            {
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
                bus.Unsubscribe(_onFired);
                bus.Unsubscribe(_onExplosion);
                bus.Unsubscribe(_onItem);
                bus.Unsubscribe(_onOrder);
                bus.Unsubscribe(_onRevived);
            }
            catch (Exception)
            {
            }
        }

        // ------------------------------------------------------------------ takım / dinleyici

        private void RefreshSquad()
        {
            _squad.Clear();
            var local = CombatantRegistry.LocalPlayer;
            if (local == null || !local.IsInitialized)
            {
                _teamId = -1;
                return;
            }

            _teamId = local.Team;
            _team.Clear();
            CombatantRegistry.GetAliveTeam(_teamId, _team);
            for (var i = 0; i < _team.Count; i++)
                if (_team[i] != null && !_team[i].IsLocalPlayer)
                    _squad.Add(_team[i]);
            _team.Clear();
        }

        private bool IsSquadmate(Combatant c) => c != null && !c.IsLocalPlayer && _teamId >= 0 && c.Team == _teamId;

        private StressTracker Tracker(Combatant c)
        {
            var id = c.Id.Value;
            if (!_trackers.TryGetValue(id, out var t))
            {
                t = new StressTracker();
                _trackers[id] = t;
            }

            return t;
        }

        private static bool Listener(out Vector3 position, out float yaw)
        {
            var local = CombatantRegistry.LocalPlayer;
            var cam = Camera.main;
            if (cam != null)
            {
                position = cam.transform.position;
                yaw = cam.transform.eulerAngles.y;
                return true;
            }

            if (local != null)
            {
                position = local.transform.position;
                yaw = local.transform.eulerAngles.y;
                return true;
            }

            position = Vector3.zero;
            yaw = 0f;
            return false;
        }

        private Combatant NearestSquadmate(Vector3 point, Combatant exclude = null)
        {
            Combatant best = null;
            var bestSqr = float.MaxValue;
            for (var i = 0; i < _squad.Count; i++)
            {
                var c = _squad[i];
                if (c == null || c == exclude || !c.IsAlive)
                    continue;
                var d = (c.transform.position - point).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = c;
                }
            }

            return best;
        }

        // ------------------------------------------------------------------ olaylar

        private void OnDamaged(PlayerDamagedEvent e)
        {
            if (_teamId < 0 || !CombatantRegistry.TryGet(e.VictimId, out var victim) || victim.Team != _teamId)
                return;

            var attacker = CombatantRegistry.Get(e.AttackerId);
            var hostile = attacker != null && attacker.Team != _teamId;
            if (!hostile)
                return;

            if (IsSquadmate(victim))
            {
                var t = Tracker(victim);
                t.AddHeat(0.4f);
                AddHeatNear(victim.transform.position, 25f, 0.15f, victim);

                if (e.RemainingHealth <= 15f)
                    TrySay(victim, Random.value < 0.5f ? DialogueCats.Critical : DialogueCats.Wounded, null);
                else if (e.RemainingHealth <= 40f)
                    TrySay(victim, DialogueCats.Wounded, null);
                else
                    TrySay(victim, DialogueCats.HitSelf, null);

                if (e.RemainingHealth <= 40f)
                {
                    var helper = NearestSquadmate(victim.transform.position, victim);
                    if (helper != null)
                        _delayed.Add(new Delayed { At = Time.unscaledTime + Random.Range(0.8f, 1.6f), Speaker = helper, Category = DialogueCats.ManDown });
                }
            }

            if (e.HasSourcePosition)
            {
                var src = new Vector3(e.SourcePosition.X, e.SourcePosition.Y, e.SourcePosition.Z);
                var speaker = IsSquadmate(victim) ? NearestSquadmate(src, victim) : NearestSquadmate(src);
                if (speaker != null)
                    TrySpotted(speaker, src, null);
            }
        }

        private void AddHeatNear(Vector3 point, float radius, float amount, Combatant exclude)
        {
            var r2 = radius * radius;
            for (var i = 0; i < _squad.Count; i++)
            {
                var c = _squad[i];
                if (c == null || c == exclude)
                    continue;
                if ((c.transform.position - point).sqrMagnitude <= r2)
                    Tracker(c).AddHeat(amount);
            }
        }

        private void OnDied(PlayerDiedEvent e)
        {
            if (_teamId < 0 || !CombatantRegistry.TryGet(e.VictimId, out var victim))
                return;

            var killer = CombatantRegistry.Get(e.KillerId);
            if (victim.Team != _teamId)
            {
                if (IsSquadmate(killer))
                {
                    Tracker(killer).AddHeat(0.1f);
                    TrySay(killer, e.IsHeadshot ? DialogueCats.EnemyDownHeadshot : DialogueCats.EnemyDown, null);
                }
                else if (killer != null && killer.IsLocalPlayer)
                {
                    var helper = NearestSquadmate(victim.transform.position);
                    if (helper != null)
                        _delayed.Add(new Delayed { At = Time.unscaledTime + Random.Range(0.6f, 1.4f), Speaker = helper, Category = Random.value < 0.5f ? DialogueCats.EnemyDown : "squad_kill_ack" });
                }

                return;
            }

            if (victim.IsLocalPlayer)
                return;

            var near = NearestSquadmate(victim.transform.position, victim);
            if (near != null)
            {
                Tracker(near).AddHeat(0.3f);
                TrySay(near, Random.value < 0.5f ? DialogueCats.Medic : DialogueCats.ManDown, null);
            }
        }

        private void OnReload(WeaponReloadStartedEvent e)
        {
            if (!CombatantRegistry.TryGet(e.ShooterId, out var c) || !IsSquadmate(c))
                return;

            var roll = Random.value;
            TrySay(c, roll < 0.3f ? DialogueCats.MagEmpty : DialogueCats.Reload, null);
        }

        private void OnFired(WeaponFiredEvent e)
        {
            if (_teamId < 0 || !CombatantRegistry.TryGet(e.ShooterId, out var shooter))
                return;

            if (shooter.Team == _teamId)
            {
                if (IsSquadmate(shooter))
                    Tracker(shooter).AddHeat(0.03f);
                return;
            }

            if (!e.HasOrigin)
                return;

            var origin = new Vector3(e.Origin.X, e.Origin.Y, e.Origin.Z);
            AddHeatNear(origin, 40f, 0.06f, null);

            var now = Time.unscaledTime;
            if (now < _nextSpottedCheck || !Listener(out var lp, out _))
                return;
            _nextSpottedCheck = now + 1.5f;
            if ((origin - lp).sqrMagnitude > 150f * 150f)
                return;

            var speaker = NearestSquadmate(origin);
            if (speaker != null)
                TrySpotted(speaker, origin, null);
        }

        private void OnExplosion(ExplosionEvent e)
        {
            if (_teamId < 0)
                return;
            var p = new Vector3(e.Position.X, e.Position.Y, e.Position.Z);
            AddHeatNear(p, Mathf.Max(30f, e.Radius * 3f), 0.5f, null);
        }

        private void OnItem(ItemUsedEvent e)
        {
            if (!e.Started || _teamId < 0 || !CombatantRegistry.TryGet(e.UserId, out var user))
                return;

            var smoke = e.ItemId == "grenade_smoke";
            var frag = e.ItemId == "grenade_frag";
            if (IsSquadmate(user))
            {
                if (frag)
                    TrySay(user, DialogueCats.GrenadeThrow, null);
                else if (smoke)
                    TrySay(user, DialogueCats.Smoke, null);
                else if (e.ItemId == "bandage")
                    TrySay(user, DialogueCats.SelfHeal, null);
                return;
            }

            if (frag && user.Team != _teamId && Listener(out var lp, out _) && (user.transform.position - lp).sqrMagnitude < 45f * 45f)
            {
                var warner = NearestSquadmate(lp);
                if (warner != null)
                    TrySay(warner, DialogueCats.GrenadeIncoming, null);
            }
        }

        private void OnOrder(SquadOrderIssuedEvent e)
        {
            if (_teamId < 0 || e.Team != _teamId || !Listener(out var lp, out _))
                return;

            var local = CombatantRegistry.LocalPlayer;
            var issuerRank = local != null ? local.Rank : MilitaryRank.Er;
            var ordered = new List<Combatant>(_squad);
            ordered.Sort((a, b) => (a.transform.position - lp).sqrMagnitude.CompareTo((b.transform.position - lp).sqrMagnitude));

            var count = 0;
            for (var i = 0; i < ordered.Count && count < 2; i++)
            {
                var c = ordered[i];
                if (c == null || !c.IsAlive)
                    continue;
                var category = RankReplies.AckCategory(c.Rank, issuerRank);
                _delayed.Add(new Delayed { At = Time.unscaledTime + 0.15f + count * 0.95f, Speaker = c, Category = category });
                count++;
            }

            if (e.Order == SquadOrder.Attack && ordered.Count > 2 && ordered[2] != null && ordered[2].IsAlive)
                _delayed.Add(new Delayed { At = Time.unscaledTime + 2.2f, Speaker = ordered[2], Category = DialogueCats.Advance });
        }

        private void OnRevived(RevivedEvent e)
        {
            if (_teamId < 0 || !CombatantRegistry.TryGet(e.ReviverId, out var reviver) || !reviver.IsLocalPlayer)
                return;
            // Kaldırılan asker teşekkür eder (olayda hedef kimliği yoksa en yakın takım arkadaşı).
            if (Listener(out var lp, out _))
            {
                var c = NearestSquadmate(lp);
                if (c != null)
                    _delayed.Add(new Delayed { At = Time.unscaledTime + 1.0f, Speaker = c, Category = DialogueCats.RevivedThanks });
            }
        }

        private void AmbientBark()
        {
            if (_squad.Count == 0)
                return;

            var c = _squad[Random.Range(0, _squad.Count)];
            if (c == null || !c.IsAlive)
                return;

            var stress = Tracker(c).Current;
            var r = Random.value;
            string category;
            switch (stress)
            {
                case DialogueStress.Panic:
                    category = r < 0.4f ? DialogueCats.TakingFire : r < 0.7f ? DialogueCats.CoverMe : r < 0.85f ? DialogueCats.FallBack : DialogueCats.Breath;
                    break;
                case DialogueStress.Combat:
                    category = r < 0.28f ? DialogueCats.Advance : r < 0.5f ? DialogueCats.CoverMe : r < 0.7f ? DialogueCats.Covering
                        : r < 0.88f ? DialogueCats.Suppress : DialogueCats.Flank;
                    break;
                default:
                    if (Random.value > 0.45f)
                        return;
                    category = r < 0.35f ? DialogueCats.Clear : r < 0.65f ? DialogueCats.Watch : r < 0.85f ? DialogueCats.Holding : DialogueCats.ReportToLeader;
                    break;
            }

            TrySay(c, category, null);
        }

        // ------------------------------------------------------------------ söyleme

        private bool TrySpotted(Combatant speaker, Vector3 enemy, string targetId)
        {
            if (!_ready || speaker == null || !speaker.IsAlive || speaker.IsLocalPlayer || !Listener(out var lp, out var yaw))
                return false;

            var to = enemy - lp;
            to.y = 0f;
            var meters = to.magnitude;
            var bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            var relative = TurkishNumbers.RelativeAngle(yaw, bearing);

            var stress = Tracker(speaker).Current;
            var phrase = PhraseComposer.ComposeSpotted(DialogueClipLibrary.Book, _memory, speaker.Id.Value, stress, relative, meters,
                targetId, () => Random.value);
            if (phrase == null)
                return false;

            return TrySay(speaker, DialogueCats.ContactFull, phrase);
        }

        private bool TrySay(Combatant speaker, string category, ComposedPhrase phrase)
        {
            if (!_ready || speaker == null || speaker.IsLocalPlayer || !speaker.IsAlive || string.IsNullOrEmpty(category))
                return false;
            if (!Listener(out var lp, out _))
                return false;

            var id = speaker.Id.Value;
            var tracker = Tracker(speaker);
            var stress = tracker.Current;
            var rule = DialogueCategoryRules.Get(category);

            _lastChannel.TryGetValue(id, out var last);
            var distance = Vector3.Distance(lp, speaker.transform.position);
            var route = ProximityRouter.Route(distance, stress, rule.Priority == DialoguePriority.Critical, rule.AllowShout, rule.AllowRadio, last);
            if (route.Channel == DialogueChannel.None)
                return false;

            var u = new Utterance
            {
                Speaker = speaker,
                SpeakerId = id,
                Category = category,
                Channel = route.Channel,
                Route = route,
                Stress = stress,
                Voice = _voices.IdentityFor(id).ForStress(stress),
                InCombat = stress != DialogueStress.Calm,
                Phrase = phrase
            };

            if (phrase != null)
            {
                u.Text = phrase.Text;
            }
            else
            {
                var book = DialogueClipLibrary.Book;
                if (book.Collect(category, stress, _scratch) == 0)
                    return false;
                u.Line = _memory.Pick(category, id, _scratch, Random.value);
                if (u.Line == null)
                    return false;
                u.Text = u.Line.Text;
            }

            u.EstimatedSeconds = Mathf.Max(SubtitleMin, u.Text.Length * 0.062f / Mathf.Max(0.6f, u.Voice.Speed)) + (u.Channel == DialogueChannel.Radio ? 0.3f : 0f);

            var now = Time.unscaledTime;
            var priority = rule.Priority;
            if (stress == DialogueStress.Panic && priority == DialoguePriority.Normal)
                priority = DialoguePriority.High;

            var request = new DialogueRequest(u.Line != null ? u.Line.Id : category, category, route.Channel, priority, u.EstimatedSeconds, id, rule.Cooldown, u);
            var result = _arbiter.Request(now, request);
            for (var i = 0; i < _arbiter.Preempted.Count; i++)
                _playback.Stop(_arbiter.Preempted[i]);

            if (result == ArbiterResult.Play)
            {
                Play(u, now);
                return true;
            }

            return result == ArbiterResult.Queued;
        }

        private void Play(Utterance u, float now)
        {
            if (u.Speaker == null || !u.Speaker.IsAlive)
                return;

            var samples = BuildSamples(u, out var procedural);
            if (samples == null)
                return;

            AudioClip clip;
            var owned = true;
            var pitch = 1f;
            if (samples.IsDirect)
            {
                // Klip örneklenemiyor (DecompressOnLoad değil): birleştirme + kimlik/telsiz DSP'si atlanır, klip olduğu gibi çalınır.
                // Kimlik perdesi AudioSource.pitch ile, telsiz karakteri kaynak süzgeçleriyle taşınır. Klip bir varlıktır: Destroy edilmez.
                clip = samples.DirectClip;
                owned = false;
                pitch = DialogueClipLibrary.DirectPitch(u.Voice);
            }
            else
            {
                if (samples.Data == null || samples.Data.Length == 0)
                    return;

                var data = samples.Data;
                var rate = samples.SampleRate;
                if (u.Channel == DialogueChannel.Radio)
                {
                    var settings = RadioDspSettings.For(u.Route.SignalQuality, u.Stress, u.InCombat);
                    _seedCounter++;
                    data = RadioDsp.Process(data, rate, settings, (uint)(VoiceIdentities.Hash(u.SpeakerId) ^ (uint)(_seedCounter * 7919)));
                }

                clip = DialogueClipLibrary.ToClip("dlg_" + (u.Line != null ? u.Line.Id : "composed"), data, rate);
            }

            if (clip == null)
                return;

            var seconds = clip.length / pitch;
            if (u.Channel == DialogueChannel.Radio)
            {
                RadioBusyUntil = Mathf.Max(RadioBusyUntil, now + seconds);
                var highCut = DialoguePlayback.RadioBandHighHz - (1f - Mathf.Clamp01(u.Route.SignalQuality)) * 700f;
                _playback.PlayRadio(u.SpeakerId, clip, u.Route.Volume, owned, pitch, highCut);
            }
            else
            {
                _playback.PlayShout(u.SpeakerId, u.Speaker.transform, clip, u.Route.Volume, u.Route.MaxDistance, Occlusion(u.Speaker), owned, pitch);
            }

            _lastChannel[u.SpeakerId] = u.Channel;
            RaiseSubtitle(u, Mathf.Max(SubtitleMin, seconds));
        }

        private VoiceSamples BuildSamples(Utterance u, out bool procedural)
        {
            procedural = false;
            VoiceSamples samples = null;
            var vstress = DialogueClipLibrary.ToVoiceStress(u.Stress); // v2 ses/stres klasörü (VoiceV2Resolver)

            if (u.Phrase != null)
            {
                // Parça yoksa ya da örneklenemiyorsa (DecompressOnLoad değil) birleştirme ATLANIR (null) ve bütün satıra düşülür.
                samples = DialogueClipLibrary.StitchParts(u.Phrase.PartIds, 0.05f, u.Voice.Speed, u.SpeakerId, vstress);
                if (samples == null && !string.IsNullOrEmpty(u.Phrase.FallbackLineId))
                {
                    var whole = DialogueClipLibrary.Resolve(u.Phrase.FallbackLineId, u.SpeakerId, vstress);
                    if (whole != null)
                    {
                        // Bütün satır klibi farklı bir cümledir; yalnız klip varsa kullan, altyazı ona uysun.
                        var line = DialogueClipLibrary.Book.Get(u.Phrase.FallbackLineId);
                        if (line != null)
                            u.Text = line.Text;
                        samples = whole; // örneklenemiyorsa DirectClip: doğrudan çalınır
                    }
                }
            }
            else if (u.Line != null)
            {
                samples = DialogueClipLibrary.Resolve(u.Line.Id, u.SpeakerId, vstress);
            }

            if (samples != null)
            {
                if (samples.IsDirect)
                    return samples; // gerçek ses kaydı, DSP'siz: prosedürel konuşmadan her zaman iyi

                var processed = VoiceDsp.ApplyIdentity(samples.Data, samples.SampleRate, u.Voice);
                return new VoiceSamples { Data = processed, SampleRate = samples.SampleRate };
            }

            procedural = true;
            var seed = VoiceIdentities.Hash(u.SpeakerId) ^ (uint)((u.Text != null ? u.Text.Length : 0) * 31);
            var synth = ProceduralVoice.Synthesize(u.Text, u.Voice, u.Stress, DialogueClipLibrary.FallbackSampleRate, seed);
            return new VoiceSamples { Data = synth, SampleRate = DialogueClipLibrary.FallbackSampleRate, Procedural = true };
        }

        private static float Occlusion(Combatant speaker)
        {
            try
            {
                if (!Listener(out var lp, out _))
                    return 0f;
                var target = speaker.transform.position + Vector3.up * 1.5f;
                if (Physics.Linecast(lp, target, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && hit.collider.transform.root != speaker.transform.root)
                        return 0.85f;
                }
            }
            catch (Exception)
            {
            }

            return 0f;
        }

        private static void RaiseSubtitle(Utterance u, float seconds)
        {
            string name;
            try
            {
                name = u.Speaker.RankedName;
            }
            catch (Exception)
            {
                name = u.Speaker.DisplayName;
            }

            var tone = u.Line != null && !string.IsNullOrEmpty(u.Line.VoiceHint) ? u.Line.VoiceHint.Split(';')[0] : (u.Stress == DialogueStress.Panic ? "acil" : u.Stress == DialogueStress.Combat ? "gergin" : "sakin");
            RadioChatterSystem.RaiseSpoken(new RadioSpoken(name ?? string.Empty, u.Text, tone, seconds));
        }
    }
}
