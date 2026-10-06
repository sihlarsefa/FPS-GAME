using System;
using System.Collections.Generic;
using Project.Application.Services;
using Project.Application.Spectate;
using Project.Core.Domain;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Rendering;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Spectator
{
    /// <summary>
    /// Yerel oyuncu öldükten sonra: 4 sn'lik killcam (öldürenin gözünden, canlı; Boşluk/sol tık atlar) ve yanında ölüm özeti
    /// (öldüren + rütbe, silah, mesafe, öldürenin kalan canı, vuruş bölgeleri, hasar çizelgesi); ardından hayattaki takım
    /// arkadaşlarını (sonra herkesi) 3. şahıs yörünge kamerasıyla izleme. Sol tık / E / tekerlek aşağı sonraki, sağ tık / Q
    /// önceki hedef; orta tuş basılıyken fare yörüngeyi çevirir. Altta izlenenin can/zırh/silah özeti, tim tamamen elendiyse
    /// derece + istatistik ekranı. "Ana menüye dön" / "Maç sonu" düğmeleri.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpectatorController : MonoBehaviour
    {
        public const float KillcamSeconds = SpectateRules.KillcamSeconds;
        private const int SortOrder = 55;

        private Combatant _local;
        private Action _onMainMenu;
        private Action _onMatchEnd;
        private Camera _camera;
        private Canvas _canvas;
        private Text _banner;
        private readonly List<SpectatorCandidate> _candidates = new List<SpectatorCandidate>(16);
        private readonly List<int> _order = new List<int>(16);
        private float _startTime;
        private int _targetId = -1;
        private bool _killcam;
        private bool _followKiller;
        private int _killerId = -1;
        private bool _noTargetsReported;
        private float _yaw;
        private float _pitch = 18f;
        private float _distance = 4.5f;
        private Vector3 _lastFocus;
        private DeathRecap _recap;
        private DeathRecapView _recapView;
        private Button _recapButton;
        private bool _recapShown = true;
        private RectTransform _letterTop;
        private RectTransform _letterBottom;
        private Text _killcamTag;
        private RectTransform _targetPanel;
        private Text _targetName;
        private Text _targetStats;
        private Image _targetHp;
        private Image _targetArmor;
        private RectTransform _wipePanel;
        private bool _wipeShown;
        private Vector3 _killerEye;
        private Quaternion _killerRot = Quaternion.identity;
        private bool _hasKillerPose;
        private float _origFov;
        private Project.Presentation.Player.KillCam _replayCam;

        public static SpectatorController Begin(Combatant local, Action onMainMenu, Action onMatchEnd)
        {
            var go = new GameObject("[İzleyici]");
            var c = go.AddComponent<SpectatorController>();
            c._local = local;
            c._onMainMenu = onMainMenu;
            c._onMatchEnd = onMatchEnd;
            c.Setup();
            return c;
        }

        public void Stop()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);
            Destroy(gameObject);
        }

        private void Setup()
        {
            _startTime = Time.time;
            _killcam = true;
            PostProcessing.SetDeathBlur(true);
            _lastFocus = _local != null ? _local.transform.position + Vector3.up * 1.5f : Vector3.zero;

            Camera source = null;
            var cams = Camera.allCameras;
            for (var i = 0; i < cams.Length; i++)
            {
                if (cams[i] == null) continue;
                if (source == null || cams[i].depth > source.depth) source = cams[i];
            }

            var camGo = new GameObject("SpectatorCamera");
            camGo.transform.SetParent(transform, false);
            _camera = camGo.AddComponent<Camera>();
            if (source != null)
            {
                _camera.CopyFrom(source);
                _camera.depth = source.depth + 10f;
                _camera.targetTexture = null;
                camGo.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                // Oyuncu kamerası(ları) kapanır; dinleyici onlarda kalır.
                for (var i = 0; i < cams.Length; i++)
                    if (cams[i] != null && cams[i] != _camera)
                        cams[i].enabled = false;
            }
            else
            {
                _camera.depth = 10f;
            }
            _camera.enabled = true;

            Combatant killer = null;
            if (_local != null && _local.LastAttackerId.IsValid && CombatantRegistry.TryGet(_local.LastAttackerId, out var k) && k != _local)
                killer = k;
            _targetId = killer != null ? killer.Id.Value : -1;
            _killerId = _targetId;
            if (killer == null)
                _killcam = false;

            _origFov = _camera.fieldOfView;
            try { _recap = DeathRecapTracker.Build(_local); }
            catch (Exception e) { Debug.LogException(e); _recap = new DeathRecap(); }

            BuildUi();
            // Tekrar kaydından son 6 sn'lik omuz-arkası killcam; kayıt yok / mod kapalıysa canlı killcam yedeği çalışır.
            try
            {
                if (_local != null && _canvas != null)
                {
                    _replayCam = Project.Presentation.Player.KillCam.TryBegin(_camera, _local.Id.Value, (RectTransform)_canvas.transform);
                    if (_replayCam != null)
                    {
                        _killcam = true;
                        _targetId = _replayCam.Plan.KillerId;
                        _killerId = _targetId;
                        ApplyPhaseVisibility();
                        if (_killcamTag != null) UiFactory.SetText(_killcamTag, "KILLCAM  ·  SON 6 SN  ·  [BOŞLUK] ATLA");
                    }
                }
            }
            catch (Exception e) { Debug.LogException(e); EndReplayCam(); }
            UiFactory.SetCursorFree(true);
            UpdateBanner();
        }

        private void BuildUi()
        {
            _canvas = UiFactory.CreateCanvas("[İzleyici UI]", SortOrder);
            var root = (RectTransform)_canvas.transform;

            var bannerPanel = UiFactory.Panel(root, UiTheme.HudBackdrop);
            UiFactory.Anchor(bannerPanel, UiAnchor.Top, new Vector2(0f, -40f), new Vector2(640f, 56f));
            _banner = UiFactory.Label(bannerPanel, "", 26, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Stretch(_banner);

            var hint = UiFactory.Label(root, "Sol tık / E: sonraki   ·   Sağ tık / Q: önceki   ·   Orta tuş + fare: yörünge   ·   Tekerlek: yakınlık", 16, TextAnchor.MiddleCenter, UiTheme.Khaki);
            UiFactory.Anchor(hint, UiAnchor.Bottom, new Vector2(0f, 110f), new Vector2(900f, 30f));

            var menu = UiFactory.Button(root, "Ana menüye dön", () => _onMainMenu?.Invoke());
            UiFactory.Anchor(menu, UiAnchor.BottomLeft, new Vector2(40f, 40f), new Vector2(260f, 56f));
            var end = UiFactory.Button(root, "Maç sonu", () => _onMatchEnd?.Invoke());
            UiFactory.Anchor(end, UiAnchor.BottomRight, new Vector2(-40f, 40f), new Vector2(260f, 56f));

            _recapView = new DeathRecapView(root, _recap);
            _recapButton = UiFactory.Button(root, "Özeti gizle", ToggleRecap);
            UiFactory.Anchor(_recapButton, UiAnchor.BottomLeft, new Vector2(320f, 40f), new Vector2(220f, 56f));

            // Sinematik kenar şeritleri + KILLCAM etiketi
            _letterTop = UiFactory.Panel(root, Color.black);
            UiFactory.Anchor(_letterTop, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(4000f, 80f));
            _letterBottom = UiFactory.Panel(root, Color.black);
            UiFactory.Anchor(_letterBottom, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, 80f));
            _letterTop.SetAsFirstSibling();
            _letterBottom.SetAsFirstSibling();
            _killcamTag = UiFactory.Label(root, "KILLCAM", 22, TextAnchor.MiddleLeft, UiTheme.EnemyRed, FontStyle.Bold);
            UiFactory.Anchor(_killcamTag, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -100f), new Vector2(600f, 30f));

            BuildTargetPanel(root);
            ApplyPhaseVisibility();
        }

        private void BuildTargetPanel(RectTransform root)
        {
            _targetPanel = UiFactory.Panel(root, UiTheme.PanelDark);
            UiFactory.Anchor(_targetPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(460f, 96f));
            _targetName = UiFactory.Label(_targetPanel, "", 20, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.Anchor(_targetName, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -8f), new Vector2(428f, 28f));
            var hpTrack = UiFactory.Panel(_targetPanel, UiTheme.Track);
            UiFactory.Anchor(hpTrack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -42f), new Vector2(428f, 12f));
            _targetHp = UiFactory.Image(hpTrack, null, UiTheme.HealthHigh);
            _targetHp.raycastTarget = false;
            UiFactory.Stretch(_targetHp);
            var arTrack = UiFactory.Panel(_targetPanel, UiTheme.Track);
            UiFactory.Anchor(arTrack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -58f), new Vector2(428f, 6f));
            _targetArmor = UiFactory.Image(arTrack, null, UiTheme.Armor);
            _targetArmor.raycastTarget = false;
            UiFactory.Stretch(_targetArmor);
            _targetStats = UiFactory.Label(_targetPanel, "", 15, TextAnchor.MiddleLeft, UiTheme.TextDim);
            UiFactory.Anchor(_targetStats, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -68f), new Vector2(428f, 22f));
        }

        private void ToggleRecap()
        {
            _recapShown = !_recapShown;
            ApplyPhaseVisibility();
        }

        private void ApplyPhaseVisibility()
        {
            if (_recapView != null) _recapView.SetVisible(_killcam || _recapShown);
            if (_recapButton != null)
            {
                UiFactory.SetVisible(_recapButton, !_killcam);
                UiFactory.SetButtonLabel(_recapButton, _recapShown ? "Özeti gizle" : "Ölüm özeti");
            }
            if (_letterTop != null) UiFactory.SetVisible(_letterTop, _killcam);
            if (_letterBottom != null) UiFactory.SetVisible(_letterBottom, _killcam);
            if (_killcamTag != null) UiFactory.SetVisible(_killcamTag, _killcam);
            if (_targetPanel != null) UiFactory.SetVisible(_targetPanel, !_killcam);
            if (_wipePanel != null) UiFactory.SetVisible(_wipePanel, !_killcam);
        }

        private void RefreshOrder()
        {
            _candidates.Clear();
            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !c.IsInitialized) continue;
                _candidates.Add(new SpectatorCandidate(c.Id.Value, c.Team, c.IsAlive));
            }

            SpectateRules.BuildTeamOrder(_candidates, _local != null ? _local.Team : 0, _local != null ? _local.Id.Value : -1, _order);
        }

        private void Update()
        {
            UiFactory.SetCursorFree(true);
            RefreshOrder();

            if (_killcam)
            {
                if (_replayCam != null)
                {
                    _replayCam.Tick(Time.deltaTime);
                }
                var replayDone = _replayCam != null && _replayCam.Finished;
                if (_replayCam != null ? (replayDone || KillcamSkipPressed()) : (Time.time - _startTime >= KillcamSeconds || KillcamSkipPressed()))
                {
                    EndReplayCam();
                    _killcam = false;
                    if (_camera != null && _origFov > 1f) _camera.fieldOfView = _origFov;
                    // Killcam sonrası öldüreni serbest kamerayla takip; sağ/sol ile takım arkadaşına geçilir.
                    _followKiller = _killerId >= 0 && IsAlive(_killerId);
                    _targetId = SpectateRules.Resolve(_order, -1, _followKiller, _killerId, _followKiller);
                    UpdateBanner();
                    ApplyPhaseVisibility();
                }
            }
            else
            {
                if (_followKiller && !IsAlive(_killerId)) _followKiller = false;
                var resolved = SpectateRules.Resolve(_order, _targetId, _followKiller, _killerId, _followKiller);
                if (resolved != _targetId)
                {
                    _targetId = resolved;
                    UpdateBanner();
                }

                if (_targetId < 0 && !_noTargetsReported)
                {
                    _noTargetsReported = true;
                    _onMatchEnd?.Invoke();
                    return;
                }

                HandleInput();
            }

            RefreshTargetPanel();
            CheckSquadWipe();
        }

        private bool KillcamSkipPressed()
        {
            if (Time.time - _startTime < 0.6f) return false; // yanlışlıkla atlamayı önle
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            return (kb != null && kb.spaceKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        }

        private void HandleInput()
        {
            // Duraklatma / harita / envanter / konsol açıkken Q-E hedef değiştirmez, fare kamerayı döndürmez.
            if (Time.timeScale <= 0f || UI.MapOverlayInput.IsAnyOpen || UI.OverlayState.TextInputActive)
                return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var dir = 0;
            if (kb != null)
            {
                if (kb.eKey.wasPressedThisFrame) dir = 1;
                else if (kb.qKey.wasPressedThisFrame) dir = -1;
            }

            if (mouse != null)
            {
                // Düğmelerin üstündeyken tıklama hedef değiştirmesin.
                var overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
                if (!overUi)
                {
                    if (mouse.leftButton.wasPressedThisFrame) dir = 1;
                    else if (mouse.rightButton.wasPressedThisFrame) dir = -1;
                }

                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.1f)
                    _distance = Mathf.Clamp(_distance - Mathf.Sign(scroll) * 0.6f, 2.5f, 9f);

                if (mouse.middleButton.isPressed)
                {
                    var d = mouse.delta.ReadValue();
                    _yaw += d.x * 0.15f;
                    _pitch = Mathf.Clamp(_pitch - d.y * 0.15f, -10f, 70f);
                }
            }

            if (dir != 0)
            {
                _followKiller = false;
                _targetId = SpectatorTargeting.Cycle(_order, _targetId, dir);
                UpdateBanner();
            }
        }

        private void UpdateBanner()
        {
            if (_banner == null) return;
            if (_killcam)
            {
                var k = FindTarget();
                UiFactory.SetText(_banner, k != null ? "ÖLDÜREN: " + k.RankedName : "ŞEHİT DÜŞTÜN");
                return;
            }

            var t = FindTarget();
            UiFactory.SetText(_banner, t != null ? "İZLENİYOR: " + t.RankedName : "İZLENECEK KİMSE KALMADI");
        }

        private static bool IsAlive(int id)
        {
            return id >= 0 && CombatantRegistry.TryGet(new PlayerId(id), out var c) && c != null && c.IsAlive;
        }

        private Combatant FindTarget()
        {
            return _targetId >= 0 && CombatantRegistry.TryGet(new PlayerId(_targetId), out var c) ? c : null;
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            var target = FindTarget();
            if (target != null)
                _lastFocus = target.transform.position + Vector3.up * 1.5f;

            if (_killcam)
            {
                if (_replayCam != null) return; // kamerayı tekrar killcam'i sürer
                LateUpdateKillcam(target);
                return;
            }

            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            var desired = _lastFocus - rot * Vector3.forward * _distance;
            // Duvar içine girmemesi için basit ışın.
            if (Physics.Linecast(_lastFocus, desired, out var hit, ~0, QueryTriggerInteraction.Ignore))
                desired = hit.point + (_lastFocus - desired).normalized * 0.2f;

            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, 1f - Mathf.Exp(-12f * Time.deltaTime));
            _camera.transform.rotation = Quaternion.LookRotation(_lastFocus - _camera.transform.position, Vector3.up);
        }

        /// <summary>Öldürenin gözünden (canlı) bakar, kurbana doğru döner; öldüren yoksa/ölürse son pozda kalır, yoksa leşin etrafında döner.</summary>
        private void LateUpdateKillcam(Combatant killer)
        {
            var victimPos = _local != null ? _local.transform.position + Vector3.up * 1.2f : _lastFocus;
            if (killer != null)
            {
                _killerEye = killer.EyePosition;
                var want = victimPos - _killerEye;
                if (want.sqrMagnitude > 0.01f)
                    _killerRot = Quaternion.Slerp(_hasKillerPose ? _killerRot : Quaternion.LookRotation(want), Quaternion.LookRotation(want), 1f - Mathf.Exp(-5f * Time.deltaTime));
                _hasKillerPose = true;
            }

            if (_hasKillerPose)
            {
                _camera.transform.SetPositionAndRotation(_killerEye, _killerRot);
                _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, 62f, 1f - Mathf.Exp(-3f * Time.deltaTime));
                return;
            }

            // Yedek: ölen oyuncunun etrafında yavaş yörünge.
            var t = Time.time - _startTime;
            var orbit = Quaternion.Euler(22f, t * 25f, 0f);
            var desired = victimPos - orbit * Vector3.forward * 4f;
            _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, 1f - Mathf.Exp(-6f * Time.deltaTime));
            _camera.transform.rotation = Quaternion.LookRotation(victimPos - _camera.transform.position, Vector3.up);
        }

        private void RefreshTargetPanel()
        {
            if (_targetPanel == null || _killcam) return;
            var t = FindTarget();
            if (t == null)
            {
                UiFactory.SetVisible(_targetPanel, false);
                return;
            }

            UiFactory.SetVisible(_targetPanel, true);
            var hs = t.State;
            var frac = hs.Normalized;
            UiFactory.SetText(_targetName, t.RankedName + (t.IsDowned ? "   [YARALI]" : string.Empty) + (t.Team == (_local != null ? _local.Team : -1) ? string.Empty : "   [DÜŞMAN]"));
            _targetHp.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            UiFactory.SetColor(_targetHp, frac > 0.5f ? UiTheme.HealthHigh : (frac > 0.25f ? UiTheme.HealthMid : UiTheme.HealthLow));

            var inv = t.Inventory;
            var armorFrac = 0f;
            if (inv != null)
            {
                var vest = inv.Vest;
                if (vest != null && !vest.IsBroken) armorFrac = 1f;
            }
            _targetArmor.rectTransform.anchorMax = new Vector2(armorFrac, 1f);

            var w = inv != null ? inv.ActiveWeapon : null;
            var weapon = w != null ? Project.Application.Catalogs.WeaponCatalog.GetDisplayName(w.WeaponId) + "  " + w.CurrentAmmo + "/" + w.MagazineSize : "Silahsız";
            UiFactory.SetText(_targetStats, Mathf.CeilToInt(hs.Current) + " CAN   ·   " + weapon);
        }

        private void CheckSquadWipe()
        {
            if (_wipeShown || _local == null || _local.Team < 0 || _canvas == null)
                return;

            var all = CombatantRegistry.All;
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.IsInitialized && c.Team == _local.Team && c.IsAlive)
                    return;
            }

            _wipeShown = true;
            BuildWipePanel();
        }

        private void BuildWipePanel()
        {
            int placement = 0, teams = 0, teamKills = 0;
            CombatantStats stats = null;
            try
            {
                var services = Project.Infrastructure.GameContext.Services;
                if (services != null && services.TryResolve<MatchService>(out var match) && match != null)
                {
                    teams = match.TeamCount;
                    placement = match.GetTeamPlacement(_local.Team);
                    if (placement <= 0) placement = match.AliveTeamCount + 1;
                    if (services.TryResolve<MatchStatsService>(out var ms) && ms != null)
                    {
                        stats = ms.Get(_local.Id);
                        teamKills = ms.GetTeamKills(_local.Team);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            var root = (RectTransform)_canvas.transform;
            _wipePanel = UiFactory.Panel(root, UiTheme.PanelDark);
            UiFactory.Anchor(_wipePanel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 20f), new Vector2(400f, 420f));
            UiFactory.AddOutline(_wipePanel.GetComponent<Image>(), UiTheme.AccentDark, 2f);

            var head = UiFactory.Label(_wipePanel, "TİMİN ELENDİ", 30, TextAnchor.MiddleCenter, UiTheme.EnemyRed, FontStyle.Bold);
            UiFactory.Anchor(head, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(360f, 42f));
            var place = UiFactory.Label(_wipePanel, DeathRecapText.Placement(placement, teams), 22, TextAnchor.MiddleCenter, UiTheme.TextHeader, FontStyle.Bold);
            UiFactory.Anchor(place, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(360f, 32f));

            var rows = new[]
            {
                new[] { "Etkisiz bırakma", (stats != null ? stats.Kills : 0).ToString() },
                new[] { "Tim toplamı", teamKills.ToString() },
                new[] { "Verilen hasar", Mathf.RoundToInt(stats != null ? stats.DamageDealt : 0f).ToString() },
                new[] { "Kafadan isabet", (stats != null ? stats.Headshots : 0).ToString() },
                new[] { "İsabet oranı", "%" + Mathf.RoundToInt((stats != null ? stats.Accuracy : 0f) * 100f) },
                new[] { "Hayatta kalma", DeathRecapText.Survival(stats != null ? stats.SurvivalSeconds : 0f) },
            };
            var y = 110f;
            for (var i = 0; i < rows.Length; i++)
            {
                var l = UiFactory.Label(_wipePanel, rows[i][0], 18, TextAnchor.MiddleLeft, UiTheme.TextDim);
                UiFactory.Anchor(l, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -y), new Vector2(220f, 28f));
                var v = UiFactory.Label(_wipePanel, rows[i][1], 22, TextAnchor.MiddleRight, UiTheme.Text, FontStyle.Bold);
                UiFactory.Anchor(v, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -y), new Vector2(140f, 28f));
                y += 36f;
            }

            var note = UiFactory.Label(_wipePanel, "Maçı izlemeye devam edebilirsin.", 15, TextAnchor.MiddleCenter, UiTheme.TextMuted);
            UiFactory.Anchor(note, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(360f, 24f));
            UiFactory.SetVisible(_wipePanel, !_killcam);
        }

        private void EndReplayCam()
        {
            if (_replayCam == null) return;
            _replayCam.Dispose();
            _replayCam = null;
        }

        private void OnDestroy()
        {
            EndReplayCam();
            PostProcessing.SetDeathBlur(false);
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }
    }
}
