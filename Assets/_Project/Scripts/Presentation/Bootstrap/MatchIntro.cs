using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Localization;
using Project.Infrastructure.Transport;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.Bootstrap
{
    /// <summary>
    /// Maç girişi / zafer sinematiği için saf zaman çizelgesi (Unity nesnesi yok; testlenebilir).
    /// 8 sn: çekim 1 (0–3) T-70 uçuş takibi, çekim 2 (3–5,5) harita panoraması + yazı, çekim 3 (5,5–8) tim dizilişi.
    /// </summary>
    public static class MatchIntroTimeline
    {
        public const float Duration = 8f;
        public const float Shot1End = 3f;
        public const float Shot2End = 5.5f;
        public const float StartFade = 0.5f;
        public const float FinalFade = 0.35f;
        public const float CutDip = 0.14f;
        public const float LetterboxIn = 0.6f;
        public const float CardStagger = 0.35f;
        public const float CardPopSeconds = 0.4f;
        public const int MaxCards = 5;

        /// <summary>0 = uçuş takibi, 1 = harita panoraması, 2 = tim dizilişi.</summary>
        public static int ShotAt(float t) => t < Shot1End ? 0 : (t < Shot2End ? 1 : 2);

        /// <summary>Çekim içindeki 0..1 ilerleme.</summary>
        public static float ShotProgress(float t)
        {
            switch (ShotAt(t))
            {
                case 0: return Mathf.Clamp01(t / Shot1End);
                case 1: return Mathf.Clamp01((t - Shot1End) / (Shot2End - Shot1End));
                default: return Mathf.Clamp01((t - Shot2End) / (Duration - Shot2End));
            }
        }

        public static float EaseInOut(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        public static float EaseOutCubic(float x)
        {
            x = Mathf.Clamp01(x);
            var u = 1f - x;
            return 1f - u * u * u;
        }

        /// <summary>Hafif aşan yerleşme eğrisi (kart çıkışı). 0 girişte 0, 1 girişte 1.</summary>
        public static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = x - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Letterbox çubuklarının kapanma oranı (0..1).</summary>
        public static float Letterbox(float t) => EaseInOut(t / LetterboxIn);

        /// <summary>Siyah örtü: açılış kararması, kesme geçişleri ve son kararma (0..1).</summary>
        public static float Darkness(float t)
        {
            var d = Mathf.Clamp01(1f - t / StartFade);
            d = Mathf.Max(d, Dip(t, Shot1End));
            d = Mathf.Max(d, Dip(t, Shot2End));
            d = Mathf.Max(d, Mathf.Clamp01((t - (Duration - FinalFade)) / FinalFade));
            return d;
        }

        private static float Dip(float t, float boundary)
        {
            var d = Mathf.Abs(t - boundary);
            return d < CutDip ? 1f - d / CutDip : 0f;
        }

        /// <summary>Kartın giriş eğrisi (i. kart, sırayla). Başlamadan 0, sonra 0..~1,07..1.</summary>
        public static float CardPop(float t, int index)
        {
            var start = Shot2End + 0.25f + Mathf.Max(0, index) * CardStagger;
            return EaseOutBack((t - start) / CardPopSeconds);
        }

        /// <summary>Yazı zarfı: giriş, bekleme, çıkış (hepsi yumuşatılmış) → 0..1.</summary>
        public static float Window(float t, float start, float inSeconds, float holdSeconds, float outSeconds)
        {
            if (t < start)
                return 0f;
            var a = EaseOutCubic((t - start) / Mathf.Max(0.0001f, inSeconds));
            var outStart = start + inSeconds + holdSeconds;
            if (t <= outStart)
                return a;
            return a * (1f - EaseInOut((t - outStart) / Mathf.Max(0.0001f, outSeconds)));
        }

        /// <summary>Boşluk ile geçildiğinde zamanın atlayacağı yer: yalnızca son kararma kalır.</summary>
        public static float SkipTarget(float t) => Mathf.Max(t, Duration - FinalFade);

        /// <summary>Yakın çekimde gösterilecek kart sayısı.</summary>
        public static int CardCount(int soldiers) => Mathf.Clamp(soldiers, 0, MaxCards);
    }

    /// <summary>
    /// CoD tarzı maç girişi: T-70 helikopter takibi → harita panoraması (harita adı + mod yazısı) → tim yakın çekimi
    /// (rütbe/ad kartları tek tek belirir) → normal intikal kamerasına devir. Boşluk ile geçilir; letterbox çubukları
    /// vardır, HUD gizlenir. Zaferde kazanan timin yakın çekimi + "ZAFER" yazısı (<see cref="PlayVictory"/>).
    /// <para>Kendi kamerasını (ana kameranın üstünde) kullanır; ana kameraya dokunmaz. Ölçeksiz zamanla çalışır.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchIntro : MonoBehaviour
    {
        /// <summary>Tuval sırası: HUD'un üstünde, maç sonu ekranının (70) altında.</summary>
        public const int SortOrder = 65;

        private const float LetterboxHeight = 132f;
        private static readonly Color Gold = new Color(0.89f, 0.72f, 0.29f, 1f);

        private sealed class Subject
        {
            public Transform Body;
            public float HeadHeight = 1.3f;
            public string Name;
            public string Rank;
            public RectTransform Card;
            public CanvasGroup Group;
        }

        private static MatchIntro _current;

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _topBar;
        private RectTransform _bottomBar;
        private Image _dark;
        private Text _hint;
        private Camera _cam;
        private Action<bool> _setHud;
        private bool _victory;
        private bool _ending;
        private float _t;
        private float _outro;
        private TransportVehicle _heli;
        private Transform _fallbackFocus;
        private Vector3 _mapCenter;
        private readonly List<Subject> _subjects = new List<Subject>(MatchIntroTimeline.MaxCards);

        private RectTransform _mapTitle;
        private CanvasGroup _mapTitleGroup;
        private RectTransform _captionRect;
        private CanvasGroup _captionGroup;
        private RectTransform _victoryRect;
        private CanvasGroup _victoryGroup;

        public static bool IsPlaying => _current != null;

        // ------------------------------------------------------------------ Giriş noktaları

        /// <summary>Maç girişi sinematiğini başlatır. Başlatılamazsa (kamera/helikopter yok) sessizce geçer.</summary>
        /// <param name="mapName">Harita adı.</param>
        /// <param name="modeName">Mod yazısı (boşsa varsayılan).</param>
        /// <param name="setHudVisible">HUD'u göster/gizle (null olabilir).</param>
        public static MatchIntro Play(string mapName, string modeName, Action<bool> setHudVisible)
        {
            if (TransportVehicle.IsHeadless || Camera.main == null)
                return null;

            DestroyCurrent();
            var go = new GameObject("[MatchIntro]");
            var intro = go.AddComponent<MatchIntro>();
            if (!intro.Begin(mapName, modeName, setHudVisible, false, null))
            {
                Destroy(go);
                return null;
            }

            _current = intro;
            return intro;
        }

        /// <summary>Zafer sinematiği: kazanan timin canlı üyeleri için yakın çekim ve "ZAFER" yazısı (MaçSonu ekranının altında kalır).</summary>
        public static MatchIntro PlayVictory(string teamName)
        {
            if (TransportVehicle.IsHeadless || Camera.main == null)
                return null;

            DestroyCurrent();
            var go = new GameObject("[ZaferSinematik]");
            var intro = go.AddComponent<MatchIntro>();
            if (!intro.Begin(null, null, null, true, teamName))
            {
                Destroy(go);
                return null;
            }

            _current = intro;
            return intro;
        }

        public static void DestroyCurrent()
        {
            if (_current != null)
                Destroy(_current.gameObject);
            _current = null;
        }

        // ------------------------------------------------------------------ Kurulum

        private bool Begin(string mapName, string modeName, Action<bool> setHud, bool victory, string teamName)
        {
            _victory = victory;
            _setHud = setHud;

            var main = Camera.main;
            var local = CombatantRegistry.LocalPlayer;
            var team = local != null ? local.Team : 0;

            _heli = TransportVehicle.FindForTeam(team);
            _fallbackFocus = local != null ? local.transform : null;
            if (_heli == null && _fallbackFocus == null && !victory)
                return false;

            CollectSubjects(team, victory);
            if (victory && _subjects.Count == 0)
                return false;

            BuildCamera(main);
            BuildUi(mapName, modeName, teamName);

            _mapCenter = new Vector3(0f, 0f, 0f);
            if (_setHud != null && !victory)
                _setHud(false);
            return true;
        }

        private void CollectSubjects(int team, bool victory)
        {
            var alive = new List<Combatant>(16);
            CombatantRegistry.GetAliveTeam(team, alive);
            if (alive.Count == 0)
                return;

            var anchor = _heli != null ? _heli.transform.position : alive[0].transform.position;
            var local = CombatantRegistry.LocalPlayer;
            if (local != null && local.Team == team)
                anchor = local.transform.position;

            alive.Sort((a, b) =>
                (a.transform.position - anchor).sqrMagnitude.CompareTo((b.transform.position - anchor).sqrMagnitude));

            var count = MatchIntroTimeline.CardCount(alive.Count);
            for (var i = 0; i < count; i++)
            {
                var c = alive[i];
                var s = new Subject
                {
                    Body = c.transform,
                    Name = string.IsNullOrWhiteSpace(c.DisplayName) ? "Asker" : c.DisplayName.Trim(),
                    Rank = RankCatalog.GetName(c.Rank)
                };
                s.HeadHeight = MeasureHead(c.transform);
                _subjects.Add(s);
            }

            // Kartlar soldan sağa belirsin (ekranda yan yana dursun).
            _subjects.Sort((a, b) => a.Body.position.x.CompareTo(b.Body.position.x));
        }

        private static float MeasureHead(Transform body)
        {
            var renderers = body.GetComponentsInChildren<Renderer>();
            var top = float.MinValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i] is ParticleSystemRenderer)
                    continue;
                top = Mathf.Max(top, renderers[i].bounds.max.y);
            }

            if (top <= float.MinValue)
                return 1.3f;
            return Mathf.Clamp(top - body.position.y + 0.15f, 0.8f, 2.3f);
        }

        private void BuildCamera(Camera main)
        {
            var go = new GameObject("IntroCamera");
            go.transform.SetParent(transform, false);
            _cam = go.AddComponent<Camera>();
            _cam.CopyFrom(main);
            _cam.depth = main.depth + 10f;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.fieldOfView = 55f;
            _cam.targetTexture = null;
            var listener = go.GetComponent<AudioListener>();
            if (listener != null)
                Destroy(listener);
            PlaceCamera();
        }

        private void BuildUi(string mapName, string modeName, string teamName)
        {
            _canvas = UiFactory.CreateCanvas(_victory ? "[Zafer Sinematik]" : "[Maç Girişi]", SortOrder);
            _canvas.transform.SetParent(transform, false);
            var gr = _canvas.GetComponent<GraphicRaycaster>();
            if (gr != null)
                gr.enabled = false;
            _root = (RectTransform)_canvas.transform;

            // Kartlar (kamera üstünde, çubukların altında).
            for (var i = 0; i < _subjects.Count; i++)
                BuildCard(_subjects[i]);

            if (_victory)
                BuildVictoryTitle(teamName);
            else
                BuildIntroTitles(mapName, modeName);

            _topBar = BarImage("LetterboxTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            _bottomBar = BarImage("LetterboxBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));

            var dark = UiFactory.Image(_root, null, Color.black);
            dark.gameObject.name = "Dark";
            dark.raycastTarget = false;
            UiFactory.Stretch(dark);
            _dark = dark;

            if (!_victory)
            {
                _hint = UiFactory.Label(_root, Loc.Get("intro.skip", "BOŞLUK  ·  GEÇ"), UiTheme.FontSmall, TextAnchor.LowerRight,
                    new Color(1f, 1f, 1f, 0.55f), FontStyle.Bold);
                _hint.raycastTarget = false;
                UiFactory.Anchor(_hint, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-48f, 28f), new Vector2(320f, 40f));
            }

            ApplyUi();
        }

        private RectTransform BarImage(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var img = UiFactory.Image(_root, null, Color.black);
            img.gameObject.name = name;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);
            return rt;
        }

        private void BuildCard(Subject s)
        {
            var card = UiFactory.CreateRect("Kart_" + s.Name, _root);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0f);
            card.sizeDelta = new Vector2(320f, 84f);

            var bg = UiFactory.Image(card, null, new Color(0.06f, 0.07f, 0.045f, 0.82f));
            bg.raycastTarget = false;
            UiFactory.Stretch(bg);

            var stripe = UiFactory.Image(card, null, _victory ? Gold : UiTheme.Accent);
            stripe.raycastTarget = false;
            UiFactory.SetRect(stripe, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f));

            var name = UiFactory.Label(card, s.Name.ToUpperInvariant(), UiTheme.FontLarge - 4, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            name.raycastTarget = false;
            UiFactory.SetRect(name, new Vector2(0f, 0.45f), new Vector2(1f, 1f), new Vector2(22f, 0f), new Vector2(-10f, -4f));

            var rank = UiFactory.Label(card, s.Rank.ToUpperInvariant(), UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            rank.raycastTarget = false;
            UiFactory.SetRect(rank, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(22f, 4f), new Vector2(-10f, 0f));

            s.Card = card;
            s.Group = UiFactory.EnsureCanvasGroup(card);
            s.Group.alpha = 0f;
            s.Group.blocksRaycasts = false;
        }

        private void BuildIntroTitles(string mapName, string modeName)
        {
            var map = string.IsNullOrWhiteSpace(mapName) ? "KUZGUN VADİSİ" : mapName.Trim().ToUpperInvariant();
            var mode = string.IsNullOrWhiteSpace(modeName) ? Loc.Get("intro.mode", "TİM HAREKÂTI  ·  SON TİM AYAKTA") : modeName.Trim().ToUpperInvariant();

            _mapTitle = UiFactory.CreateRect("HaritaYazisi", _root);
            _mapTitle.anchorMin = new Vector2(0f, 0.5f);
            _mapTitle.anchorMax = new Vector2(0f, 0.5f);
            _mapTitle.pivot = new Vector2(0f, 0.5f);
            _mapTitle.sizeDelta = new Vector2(1300f, 220f);
            _mapTitleGroup = UiFactory.EnsureCanvasGroup(_mapTitle);

            var line = UiFactory.Image(_mapTitle, null, UiTheme.Accent);
            line.raycastTarget = false;
            UiFactory.SetRect(line, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(8f, 0f));

            var title = UiFactory.Label(_mapTitle, map, 118, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            title.raycastTarget = false;
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
            UiFactory.SetRect(title, new Vector2(0f, 0.38f), new Vector2(1f, 1f), new Vector2(32f, 0f), Vector2.zero);

            var sub = UiFactory.Label(_mapTitle, mode, UiTheme.FontLarge, TextAnchor.MiddleLeft, UiTheme.Amber, FontStyle.Bold);
            sub.raycastTarget = false;
            UiFactory.SetRect(sub, new Vector2(0f, 0f), new Vector2(1f, 0.38f), new Vector2(36f, 0f), Vector2.zero);

            _captionRect = UiFactory.CreateRect("Alt", _root);
            _captionRect.anchorMin = _captionRect.anchorMax = new Vector2(0f, 0f);
            _captionRect.pivot = new Vector2(0f, 0f);
            _captionRect.sizeDelta = new Vector2(900f, 60f);
            _captionGroup = UiFactory.EnsureCanvasGroup(_captionRect);
            var cap = UiFactory.Label(_captionRect, Loc.Get("intro.caption", "T-70  ·  İNTİKAL BAŞLIYOR"), UiTheme.FontMedium,
                TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.9f), FontStyle.Bold);
            cap.raycastTarget = false;
            UiFactory.Stretch(cap);
        }

        private void BuildVictoryTitle(string teamName)
        {
            _victoryRect = UiFactory.CreateRect("Zafer", _root);
            _victoryRect.anchorMin = _victoryRect.anchorMax = new Vector2(0.5f, 0.5f);
            _victoryRect.pivot = new Vector2(0.5f, 0.5f);
            _victoryRect.sizeDelta = new Vector2(1500f, 300f);
            _victoryGroup = UiFactory.EnsureCanvasGroup(_victoryRect);

            var title = UiFactory.Label(_victoryRect, Loc.Get("intro.victory", "ZAFER"), 240, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            title.raycastTarget = false;
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);
            UiFactory.SetRect(title, new Vector2(0f, 0.25f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var team = string.IsNullOrWhiteSpace(teamName) ? "KARTAL TİMİ" : teamName.Trim().ToUpperInvariant();
            var sub = UiFactory.Label(_victoryRect, team, UiTheme.FontTitle - 8, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            sub.raycastTarget = false;
            UiFactory.SetRect(sub, new Vector2(0f, 0f), new Vector2(1f, 0.25f), Vector2.zero, Vector2.zero);
        }

        // ------------------------------------------------------------------ Döngü

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (_cam == null || _root == null)
            {
                Finish(true);
                return;
            }

            if (_victory)
            {
                _t += dt;
                PlaceCamera();
                ApplyUi();
                return;
            }

            if (_ending)
            {
                _outro += dt;
                ApplyOutro();
                if (_outro >= 0.4f)
                    Finish(false);
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
                _t = MatchIntroTimeline.SkipTarget(_t);

            _t += dt;
            if (_t >= MatchIntroTimeline.Duration)
            {
                BeginOutro();
                return;
            }

            PlaceCamera();
            ApplyUi();
        }

        private void BeginOutro()
        {
            _ending = true;
            _outro = 0f;
            // Kamerayı bırak: normal intikal kamerası görünür; siyahtan açılarak devir olur.
            if (_cam != null)
                _cam.enabled = false;
            for (var i = 0; i < _subjects.Count; i++)
                if (_subjects[i].Group != null)
                    _subjects[i].Group.alpha = 0f;
            if (_mapTitleGroup != null) _mapTitleGroup.alpha = 0f;
            if (_captionGroup != null) _captionGroup.alpha = 0f;
            if (_hint != null) _hint.enabled = false;
            if (_setHud != null)
                _setHud(true);
            ApplyOutro();
        }

        private void ApplyOutro()
        {
            var k = Mathf.Clamp01(_outro / 0.4f);
            SetBars(1f - MatchIntroTimeline.EaseInOut(k));
            SetDark(1f - MatchIntroTimeline.EaseOutCubic(k));
        }

        private void Finish(bool restoreHud)
        {
            if (restoreHud && !_victory && _setHud != null)
                _setHud(true);
            if (_current == this)
                _current = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;
        }

        // ------------------------------------------------------------------ Kamera

        private void PlaceCamera()
        {
            if (_cam == null)
                return;

            Vector3 pos;
            Quaternion rot;
            if (_victory)
                VictoryPose(out pos, out rot);
            else
                switch (MatchIntroTimeline.ShotAt(_t))
                {
                    case 0: FlyoverPose(out pos, out rot); break;
                    case 1: VistaPose(out pos, out rot); break;
                    default: LineupPose(out pos, out rot); break;
                }

            pos = ClampAboveTerrain(pos, 1.2f);
            _cam.transform.SetPositionAndRotation(pos, rot);
        }

        private Vector3 FocusPoint()
        {
            if (_heli != null)
                return _heli.transform.position;
            if (_fallbackFocus != null)
                return _fallbackFocus.position;
            return _subjects.Count > 0 && _subjects[0].Body != null ? _subjects[0].Body.position : Vector3.zero;
        }

        private Vector3 FocusForward()
        {
            var tr = _heli != null ? _heli.transform : _fallbackFocus;
            if (tr == null)
                return Vector3.forward;
            var f = tr.forward;
            f.y = 0f;
            return f.sqrMagnitude < 0.01f ? Vector3.forward : f.normalized;
        }

        /// <summary>Çekim 1: helikopteri arkadan yandan sarmalayarak takip.</summary>
        private void FlyoverPose(out Vector3 pos, out Quaternion rot)
        {
            var e = MatchIntroTimeline.EaseInOut(MatchIntroTimeline.ShotProgress(_t));
            var focus = FocusPoint();
            var fwd = FocusForward();
            var azimuth = Mathf.Lerp(25f, 95f, e);
            var dist = Mathf.Lerp(32f, 22f, e);
            var height = Mathf.Lerp(7f, 2.5f, e);
            var dir = Quaternion.AngleAxis(azimuth, Vector3.up) * -fwd;
            pos = focus + dir * dist + Vector3.up * height;
            rot = Quaternion.LookRotation((focus + Vector3.up * 0.8f) - pos, Vector3.up);
            _cam.fieldOfView = Mathf.Lerp(50f, 42f, e);
        }

        /// <summary>Çekim 2: helikopter üstünden harita merkezine doğru süpürerek panorama.</summary>
        private void VistaPose(out Vector3 pos, out Quaternion rot)
        {
            var e = MatchIntroTimeline.EaseInOut(MatchIntroTimeline.ShotProgress(_t));
            var focus = FocusPoint();
            pos = focus + Vector3.up * 16f - FocusForward() * 6f;
            var toCenter = _mapCenter - pos;
            toCenter.y = 0f;
            var baseYaw = toCenter.sqrMagnitude < 1f ? Mathf.Atan2(FocusForward().x, FocusForward().z) * Mathf.Rad2Deg
                : Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
            var yaw = baseYaw - 28f + 56f * e;
            rot = Quaternion.Euler(Mathf.Lerp(-10f, -4f, e), yaw, 0f);
            _cam.fieldOfView = Mathf.Lerp(62f, 52f, e);
        }

        /// <summary>Çekim 3: tim üyelerinin yan yakın çekimi (yavaş yaklaşma).</summary>
        private void LineupPose(out Vector3 pos, out Quaternion rot)
        {
            var e = MatchIntroTimeline.EaseInOut(MatchIntroTimeline.ShotProgress(_t));
            SquadFrame(out var center, out var spread);
            var side = Vector3.Cross(Vector3.up, FocusForward());
            if (side.sqrMagnitude < 0.01f)
                side = Vector3.right;
            side.Normalize();

            var dist = Mathf.Lerp(Mathf.Max(6f, spread + 5f), Mathf.Max(4f, spread + 3f), e);
            pos = center + side * dist + Vector3.up * 0.7f;
            rot = Quaternion.LookRotation((center + Vector3.up * 0.9f) - pos, Vector3.up);
            _cam.fieldOfView = Mathf.Lerp(48f, 38f, e);
        }

        /// <summary>Zafer: kazanan tim etrafında yavaş yörünge, hafif aşağıdan bakış.</summary>
        private void VictoryPose(out Vector3 pos, out Quaternion rot)
        {
            SquadFrame(out var center, out var spread);
            var ease = MatchIntroTimeline.EaseOutCubic(_t / 1.6f);
            var angle = 150f + _t * 7f;
            var dist = Mathf.Lerp(Mathf.Max(9f, spread + 8f), Mathf.Max(5f, spread + 3.5f), ease);
            var dir = Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward;
            pos = center + dir * dist + Vector3.up * Mathf.Lerp(3.2f, 0.9f, ease);
            rot = Quaternion.LookRotation((center + Vector3.up * 1.1f) - pos, Vector3.up);
            _cam.fieldOfView = Mathf.Lerp(52f, 40f, ease);
        }

        private void SquadFrame(out Vector3 center, out float spread)
        {
            var sum = Vector3.zero;
            var n = 0;
            for (var i = 0; i < _subjects.Count; i++)
            {
                if (_subjects[i].Body == null)
                    continue;
                sum += _subjects[i].Body.position;
                n++;
            }

            center = n > 0 ? sum / n : FocusPoint();
            spread = 0f;
            for (var i = 0; i < _subjects.Count; i++)
            {
                if (_subjects[i].Body == null)
                    continue;
                spread = Mathf.Max(spread, Vector3.Distance(_subjects[i].Body.position, center));
            }
        }

        private static Vector3 ClampAboveTerrain(Vector3 p, float clearance)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return p;
            var h = terrain.SampleHeight(p) + terrain.transform.position.y + clearance;
            if (p.y < h)
                p.y = h;
            return p;
        }

        // ------------------------------------------------------------------ Arayüz

        private void ApplyUi()
        {
            if (_victory)
                ApplyVictoryUi();
            else
                ApplyIntroUi();
        }

        private void ApplyIntroUi()
        {
            SetBars(MatchIntroTimeline.Letterbox(_t));
            SetDark(MatchIntroTimeline.Darkness(_t));

            // Harita adı: 3,3 sn'de kayarak girer, 5,2 sn'de çıkar.
            var env = MatchIntroTimeline.Window(_t, 3.25f, 0.7f, 1.15f, 0.45f);
            if (_mapTitle != null)
            {
                _mapTitleGroup.alpha = env;
                _mapTitle.anchoredPosition = new Vector2(120f - 220f * (1f - env), 40f);
            }

            var cap = MatchIntroTimeline.Window(_t, 0.7f, 0.6f, 1.5f, 0.5f);
            if (_captionRect != null)
            {
                _captionGroup.alpha = cap;
                _captionRect.anchoredPosition = new Vector2(120f - 60f * (1f - cap), LetterboxHeight + 40f);
            }

            UpdateCards(_t, 1f);
        }

        private void ApplyVictoryUi()
        {
            SetBars(MatchIntroTimeline.EaseInOut(_t / 0.8f));
            SetDark(Mathf.Clamp01(1f - _t / 0.5f));

            // "ZAFER" yazısı Maç Sonu ekranı belirirken (3,5 sn civarı) çekilir.
            var env = MatchIntroTimeline.Window(_t, 0.3f, 0.8f, 2.2f, 0.9f);
            if (_victoryRect != null)
            {
                _victoryGroup.alpha = env;
                var scale = Mathf.Lerp(1.35f, 1f, MatchIntroTimeline.EaseOutCubic((_t - 0.3f) / 0.9f));
                _victoryRect.localScale = new Vector3(scale, scale, 1f);
                _victoryRect.anchoredPosition = new Vector2(0f, 180f);
            }

            // Kartlar belirir, uzun süre kalıp Maç Sonu içeriği oturunca sönmeye başlar.
            var fade = 1f - MatchIntroTimeline.EaseInOut((_t - 3.0f) / 1.2f);
            UpdateCards(_t - 2.5f, fade);
        }

        private void SetBars(float k)
        {
            var h = LetterboxHeight * Mathf.Clamp01(k);
            if (_topBar != null) _topBar.sizeDelta = new Vector2(0f, h);
            if (_bottomBar != null) _bottomBar.sizeDelta = new Vector2(0f, h);
        }

        private void SetDark(float a)
        {
            if (_dark != null)
                _dark.color = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
        }

        /// <summary>Kartları sıraya göre belirtir; kart zamanı 'cardTime' ile (giriş: t, zafer: kaydırılmış).</summary>
        private void UpdateCards(float cardTime, float fade)
        {
            if (_cam == null)
                return;

            for (var i = 0; i < _subjects.Count; i++)
            {
                var s = _subjects[i];
                if (s.Card == null || s.Body == null)
                    continue;

                var pop = MatchIntroTimeline.CardPop(cardTime, i);
                var alpha = Mathf.Clamp01(pop) * fade;
                var head = s.Body.position + Vector3.up * s.HeadHeight;
                var sp = _cam.WorldToScreenPoint(head);
                if (sp.z <= 0.1f || alpha <= 0.001f)
                {
                    s.Group.alpha = 0f;
                    continue;
                }

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, new Vector2(sp.x, sp.y), null, out var local))
                {
                    var stagger = (i & 1) == 1 ? 58f : 0f;
                    s.Card.anchoredPosition = local + new Vector2(0f, 46f + stagger);
                }

                s.Group.alpha = alpha;
                var sc = Mathf.Max(0.01f, pop);
                s.Card.localScale = new Vector3(sc, sc, 1f);
            }
        }
    }
}
