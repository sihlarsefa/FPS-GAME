using System;
using System.Collections.Generic;
using Project.Application.Catalogs;
using Project.Application.Dialogue;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.Dialogue;
using Project.Infrastructure.Audio.HdrMix;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Presentation.UI.Lobby
{
    /// <summary>
    /// PUBG/CoD tarzı maç öncesi lobi akışı: TİM TOPLANIYOR (9 bot kartı tek tek) → HARİTA BRİFİNGİ (önizleme + intikal
    /// koridoru + 3 kural) → "İNTİKALE HAZIR OL" 5 sn geri sayım → tamamlanınca <c>onDone</c> (mevcut yükleme/MatchIntro zinciri).
    /// Zamanlama saf <see cref="LobbyFlowRules"/>'tadır. Boşluk ile evre atlanır. Menü sahnesinde, ölçeksiz zamanla çalışır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyFlow : MonoBehaviour
    {
        public const int SortOrder = 90;

        /// <summary>Kapatılırsa akış atlanır (otomasyon/test).</summary>
        public static bool Enabled = true;

        /// <summary>
        /// Bir bot katıldığında (ad, sıra 0..8, rol). ENTEGRASYON: Infrastructure/Audio/Dialogue API'si bu olaya telsiz
        /// selamlama repliği bağlayabilir.
        /// </summary>
        public static event Action<string, int, TeamRole> BotJoined;

        private static LobbyFlow _current;
        private static bool _passThrough;

        private static readonly string[] Names =
        {
            "Yılmaz", "Demir", "Kaya", "Çelik", "Aydın", "Koç", "Arslan", "Polat", "Şahin"
        };

        private static readonly MilitaryRank[] Ranks =
        {
            MilitaryRank.Ustegmen, MilitaryRank.UzmanCavus, MilitaryRank.UzmanOnbasi, MilitaryRank.Cavus,
            MilitaryRank.Onbasi, MilitaryRank.SozlesmeliEr, MilitaryRank.Er, MilitaryRank.UzmanOnbasi, MilitaryRank.Onbasi
        };

        private static readonly TeamRole[] Roles =
        {
            TeamRole.Leader, TeamRole.Rifleman, TeamRole.Marksman, TeamRole.MachineGunner, TeamRole.Medic,
            TeamRole.Radioman, TeamRole.Grenadier, TeamRole.Rifleman, TeamRole.Rifleman
        };

        private static readonly Color Gold = new Color(0.89f, 0.72f, 0.29f, 1f);

        private Action _onDone;
        private float _t;
        private float _prevT;
        private int _announced;
        private bool _finished;

        private CanvasGroup _gatherGroup;
        private CanvasGroup _briefGroup;
        private Text _gatherCount;
        private readonly RectTransform[] _cards = new RectTransform[LobbyFlowRules.Teammates];
        private readonly CanvasGroup[] _cardGroups = new CanvasGroup[LobbyFlowRules.Teammates];
        private readonly CanvasGroup[] _bullets = new CanvasGroup[3];
        private CanvasGroup _countGroup;
        private Text _countText;
        private Text _hint;

        /// <summary>
        /// Akışı başlatır ve true döner (çağıran dönmeli; bitince <paramref name="onDone"/> çağrılır). Akış devre dışı,
        /// toplu mod ya da kurulum hatasında false döner (çağıran normal devam eder). Tek satırlık menü kancası.
        /// </summary>
        public static bool TryRun(Action onDone)
        {
            if (_passThrough || onDone == null)
                return false;
            if (!Enabled || _current != null || !UnityEngine.Application.isPlaying || UnityEngine.Application.isBatchMode)
                return false;
            try
            {
                var go = new GameObject("LobbyFlow");
                var flow = go.AddComponent<LobbyFlow>();
                flow._onDone = onDone;
                flow.Build();
                _current = flow;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        private void Update()
        {
            if (_finished)
                return;
            _prevT = _t;
            _t += Time.unscaledDeltaTime;

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                _t = Mathf.Max(_t, LobbyFlowRules.SkipTarget(_t));
                UiSounds.Play(UiSfx.Press);
            }

            Tick();
            if (LobbyFlowRules.IsDone(_t))
                Finish();
        }

        private void Tick()
        {
            var joined = LobbyFlowRules.JoinedCount(_t);
            while (_announced < joined)
            {
                var i = _announced++;
                UiSounds.Play(UiSfx.Tab);
                try { BotJoined?.Invoke(Names[i], i, Roles[i]); }
                catch (Exception e) { Debug.LogException(e); }
                GreetOnJoin(i, Roles[i]);
            }

            if (_gatherCount != null)
                _gatherCount.text = (joined + 1) + " / 10 ASKER HAZIR";
            for (var i = 0; i < _cards.Length; i++)
            {
                var p = LobbyFlowRules.CardPop(_t, i);
                _cardGroups[i].alpha = Mathf.Clamp01(p * 1.5f);
                var s = Mathf.LerpUnclamped(0.7f, 1f, p);
                _cards[i].localScale = new Vector3(s, s, 1f);
                var a = _cards[i].anchoredPosition;
                a.x = Mathf.LerpUnclamped(-120f, 0f, p) + _cardBaseX[i];
                _cards[i].anchoredPosition = new Vector2(a.x, _cardBaseY[i]);
            }

            _gatherGroup.alpha = LobbyFlowRules.PhaseAlpha(_t, LobbyPhase.Gather);
            _briefGroup.alpha = LobbyFlowRules.PhaseAlpha(_t, LobbyPhase.Briefing);
            for (var i = 0; i < _bullets.Length; i++)
                _bullets[i].alpha = LobbyFlowRules.BulletAlpha(_t, i);

            var n = LobbyFlowRules.CountdownNumber(_t);
            _countGroup.alpha = n > 0 ? 1f : 0f;
            if (n > 0)
            {
                _countText.text = n.ToString();
                var beat = LobbyFlowRules.CountdownBeat(_t);
                var sc = 1f + 0.35f * (1f - beat) * (1f - beat);
                _countText.rectTransform.localScale = new Vector3(sc, sc, 1f);
                if (LobbyFlowRules.CountdownTickCrossed(_prevT, _t))
                    UiSounds.Play(n == 1 ? UiSfx.CountdownGo : UiSfx.CountdownTick);
            }
        }

        private readonly float[] _cardBaseX = new float[LobbyFlowRules.Teammates];
        private readonly float[] _cardBaseY = new float[LobbyFlowRules.Teammates];

        // ------------------------------------------------------------------ telsiz selamı

        private const string GreetCategory = "radio_check"; // dialogue_lines_v2.csv: "Telsiz kontrol, sesim geliyor mu?" vb.
        private AudioSource _greetSource;
        private float _greetBusyUntil;
        private readonly List<DialogueLine> _greetScratch = new List<DialogueLine>(8);

        /// <summary>
        /// Bot katılınca telsiz selamı. Menüde savaşan (Combatant) ve maç bağlamı olmadığından <c>DialogueDirector.Say</c> çalışmaz;
        /// bunun yerine diyalog v2'nin herkese açık klip kitaplığından ("radio_check", sakin) kalıcı konuşmacı sesiyle bir satır seçilip
        /// telsiz bandında (350-3400 Hz) çalınır; mikserde Konuşma grubuna bağlanır (Telsiz/ses ayarı). Hat tek seferde tek konuşmacıya
        /// ayrılır; klip/kitap yoksa ya da hata olursa sessizce geçer.
        /// </summary>
        private void GreetOnJoin(int index, TeamRole role)
        {
            try
            {
                if (UnityEngine.Application.isBatchMode || Time.unscaledTime < _greetBusyUntil)
                    return;

                var book = DialogueClipLibrary.Book;
                if (book == null)
                    return;

                _greetScratch.Clear();
                if (book.Collect(GreetCategory, DialogueStress.Calm, _greetScratch) == 0)
                    return;

                var line = _greetScratch[(index * 5 + 1) % _greetScratch.Count];
                var samples = DialogueClipLibrary.Resolve(line.Id, 7000 + index * 37 + (int)role, VoiceStress.Sakin);
                if (samples == null)
                    return;

                var owned = false;
                AudioClip clip;
                if (samples.IsDirect)
                {
                    clip = samples.DirectClip;
                }
                else
                {
                    clip = DialogueClipLibrary.ToClip("lobi_" + line.Id, samples.Data, samples.SampleRate);
                    owned = true;
                }

                if (clip == null)
                    return;

                EnsureGreetSource().PlayOneShot(clip, 0.7f);
                _greetBusyUntil = Time.unscaledTime + clip.length + 0.25f;
                if (owned)
                    Destroy(clip, clip.length + 0.5f);
            }
            catch (Exception)
            {
                // selam süstür; lobi akışı etkilenmez
            }
        }

        private AudioSource EnsureGreetSource()
        {
            if (_greetSource != null)
                return _greetSource;

            var go = new GameObject("LobiTelsiz");
            go.transform.SetParent(transform, false);
            _greetSource = go.AddComponent<AudioSource>();
            _greetSource.playOnAwake = false;
            _greetSource.spatialBlend = 0f;
            _greetSource.ignoreListenerPause = true;
            go.AddComponent<AudioHighPassFilter>().cutoffFrequency = 350f;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3400f;
            MixerRouting.Route(_greetSource, MixChannel.Konusma);
            return _greetSource;
        }

        private void Finish()
        {
            if (_finished)
                return;
            _finished = true;
            _current = null;
            UiSounds.Play(UiSfx.MatchFound);
            var done = _onDone;
            _onDone = null;
            _passThrough = true;
            try { done?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            finally { _passThrough = false; }
            // Yükleme başladıysa sahne zaten yok edilir; başlamadıysa örtü kalkar.
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;
        }

        // ================================================================== Arayüz

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("LobbyFlowCanvas", SortOrder);
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform as RectTransform;

            var bg = UiFactory.Panel(root, new Color(0.04f, 0.05f, 0.03f, 0.97f));
            UiFactory.Stretch(bg);
            bg.GetComponent<Image>().raycastTarget = true;

            BuildGather(root);
            BuildBriefing(root);

            _hint = UiFactory.Label(root, "BOŞLUK: GEÇ", 18, TextAnchor.LowerRight, UiTheme.TextMuted);
            UiFactory.Anchor(_hint, UiAnchor.BottomRight, new Vector2(-40f, 28f), new Vector2(400f, 32f));
            Tick();
        }

        private CanvasGroup NewGroup(RectTransform parent, string name)
        {
            var rt = UiFactory.CreateRect(name, parent);
            UiFactory.Stretch(rt);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.interactable = false;
            g.blocksRaycasts = false;
            return g;
        }

        private void BuildGather(RectTransform root)
        {
            _gatherGroup = NewGroup(root, "Gather");
            var g = _gatherGroup.transform as RectTransform;

            var title = UiFactory.Label(g, "TİM TOPLANIYOR", 54, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            UiFactory.Anchor(title, UiAnchor.Top, new Vector2(0f, -90f), new Vector2(1200f, 80f));
            _gatherCount = UiFactory.Label(g, "1 / 10 ASKER HAZIR", 24, TextAnchor.MiddleCenter, UiTheme.TextDim);
            UiFactory.Anchor(_gatherCount, UiAnchor.Top, new Vector2(0f, -160f), new Vector2(800f, 40f));

            // 3 sütun x 3 satır kart ızgarası.
            const float cw = 420f, ch = 120f, gx = 24f, gy = 22f;
            for (var i = 0; i < LobbyFlowRules.Teammates; i++)
            {
                var col = i % 3;
                var row = i / 3;
                _cardBaseX[i] = (col - 1) * (cw + gx);
                _cardBaseY[i] = 60f - row * (ch + gy);
                BuildCard(g, i, cw, ch);
            }
        }

        private void BuildCard(RectTransform parent, int i, float w, float h)
        {
            var leader = Roles[i] == TeamRole.Leader;
            var card = UiFactory.Panel(parent, leader ? new Color(0.30f, 0.26f, 0.12f, 0.95f) : UiTheme.Panel,
                UiSprites.GetRoundedRect(8));
            UiFactory.Anchor(card, UiAnchor.Center, new Vector2(_cardBaseX[i], _cardBaseY[i]), new Vector2(w, h));
            var grp = card.gameObject.AddComponent<CanvasGroup>();
            grp.alpha = 0f;
            _cards[i] = card;
            _cardGroups[i] = grp;

            // Rol rozeti (sol).
            var badge = UiFactory.Panel(card, RoleColor(Roles[i]), UiSprites.GetRoundedRect(8));
            UiFactory.Anchor(badge, UiAnchor.Left, new Vector2(14f, 0f), new Vector2(64f, 64f));
            var ab = UiFactory.Label(badge, RoleAbbr(Roles[i]), 26, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UiFactory.Stretch(ab);

            var name = UiFactory.Label(card, Names[i].ToUpperInvariant(), 28, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.Anchor(name, UiAnchor.TopLeft, new Vector2(94f, -12f), new Vector2(w - 110f, 36f));
            var role = UiFactory.Label(card, (leader ? "★ " : "") + RoleName(Roles[i]) + " · " + RankCatalog.GetName(Ranks[i]),
                16, TextAnchor.MiddleLeft, leader ? Gold : UiTheme.TextDim);
            UiFactory.Anchor(role, UiAnchor.TopLeft, new Vector2(94f, -48f), new Vector2(w - 110f, 24f));

            var ins = MenuRankInsignia.Create(card, Ranks[i], 30f);
            UiFactory.Anchor(ins, UiAnchor.BottomLeft, new Vector2(94f, 12f), new Vector2(78f, 30f));
            if (leader)
            {
                var tag = UiFactory.Label(card, "TİM KOMUTANI", 14, TextAnchor.MiddleRight, Gold, FontStyle.Bold);
                UiFactory.Anchor(tag, UiAnchor.BottomRight, new Vector2(-14f, 12f), new Vector2(200f, 22f));
            }
        }

        private void BuildBriefing(RectTransform root)
        {
            _briefGroup = NewGroup(root, "Briefing");
            var g = _briefGroup.transform as RectTransform;

            var title = UiFactory.Label(g, "HARİTA BRİFİNGİ", 48, TextAnchor.MiddleCenter, Gold, FontStyle.Bold);
            UiFactory.Anchor(title, UiAnchor.Top, new Vector2(0f, -70f), new Vector2(1200f, 70f));

            // Harita önizleme (sol).
            var frame = UiFactory.Panel(g, UiTheme.PanelDark, UiSprites.GetRoundedRect(8));
            UiFactory.Anchor(frame, UiAnchor.Left, new Vector2(140f, -20f), new Vector2(580f, 580f));
            var mapId = SafeMapId();
            Texture2D tex = null;
            try { tex = MapPreviewArt.Get(mapId, 512); }
            catch (Exception e) { Debug.LogWarning("[LobiAkisi] harita önizleme: " + e.Message); }
            var art = UiFactory.RawImage(frame, tex);
            UiFactory.Stretch(art, 10f);
            if (tex == null)
                art.color = new Color(0.2f, 0.25f, 0.18f, 1f);
            DrawCorridor(art.rectTransform, mapId);

            // Sağ: harita adı, mod kuralları, geri sayım.
            var map = UiFactory.Label(g, MapDisplayName(mapId).ToUpperInvariant(), 40, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.Anchor(map, UiAnchor.Right, new Vector2(-120f, 190f), new Vector2(900f, 56f));
            var mode = UiFactory.Label(g, ModeTitle(), 22, TextAnchor.MiddleLeft, Gold);
            UiFactory.Anchor(mode, UiAnchor.Right, new Vector2(-120f, 140f), new Vector2(900f, 34f));

            var rules = Rules();
            for (var i = 0; i < 3; i++)
            {
                var b = UiFactory.Label(g, "▸  " + rules[i], 26, TextAnchor.MiddleLeft, UiTheme.Text);
                UiFactory.Anchor(b, UiAnchor.Right, new Vector2(-120f, 70f - i * 52f), new Vector2(900f, 44f));
                _bullets[i] = b.gameObject.AddComponent<CanvasGroup>();
            }

            var ready = UiFactory.Label(g, "İNTİKALE HAZIR OL", 34, TextAnchor.MiddleLeft, UiTheme.Accent, FontStyle.Bold);
            UiFactory.Anchor(ready, UiAnchor.Right, new Vector2(-120f, -150f), new Vector2(900f, 50f));
            _countText = UiFactory.Label(g, "5", 120, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            UiFactory.Anchor(_countText, UiAnchor.Right, new Vector2(-420f, -270f), new Vector2(200f, 150f));
            _countGroup = _countText.gameObject.AddComponent<CanvasGroup>();
            _countGroup.alpha = 0f;
            ready.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>Planlanan intikal koridoru: haritanın bir kenarından merkeze kesikli yön hattı (+ iniş işareti).</summary>
        private static void DrawCorridor(RectTransform map, string mapId)
        {
            var hash = 0;
            foreach (var c in mapId ?? string.Empty)
                hash = hash * 31 + c;
            var ang = (Mathf.Abs(hash) % 360) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            const float half = 270f;
            var from = -dir * half * 0.95f;
            var to = dir * half * 0.15f;
            const int dots = 22;
            for (var i = 0; i <= dots; i++)
            {
                var p = Vector2.Lerp(from, to, i / (float)dots);
                var d = UiFactory.Image(map, UiSprites.Circle, i % 2 == 0 ? UiTheme.Amber : new Color(1f, 1f, 1f, 0.85f));
                UiFactory.Anchor(d, UiAnchor.Center, p, new Vector2(i == dots ? 22f : 9f, i == dots ? 22f : 9f));
                d.raycastTarget = false;
                if (i == dots)
                    d.color = UiTheme.Accent;
            }
            var lbl = UiFactory.Label(map, "İNTİKAL KORİDORU", 16, TextAnchor.MiddleCenter, UiTheme.Amber, FontStyle.Bold);
            UiFactory.Anchor(lbl, UiAnchor.Center, from + (Vector2.up * 18f), new Vector2(220f, 22f));
        }

        private static string SafeMapId()
        {
            try { return GameSession.SelectedMap; }
            catch (Exception) { return MapCatalog.Kuzgun; }
        }

        private static string MapDisplayName(string id)
        {
            var i = 0;
            for (; i < MapCatalog.Count; i++)
                if (MapCatalog.IdAt(i) == id)
                    break;
            var names = MapCatalog.DisplayNames();
            return i < names.Length ? names[i] : id;
        }

        private static string ModeTitle()
        {
            switch (GameSession.Mode)
            {
                case GameMode.Skirmish: return "ÇATIŞMA · 2 TİM × 10";
                case GameMode.HostageRescue: return "REHİNE KURTARMA";
                default: return "HAREKÂT · SON TİM AYAKTA";
            }
        }

        private static string[] Rules()
        {
            switch (GameSession.Mode)
            {
                case GameMode.Skirmish:
                    return new[] { "Rakip timi tasfiye et", "Düşen askeri sıhhiyeci kaldırır", "Telsizle komut ver, birlikte kal" };
                case GameMode.HostageRescue:
                    return new[] { "Rehineleri bul ve güvenli bölgeye ulaştır", "Rehine zarar görmemeli", "Sessiz ilerle, kuşatılma" };
                default:
                    return new[] { "Helikopterden en uygun noktaya atla", "Daralan emniyet sahasının içinde kal", "Son ayakta kalan tim kazanır" };
            }
        }

        private static string RoleAbbr(TeamRole r)
        {
            switch (r)
            {
                case TeamRole.Leader: return "TK";
                case TeamRole.Marksman: return "KN";
                case TeamRole.MachineGunner: return "MT";
                case TeamRole.Medic: return "SH";
                case TeamRole.Radioman: return "TL";
                case TeamRole.Grenadier: return "BM";
                default: return "PY";
            }
        }

        private static string RoleName(TeamRole r)
        {
            switch (r)
            {
                case TeamRole.Leader: return "Tim Komutanı";
                case TeamRole.Marksman: return "Keskin Nişancı";
                case TeamRole.MachineGunner: return "Makineli Tüfekçi";
                case TeamRole.Medic: return "Sıhhiyeci";
                case TeamRole.Radioman: return "Telsizci";
                case TeamRole.Grenadier: return "Bombacı";
                default: return "Piyade";
            }
        }

        private static Color RoleColor(TeamRole r)
        {
            switch (r)
            {
                case TeamRole.Leader: return new Color(0.70f, 0.55f, 0.15f);
                case TeamRole.Marksman: return new Color(0.25f, 0.40f, 0.55f);
                case TeamRole.MachineGunner: return new Color(0.55f, 0.28f, 0.22f);
                case TeamRole.Medic: return new Color(0.25f, 0.55f, 0.35f);
                case TeamRole.Radioman: return new Color(0.40f, 0.35f, 0.60f);
                case TeamRole.Grenadier: return new Color(0.55f, 0.42f, 0.22f);
                default: return new Color(0.32f, 0.38f, 0.26f);
            }
        }
    }
}
