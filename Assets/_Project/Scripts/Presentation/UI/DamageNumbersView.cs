using System;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Presentation.UI
{
    /// <summary>Hasar sayısı birleştirme + asist takibi (saf mantık, test edilebilir).</summary>
    public sealed class DamageNumberStacker
    {
        public const float StackWindow = 0.35f;
        public const float AssistWindow = 8f;
        public const float AssistMinDamage = 20f;

        public struct Slot
        {
            public PlayerId Victim;
            public float Total;
            public bool Headshot;
            public bool Kill;
            public bool Armor;
            public float LastTime;
            public float Spawn;
            public bool Active;
        }

        public readonly Slot[] Slots;
        private readonly float[] _dealt;
        private readonly int[] _dealtVictim;
        private readonly float[] _dealtTime;

        public DamageNumberStacker(int capacity = 12)
        {
            Slots = new Slot[capacity];
            _dealt = new float[capacity * 2];
            _dealtVictim = new int[capacity * 2];
            _dealtTime = new float[capacity * 2];
            for (var i = 0; i < _dealtVictim.Length; i++) _dealtVictim[i] = -1;
        }

        /// <summary>Aynı kurbana kısa sürede gelen hasarı tek sayıda toplar. Slot indeksini döndürür.</summary>
        public int Add(PlayerId victim, float damage, bool headshot, bool kill, bool armor, float now)
        {
            if (damage <= 0f) return -1;
            RecordDealt(victim, damage, now);
            var free = -1;
            var oldest = 0;
            for (var i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].Active) { if (free < 0) free = i; continue; }
                if (Slots[i].Spawn < Slots[oldest].Spawn || !Slots[oldest].Active) oldest = i;
                if (Slots[i].Victim.Equals(victim) && now - Slots[i].LastTime <= StackWindow)
                {
                    Slots[i].Total += damage;
                    Slots[i].Headshot |= headshot;
                    Slots[i].Kill |= kill;
                    Slots[i].Armor &= armor;
                    Slots[i].LastTime = now;
                    return i;
                }
            }
            var idx = free >= 0 ? free : oldest;
            Slots[idx] = new Slot { Victim = victim, Total = damage, Headshot = headshot, Kill = kill, Armor = armor, LastTime = now, Spawn = now, Active = true };
            return idx;
        }

        private void RecordDealt(PlayerId victim, float damage, float now)
        {
            var pick = -1;
            var stalest = 0;
            for (var i = 0; i < _dealtVictim.Length; i++)
            {
                if (_dealtVictim[i] == victim.Value && now - _dealtTime[i] <= AssistWindow) { _dealt[i] += damage; _dealtTime[i] = now; return; }
                if (_dealtVictim[i] < 0 || now - _dealtTime[i] > AssistWindow) { if (pick < 0) pick = i; }
                else if (_dealtTime[i] < _dealtTime[stalest]) stalest = i;
            }
            if (pick < 0) pick = stalest;
            _dealtVictim[pick] = victim.Value; _dealt[pick] = damage; _dealtTime[pick] = now;
        }

        /// <summary>Yerel oyuncu öldürmediği ama yakın zamanda yeterince hasar verdiği kurbanda asist.</summary>
        public bool IsAssist(PlayerId victim, bool localIsKiller, float now)
        {
            if (localIsKiller) return false;
            for (var i = 0; i < _dealtVictim.Length; i++)
                if (_dealtVictim[i] == victim.Value && now - _dealtTime[i] <= AssistWindow && _dealt[i] >= AssistMinDamage)
                {
                    _dealtVictim[i] = -1;
                    return true;
                }
            return false;
        }

        public static string Format(float total) => Mathf.Max(1, Mathf.RoundToInt(total)).ToString();
        public static float Life => 0.9f;
    }

    /// <summary>
    /// Süzülen hasar sayıları (varsayılan KAPALI, ayar "Hasar sayıları") + asist bildirimi.
    /// Kafa vuruşu altın, zırh mavi, öldürme kırmızı. HitFeedbackDirector besler.
    /// </summary>
    public sealed class DamageNumbersView : MonoBehaviour
    {
        public const string PrefKey = "hud.damageNumbers";
        private const float RiseSpeed = 70f;

        public static bool Enabled
        {
            get { try { return PlayerPrefs.GetInt(PrefKey, 0) != 0; } catch (Exception) { return false; } }
            set { try { PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); } catch (Exception) { } }
        }

        private readonly DamageNumberStacker _stacker = new DamageNumberStacker();
        private Text[] _labels;
        private Vector3[] _world;
        private Text _toast;
        private float _toastUntil;
        private RectTransform _root;

        public DamageNumberStacker Stacker => _stacker;

        public static DamageNumbersView Create(GameObject host)
        {
            var view = host.AddComponent<DamageNumbersView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            var canvasGo = new GameObject("DamageNumbersCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            _root = canvasGo.GetComponent<RectTransform>();

            var n = _stacker.Slots.Length;
            _labels = new Text[n];
            _world = new Vector3[n];
            for (var i = 0; i < n; i++)
            {
                _labels[i] = MakeLabel("Num" + i, 30, TextAnchor.MiddleCenter);
                _labels[i].gameObject.SetActive(false);
            }
            _toast = MakeLabel("AssistToast", 26, TextAnchor.MiddleCenter);
            var rt = _toast.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.3f);
            rt.anchoredPosition = Vector2.zero;
            _toast.color = UiTheme.Amber;
            _toast.text = "ASİST";
            _toast.gameObject.SetActive(false);
        }

        private Text MakeLabel(string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var t = go.AddComponent<Text>();
            t.font = UiTheme.Font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(220f, 44f);
            return t;
        }

        public void Hit(PlayerId victim, Vector3 worldPos, float damage, bool headshot, bool kill, bool armor, float now)
        {
            var i = _stacker.Add(victim, damage, headshot, kill, armor, now);
            if (i < 0 || !Enabled || _labels == null) return;
            _world[i] = worldPos;
        }

        public void Assist(float now)
        {
            if (_toast == null) return;
            _toast.text = "ASİST";
            _toast.gameObject.SetActive(true);
            _toastUntil = now + 1.6f;
        }

        private void Update()
        {
            if (_labels == null) return;
            var now = Time.unscaledTime;
            if (_toast.gameObject.activeSelf && now > _toastUntil) _toast.gameObject.SetActive(false);

            var cam = Camera.main;
            var slots = _stacker.Slots;
            for (var i = 0; i < slots.Length; i++)
            {
                var label = _labels[i];
                if (!slots[i].Active) { if (label.gameObject.activeSelf) label.gameObject.SetActive(false); continue; }
                var age = now - slots[i].Spawn;
                if (age > DamageNumberStacker.Life || !Enabled || cam == null)
                {
                    slots[i].Active = false;
                    label.gameObject.SetActive(false);
                    continue;
                }
                var sp = cam.WorldToScreenPoint(_world[i]);
                if (sp.z <= 0f) { label.gameObject.SetActive(false); continue; }
                label.gameObject.SetActive(true);
                label.text = DamageNumberStacker.Format(slots[i].Total);
                label.color = ColorFor(slots[i], 1f - Mathf.Clamp01((age - 0.5f) / 0.4f));
                label.rectTransform.position = new Vector3(sp.x, sp.y + age * RiseSpeed, 0f);
                var pop = 1f + Mathf.Max(0f, 0.25f - (now - slots[i].LastTime)) * 2f;
                label.rectTransform.localScale = Vector3.one * (slots[i].Headshot ? 1.25f : 1f) * pop;
            }
        }

        private static Color ColorFor(DamageNumberStacker.Slot s, float alpha)
        {
            var c = s.Kill ? UiTheme.KillMarker : s.Headshot ? new Color(1f, 0.82f, 0.2f) : s.Armor ? UiTheme.Armor : Color.white;
            c.a = alpha;
            return c;
        }
    }
}
