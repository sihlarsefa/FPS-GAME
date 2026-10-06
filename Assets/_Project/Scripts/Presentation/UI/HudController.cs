using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Presentation.Bootstrap;
using Project.Presentation.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Oyun içi HUD (FPP, Türkçe). <see cref="Create"/> kendi tuvalini (sıra 10) kurar ve tüm görünümleri katman
    /// sırasıyla ekler: ekran efektleri → dost işaretleri → dürbün → hasar yönü → nişangâh/isabet → bağlamsal bilgi →
    /// can/silah → pusula → durum → tim paneli → öldürme akışı → bildirimler. (Mini harita bootstrap tarafından
    /// <see cref="Root"/> altına en üste eklenir.)
    /// <para>Olaylar GameContext olay yolundan güvenle dinlenir: bağlam hazır değilse sonra bağlanır, değişirse
    /// yeniden bağlanır, yok edilince abonelikler kaldırılır. Eksik servislerde ilgili bölüm sessizce boş kalır.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        public const int CanvasSortOrder = 10;

        private static readonly HashSet<string> LoggedFailures = new HashSet<string>();

        private IPlayerHudSource _player;
        private Canvas _canvas;
        private CanvasGroup _group;
        private HudContext _ctx;
        private bool _initialized;
        private bool _headless;
        private bool _visible = true;

        private HudScreenEffectsView _effects;
        private AllyMarkersView _allyMarkers;
        private ReconMarkersView _reconMarkers;
        private ScopeOverlayView _scope;
        private DamageIndicatorView _damage;
        private CrosshairView _crosshair;
        private HudCenterInfoView _centerInfo;
        private RectTransform _playerLayer;
        private CanvasGroup _playerGroup;
        private HudVitalsView _vitals;
        private HudWeaponView _weapons;
        private CompassView _compass;
        private HudStatusView _status;
        private HudMovementView _movement;
        private GrenadeWarningView _grenadeWarning;
        private SquadPanelView _squad;
        private KillFeedView _killFeed;
        private NotificationView _notifications;

        private float _playerAlpha = 1f;
        private bool _wasAlive;
        private bool _effectsReset;

        // Abonelikler (delegeler bir kez oluşturulur; abonelik/çıkış aynı örnekle yapılır).
        private readonly Action<HitConfirmedEvent> _onHitConfirmed;
        private readonly Action<PlayerDamagedEvent> _onPlayerDamaged;
        private readonly Action<PlayerDiedEvent> _onPlayerDied;
        private readonly Action<MatchPhaseChangedEvent> _onPhaseChanged;
        private readonly Action<ZoneStageChangedEvent> _onZoneStageChanged;
        private readonly Action<CommandTransferredEvent> _onCommandTransferred;
        private readonly Action<ArtilleryStrikeEvent> _onArtillery;
        private readonly Action<SquadOrderIssuedEvent> _onSquadOrder;
        private readonly Action<KillFeedEntry> _onKillFeedEntry;
        private readonly Action<GameSettings> _onSettingsChanged;
        private readonly Action<string, float> _onPlayerNotification;

        private IEventBus _subscribedBus;
        private KillFeedService _subscribedFeed;
        private SettingsService _subscribedSettings;
        private bool _bound;
        private float _nextSettingsPoll;
        private bool _fpsShown;

        public HudController()
        {
            _onHitConfirmed = OnHitConfirmed;
            _onPlayerDamaged = OnPlayerDamaged;
            _onPlayerDied = OnPlayerDied;
            _onPhaseChanged = OnPhaseChanged;
            _onZoneStageChanged = OnZoneStageChanged;
            _onCommandTransferred = OnCommandTransferred;
            _onArtillery = OnArtilleryStrike;
            _onSquadOrder = OnSquadOrderIssued;
            _onKillFeedEntry = OnKillFeedEntry;
            _onSettingsChanged = OnSettingsChanged;
            _onPlayerNotification = OnPlayerNotification;
        }

        /// <summary>HUD kök dikdörtgeni (tam ekran). Mini harita gibi ek HUD öğeleri buraya eklenir.</summary>
        public RectTransform Root { get; private set; }

        /// <summary>HUD tuvali.</summary>
        public Canvas Canvas => _canvas;

        /// <summary>HUD görünümlerinin paylaştığı durum (servisler, yerel oyuncu, tim).</summary>
        public HudContext Context => _ctx;

        /// <summary>HUD görünür mü?</summary>
        public bool IsVisible => _visible;

        /// <summary>Başsız (adanmış sunucu) süreçte HUD kurulmadıysa true.</summary>
        public bool IsHeadless => _headless;

        /// <summary>Bildirim görünümü (bootstrap/oyuncu ek bildirim göstermek isterse).</summary>
        public NotificationView Notifications => _notifications;

        /// <summary>HUD'u oluşturur. <paramref name="player"/> null olabilir (yalnızca genel bilgiler gösterilir).</summary>
        public static HudController Create(IPlayerHudSource player)
        {
            var canvas = UiFactory.CreateCanvas("[HUD]", CanvasSortOrder);
            var hud = canvas.gameObject.AddComponent<HudController>();
            hud.Initialize(player, canvas);
            AdvancedDisplay.ApplyHudScale(hud);
            return hud;
        }

        /// <summary>HUD'u gösterir/gizler (maç sonu ekranı, ölüm sonrası). Gizliyken efekt sesleri de durur.</summary>
        public void SetVisible(bool visible)
        {
            if (_headless)
                return;

            _visible = visible;
            if (_group != null)
            {
                _group.alpha = visible ? HudStyle.Opacity : 0f;
                _group.interactable = visible;
                _group.blocksRaycasts = visible;
            }

            if (!visible && _effects != null)
            {
                _effects.ResetEffects();
                _effectsReset = true;
            }
        }

        /// <summary>Ekran ortasında büyük mesaj gösterir (ör. "ŞEHİT DÜŞTÜN", zafer).</summary>
        public void ShowCenterMessage(string text, float seconds)
        {
            if (_notifications == null)
                return;

            try
            {
                _notifications.ShowCenter(text, seconds);
            }
            catch (Exception e)
            {
                LogOnce("ShowCenterMessage", e);
            }
        }

        /// <summary>Üst ortaya kısa bildirim ekler.</summary>
        public void ShowNotification(string text, HudNoticeKind kind = HudNoticeKind.Info, float seconds = 4f)
        {
            if (_notifications == null)
                return;

            try
            {
                _notifications.Push(text, kind, seconds);
            }
            catch (Exception e)
            {
                LogOnce("ShowNotification", e);
            }
        }

        // ================================================================== kurulum

        private void Initialize(IPlayerHudSource player, Canvas canvas)
        {
            if (_initialized)
                return;

            _initialized = true;
            _player = player;
            _canvas = canvas;
            _ctx = new HudContext(player);

            Root = UiFactory.CreateRect("HudRoot", canvas.transform);
            _group = UiFactory.EnsureCanvasGroup(Root);
            _group.alpha = 1f;
            _group.interactable = true;
            _group.blocksRaycasts = true;

            // Adanmış sunucu (Windows Server, -batchmode -nographics): HUD yalnızca istemci içindir — görünüm, doku ve
            // olay aboneliği kurulmaz; çağrılar boşa düşer.
            if (IsHeadlessProcess())
            {
                _headless = true;
                _visible = false;
                _group.alpha = 0f;
                enabled = false;
                return;
            }

            Build();
            EnsureBinding();
            SafeRefresh();
            ApplyFpsSetting(ReadShowFps());
        }

        private void Build()
        {
            _effects = Make("HudScreenEffectsView", () => HudScreenEffectsView.Create(Root, _ctx));
            _allyMarkers = Make("AllyMarkersView", () => AllyMarkersView.Create(Root, _ctx));
            _reconMarkers = Make("ReconMarkersView", () => ReconMarkersView.Create(Root, _ctx));
            _scope = Make("ScopeOverlayView", () => ScopeOverlayView.Create(Root, _ctx));
            _damage = Make("DamageIndicatorView", () => DamageIndicatorView.Create(Root, _ctx));
            _crosshair = Make("CrosshairView", () => CrosshairView.Create(Root, _ctx));
            _centerInfo = Make("HudCenterInfoView", () => HudCenterInfoView.Create(Root, _ctx));

            _playerLayer = UiFactory.CreateRect("PlayerLayer", Root);
            _playerGroup = HudBuild.PassiveGroup(_playerLayer);
            _vitals = Make("HudVitalsView", () => HudVitalsView.Create(_playerLayer, _ctx));
            _weapons = Make("HudWeaponView", () => HudWeaponView.Create(_playerLayer, _ctx));

            _movement = Make("HudMovementView", () => HudMovementView.Create(_playerLayer, _ctx));
            Make("LimbStateView", () => LimbStateView.Create(_playerLayer, _ctx)); // uzuv yaraları + kanama (kendi Update'i; Combatant.Limbs)
            _grenadeWarning = Make("GrenadeWarningView", () => GrenadeWarningView.Create(Root, _ctx));
            _compass = Make("CompassView", () => CompassView.Create(Root, _ctx));
            _status = Make("HudStatusView", () => HudStatusView.Create(Root, _ctx));
            _squad = Make("SquadPanelView", () => SquadPanelView.Create(Root, _ctx));
            _killFeed = Make("KillFeedView", () => KillFeedView.Create(Root,
                new Vector2(-HudStatusView.RightMargin, -(HudStatusView.TopMargin + HudStatusView.Height + 6f))));
            _notifications = Make("NotificationView", () => NotificationView.Create(Root));
            _zoneTimer = Make("ZoneTimerView", () => ZoneTimerView.Create(Root, _ctx));
            Make("ScorePanel", () => ScorePanel.Create(Root, _ctx));
        }

        /// <summary>Grafik aygıtı olmayan / adanmış sunucu süreci mi?</summary>
        private static bool IsHeadlessProcess()
        {
            try
            {
                if (ServerRuntime.IsDedicatedServer)
                    return true;
            }
            catch (Exception)
            {
                // sunucu algılama yoksa grafik aygıtına bak
            }

            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        }

        private static T Make<T>(string name, Func<T> factory) where T : class
        {
            try
            {
                return factory();
            }
            catch (Exception e)
            {
                LogOnce(name + ".Create", e);
                return null;
            }
        }

        // ================================================================== servis / olay bağlama

        private void EnsureBinding()
        {
            var services = GameContext.Services;
            if (ReferenceEquals(services, _ctx.BoundServices) && (_bound || services == null))
                return;

            Unsubscribe();
            _ctx.Bind(services);
            _bound = services != null;
            if (_bound)
                Subscribe();
        }

        private void Subscribe()
        {
            var bus = _ctx.Bus;
            if (bus != null)
            {
                _subscribedBus = bus;
                try
                {
                    bus.Subscribe(_onHitConfirmed);
                    bus.Subscribe(_onPlayerDamaged);
                    bus.Subscribe(_onPlayerDied);
                    bus.Subscribe(_onPhaseChanged);
                    bus.Subscribe(_onZoneStageChanged);
                    bus.Subscribe(_onCommandTransferred);
                    bus.Subscribe(_onArtillery);
                    bus.Subscribe(_onSquadOrder);
                }
                catch (Exception e)
                {
                    LogOnce("EventBus.Subscribe", e);
                }
            }

            var feed = _ctx.KillFeed;
            if (feed != null)
            {
                feed.EntryAdded += _onKillFeedEntry;
                _subscribedFeed = feed;
            }

            var settings = _ctx.Settings ?? SessionSettings();
            if (settings != null)
            {
                settings.Changed += _onSettingsChanged;
                _subscribedSettings = settings;
                ApplyFpsSetting(settings.Current != null && settings.Current.ShowFps);
            }

            if (_ctx.PlayerController != null)
            {
                _ctx.PlayerController.Notification -= _onPlayerNotification;
                _ctx.PlayerController.Notification += _onPlayerNotification;
            }
        }

        private void Unsubscribe()
        {
            var bus = _subscribedBus;
            _subscribedBus = null;
            if (bus != null)
            {
                try
                {
                    bus.Unsubscribe(_onHitConfirmed);
                    bus.Unsubscribe(_onPlayerDamaged);
                    bus.Unsubscribe(_onPlayerDied);
                    bus.Unsubscribe(_onPhaseChanged);
                    bus.Unsubscribe(_onZoneStageChanged);
                    bus.Unsubscribe(_onCommandTransferred);
                    bus.Unsubscribe(_onArtillery);
                    bus.Unsubscribe(_onSquadOrder);
                }
                catch (Exception e)
                {
                    LogOnce("EventBus.Unsubscribe", e);
                }
            }

            if (_subscribedFeed != null)
            {
                _subscribedFeed.EntryAdded -= _onKillFeedEntry;
                _subscribedFeed = null;
            }

            if (_subscribedSettings != null)
            {
                _subscribedSettings.Changed -= _onSettingsChanged;
                _subscribedSettings = null;
            }

            if (_ctx != null && _ctx.PlayerController != null)
                _ctx.PlayerController.Notification -= _onPlayerNotification;

            _bound = false;
        }

        private static SettingsService SessionSettings()
        {
            try
            {
                return GameSession.Settings;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool ReadShowFps()
        {
            try
            {
                var settings = _ctx.Settings ?? SessionSettings();
                if (settings != null && settings.Current != null)
                    return settings.Current.ShowFps;

                var pc = _ctx.PlayerController;
                if (pc != null && pc.Settings != null)
                    return pc.Settings.ShowFps;
            }
            catch (Exception)
            {
                // varsayılan kapalı
            }

            return false;
        }

        private void ApplyFpsSetting(bool show)
        {
            _fpsShown = show;
            if (_status != null)
                _status.SetFpsVisible(show);
        }

        private void OnEnable() => Project.Infrastructure.Support.SupportAbilitySystem.Notice += OnSupportNotice;

        private void OnDisable() => Project.Infrastructure.Support.SupportAbilitySystem.Notice -= OnSupportNotice;

        /// <summary>T-129 ATAK Desteği bildirimleri (kendi timi: hazır/yolda/çekiliyor/düştü; düşman: uyarı).</summary>
        private void OnSupportNotice(int team, Project.Infrastructure.Support.SupportNoticeKind kind, Vector3 position)
        {
            if (_notifications == null || _ctx.LocalTeam < 0)
                return;

            var mine = team == _ctx.LocalTeam;
            switch (kind)
            {
                case Project.Infrastructure.Support.SupportNoticeKind.Ready:
                    if (mine && _ctx.CommanderId.IsValid && _ctx.IsLocal(_ctx.CommanderId))
                    {
                        _notifications.Push("T-129 ATAK desteği hazır — " + Project.Infrastructure.Input.InputBindings.Bracket(BindAction.AttackHeli) + " ile çağırın",
                            HudNoticeKind.Radio, 5f);
                        PlaySound(SoundId.RadioBeep, 0.5f);
                    }

                    break;
                case Project.Infrastructure.Support.SupportNoticeKind.Incoming:
                    if (mine)
                        _notifications.Push("T-129 ATAK helikopteri yolda — 25 sn görev", HudNoticeKind.Radio, 4.5f);
                    else if (_ctx.LocalAlive)
                        _notifications.Push("DİKKAT! Düşman saldırı helikopteri sahada", HudNoticeKind.Danger, 5f);
                    PlaySound(mine ? SoundId.RadioBeep : SoundId.ZoneWarning, 0.5f);
                    break;
                case Project.Infrastructure.Support.SupportNoticeKind.Departing:
                    if (mine)
                        _notifications.Push("T-129 görevi tamamladı, üsse dönüyor", HudNoticeKind.Info, 3.5f);
                    break;
                case Project.Infrastructure.Support.SupportNoticeKind.ShotDown:
                    _notifications.Push(mine ? "T-129 düşürüldü!" : "Düşman saldırı helikopteri düşürüldü", mine ? HudNoticeKind.Danger : HudNoticeKind.Radio, 4f);
                    break;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_effects != null)
                _effects.ResetEffects();
        }

        // ================================================================== kare döngüsü

        private void Update()
        {
            if (!_initialized)
                return;

            EnsureBinding();
            SafeRefresh();

            var dt = Time.deltaTime;
            var udt = Time.unscaledDeltaTime;

            // Ayar aboneliği yoksa FPS ayarını seyrek yokla.
            if (_subscribedSettings == null && Time.unscaledTime >= _nextSettingsPoll)
            {
                _nextSettingsPoll = Time.unscaledTime + 1f;
                var show = ReadShowFps();
                if (show != _fpsShown)
                    ApplyFpsSetting(show);
            }

            if (!_visible)
            {
                if (!_effectsReset && _effects != null)
                {
                    _effects.ResetEffects();
                    _effectsReset = true;
                }

                return;
            }

            _effectsReset = false;
            _group.alpha = HudStyle.Opacity;

            var alive = _ctx.LocalAlive;
            if (_wasAlive && !alive)
            {
                if (_damage != null)
                    _damage.Clear();
            }

            _wasAlive = alive;

            // Oyuncu katmanı (can, silah) ölünce/bilinmezken solar.
            var hasPlayer = _ctx.Local != null && _ctx.Local.IsInitialized;
            _playerAlpha = Mathf.MoveTowards(_playerAlpha, alive && hasPlayer ? 1f : 0f, udt * 3f);
            HudBuild.SetAlpha(_playerGroup, _playerAlpha);

            var outside = _status != null && _status.OutsideZone;

            if (_effects != null)
            {
                try { _effects.Tick(dt, outside, true); }
                catch (Exception e) { LogOnce("HudScreenEffectsView.Tick", e); }
            }

            if (_allyMarkers != null)
            {
                try
                {
                    if (alive || hasPlayer)
                        _allyMarkers.Tick(dt);
                    else
                        _allyMarkers.HideAll();
                }
                catch (Exception e) { LogOnce("AllyMarkersView.Tick", e); }
            }

            if (_reconMarkers != null)
            {
                try
                {
                    if (alive || hasPlayer)
                        _reconMarkers.Tick(dt);
                    else
                        _reconMarkers.HideAll();
                }
                catch (Exception e) { LogOnce("ReconMarkersView.Tick", e); }
            }

            if (_scope != null)
            {
                try { _scope.Tick(udt, !alive); }
                catch (Exception e) { LogOnce("ScopeOverlayView.Tick", e); }
            }

            if (_damage != null)
            {
                try { TrackHealArmor(); }
                catch (Exception e) { LogOnce("DamageIndicatorView.HealArmor", e); }
                try { _damage.Tick(dt); }
                catch (Exception e) { LogOnce("DamageIndicatorView.Tick", e); }
            }

            if (_crosshair != null)
            {
                try { _crosshair.Tick(udt, !alive); }
                catch (Exception e) { LogOnce("CrosshairView.Tick", e); }
            }

            if (_centerInfo != null)
            {
                try { _centerInfo.Tick(udt, alive); }
                catch (Exception e) { LogOnce("HudCenterInfoView.Tick", e); }
            }

            if (_playerAlpha > 0.001f)
            {
                if (_vitals != null)
                {
                    try { _vitals.Tick(udt); }
                    catch (Exception e) { LogOnce("HudVitalsView.Tick", e); }
                }

                if (_weapons != null)
                {
                    try { _weapons.Tick(udt); }
                    catch (Exception e) { LogOnce("HudWeaponView.Tick", e); }
                }
            }

            if (_playerAlpha > 0.001f && _movement != null)
            {
                try { _movement.Tick(udt); }
                catch (Exception e) { LogOnce("HudMovementView.Tick", e); }
            }

            if (_grenadeWarning != null)
            {
                try { _grenadeWarning.Tick(udt, alive); }
                catch (Exception e) { LogOnce("GrenadeWarningView.Tick", e); }
            }

            if (_compass != null)
            {
                try { _compass.Tick(udt); }
                catch (Exception e) { LogOnce("CompassView.Tick", e); }
            }

            if (_status != null)
            {
                try { _status.Tick(udt); }
                catch (Exception e) { LogOnce("HudStatusView.Tick", e); }
            }

            if (_squad != null)
            {
                try { _squad.Tick(udt); }
                catch (Exception e) { LogOnce("SquadPanelView.Tick", e); }
            }

            if (_killFeed != null)
            {
                try { _killFeed.Tick(udt); }
                catch (Exception e) { LogOnce("KillFeedView.Tick", e); }
            }

            if (_notifications != null)
            {
                try { _notifications.Tick(udt); }
                catch (Exception e) { LogOnce("NotificationView.Tick", e); }
            }
        }

        private void SafeRefresh()
        {
            try
            {
                _ctx.Refresh();
            }
            catch (Exception e)
            {
                LogOnce("HudContext.Refresh", e);
            }
        }

        private static void LogOnce(string key, Exception e)
        {
            if (LoggedFailures.Add(key))
                Debug.LogWarning("[HUD] " + key + " hata verdi: " + e);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LoggedFailures.Clear();
        }

        // ================================================================== olay işleyicileri

        private void OnHitConfirmed(HitConfirmedEvent e)
        {
            if (_crosshair == null || !_ctx.IsLocal(e.AttackerId) || e.VictimId == e.AttackerId)
                return;

            _crosshair.ShowHit(e.IsHeadshot, e.IsKill, e.ArmorAbsorbed, e.Damage);
        }

        private ZoneTimerView _zoneTimer;
        private float _prevHealth = -1f;
        private bool _prevHadArmor;

        /// <summary>Can yükselince temiz silme (Heal), zırh bitince beyaz parlama + halka (ArmorBreak).</summary>
        private void TrackHealArmor()
        {
            var local = _ctx != null ? _ctx.Local : null;
            if (local == null || !local.IsInitialized || !local.IsAlive)
            {
                _prevHealth = -1f;
                _prevHadArmor = false;
                return;
            }

            var hp = local.State.Current;
            if (_prevHealth >= 0f && hp > _prevHealth + 0.5f)
                _damage.Heal();
            _prevHealth = hp;

            var armor = local.Armor != null ? local.Armor.GetArmorFor(BodyPart.Torso) : null;
            var has = armor != null && armor.Durability > 0f;
            if (_prevHadArmor && !has)
                _damage.ArmorBreak();
            _prevHadArmor = has;
        }

        private void OnPlayerDamaged(PlayerDamagedEvent e)
        {
            if (!_ctx.IsLocal(e.VictimId) || e.DamageAmount <= 0f)
                return;

            var zone = string.Equals(e.WeaponId, DamageSourceIds.Zone, StringComparison.Ordinal);
            if (!zone && _effects != null)
                _effects.Flash(e.DamageAmount);

            if (_damage == null || zone)
                return;

            if (TryResolveDamageSource(e, out var source))
                _damage.Show(source, e.DamageAmount);
        }

        private bool TryResolveDamageSource(PlayerDamagedEvent e, out Vector3 source)
        {
            if (e.HasSourcePosition)
            {
                source = DamageIndicatorView.ToVector(e.SourcePosition);
                return true;
            }

            if (e.AttackerId.IsValid && e.AttackerId != e.VictimId && CombatantRegistry.TryGet(e.AttackerId, out var attacker))
            {
                source = attacker.transform.position;
                return true;
            }

            var local = _ctx.Local;
            if (local != null && e.AttackerId.IsValid && local.LastAttackerId == e.AttackerId
                && Time.time - local.LastDamageTime < 0.25f)
            {
                source = local.LastDamageSource;
                return source.sqrMagnitude > 0.0001f;
            }

            source = Vector3.zero;
            return false;
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            var victimLocal = _ctx.IsLocal(e.VictimId);
            var killerLocal = _ctx.IsLocal(e.KillerId);

            if (victimLocal)
            {
                if (_damage != null)
                    _damage.Clear();
            }
            else if (killerLocal)
            {
                _ctx.LocalKillsFallback++;
                if (_notifications != null)
                {
                    if (_ctx.IsAlly(e.VictimId))
                    {
                        _notifications.Push("Dost ateşi! " + _ctx.NameOf(e.VictimId) + " şehit düştü", HudNoticeKind.Danger, 4f);
                    }
                    else
                    {
                        _notifications.ShowKill(_ctx.NameOf(e.VictimId), e.IsHeadshot, _ctx.LocalKills);
                    }
                }
            }

            // Öldürme akışı servisi yoksa kaydı burada üret.
            if (_subscribedFeed == null && _killFeed != null && e.VictimId.IsValid)
            {
                var environmental = !e.KillerId.IsValid;
                var entry = new KillFeedEntry(
                    environmental ? string.Empty : _ctx.NameOf(e.KillerId),
                    _ctx.NameOf(e.VictimId),
                    HudContext.WeaponName(e.WeaponId, environmental),
                    e.IsHeadshot,
                    killerLocal,
                    victimLocal,
                    !environmental && (killerLocal || _ctx.IsAlly(e.KillerId)),
                    victimLocal || _ctx.IsAlly(e.VictimId));
                _killFeed.Add(entry);
            }
        }

        private void OnKillFeedEntry(KillFeedEntry entry)
        {
            if (_killFeed != null)
                _killFeed.Add(entry);
        }

        private void OnPhaseChanged(MatchPhaseChangedEvent e)
        {
            if (_notifications == null)
                return;

            switch (e.CurrentPhase)
            {
                case MatchPhase.Insertion:
                    _notifications.ShowCenter("İNTİKAL BAŞLADI", 3f, UiTheme.Amber);
                    _notifications.Push("Tim, harekât bölgesine intikal ediyor", HudNoticeKind.Radio, 4f);
                    break;
                case MatchPhase.InMatch:
                    _notifications.ShowCenter("HAREKÂT BAŞLADI", 3.5f, UiTheme.Amber);
                    _notifications.Push("Son ayakta kalan tim kazanır — harekât alanının içinde kal", HudNoticeKind.Info, 5f);
                    break;
                case MatchPhase.Ending:
                    _notifications.Push("Harekât sona eriyor", HudNoticeKind.Info, 3f);
                    break;
            }
        }

        private void OnZoneStageChanged(ZoneStageChangedEvent e)
        {
            if (_notifications == null)
                return;

            switch (e.Stage)
            {
                case ZoneStage.Waiting:
                    _notifications.Push("Yeni harekât alanı belirlendi — " + UiWidgets.Clock(e.DurationSeconds) + " sonra daralacak",
                        HudNoticeKind.Warning, 5f);
                    break;
                case ZoneStage.Shrinking:
                    _notifications.Push("Harekât alanı daralıyor!", HudNoticeKind.Warning, 4f);
                    PlaySound(SoundId.ZoneWarning, 0.55f);
                    break;
                case ZoneStage.Finished:
                    _notifications.Push("Harekât alanı son sınırına ulaştı", HudNoticeKind.Info, 4f);
                    break;
            }
        }

        private void OnCommandTransferred(CommandTransferredEvent e)
        {
            if (_notifications == null || _ctx.LocalTeam < 0 || e.Team != _ctx.LocalTeam)
                return;

            if (!e.NewCommanderId.IsValid)
            {
                _notifications.Push("Timde komutayı devralacak kimse kalmadı", HudNoticeKind.Danger, 4f);
                return;
            }

            if (_ctx.IsLocal(e.NewCommanderId))
            {
                _notifications.ShowCenter("KOMUTA SİZDE", 3f, UiTheme.Amber);
                _notifications.Push("Komuta size geçti — F1-F4 ile tim emri verin", HudNoticeKind.Radio, 5f);
            }
            else
            {
                _notifications.Push("Komuta " + HudFormat.Dative(_ctx.NameOf(e.NewCommanderId)) + " geçti", HudNoticeKind.Radio, 5f);
            }

            PlaySound(SoundId.RadioBeep, 0.5f);
        }

        private void OnArtilleryStrike(ArtilleryStrikeEvent e)
        {
            if (e.IsImpact || _notifications == null)
                return;

            var target = DamageIndicatorView.ToVector(e.Target);
            var dx = target.x - _ctx.Position.x;
            var dz = target.z - _ctx.Position.z;
            var distance = Mathf.Sqrt(dx * dx + dz * dz);

            if (_ctx.LocalTeam >= 0 && e.Team == _ctx.LocalTeam)
            {
                var caller = _ctx.IsLocal(e.CallerId) ? string.Empty : " (" + _ctx.NameOf(e.CallerId) + ")";
                _notifications.Push("Topçu ateşi istendi" + caller + " — hedef " + HudFormat.Meters(distance), HudNoticeKind.Radio, 4.5f);
                PlaySound(SoundId.RadioBeep, 0.45f);
                return;
            }

            // Düşman topçusu yakına düşüyorsa uyar.
            if (_ctx.LocalAlive && distance < ArtilleryService.SpreadRadius + 60f)
            {
                _notifications.Push("DİKKAT! Düşman topçu ateşi — bölgeden uzaklaş!", HudNoticeKind.Danger, 5f);
                PlaySound(SoundId.ZoneWarning, 0.5f);
            }
        }

        private void OnSquadOrderIssued(SquadOrderIssuedEvent e)
        {
            if (_notifications == null || _ctx.LocalTeam < 0 || e.Team != _ctx.LocalTeam)
                return;

            // Oyuncu kendisi emir verdiyse oyuncu kontrolcüsü zaten bildirim gösterir.
            var commander = _ctx.CommanderId;
            if (commander.IsValid && _ctx.IsLocal(commander))
                return;

            _notifications.Push(HudFormat.OrderMessage(e.Order), HudNoticeKind.Order, 3.5f);
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            if (settings != null)
                ApplyFpsSetting(settings.ShowFps);
        }

        private void OnPlayerNotification(string text, float seconds)
        {
            // Oyuncu bildirimleri etkileşim istemi alanında gösterilir (InteractionPrompt); burada yalnızca
            // istem alanı yoksa yedek olarak üst bildirime düşülür.
            if (_centerInfo == null && _notifications != null)
                _notifications.Push(text, HudNoticeKind.Info, seconds);
        }

        private static void PlaySound(SoundId id, float volume)
        {
            try
            {
                GameAudio.Play2D(id, volume);
            }
            catch (Exception)
            {
                // ses sistemi yoksa sessiz devam
            }
        }
    }
}
