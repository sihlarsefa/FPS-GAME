using System.Collections.Generic;
using Project.Application.Services;
using Project.Presentation.Bootstrap;
using UnityEngine;
using UnityEngine.UI;
using Project.Infrastructure.Localization;

namespace Project.Presentation.UI
{
    /// <summary>Başarım açıldığında sağ üstte kısa süre görünen bildirim. Sahneler arası kalır.</summary>
    [DisallowMultipleComponent]
    public sealed class AchievementToastView : MonoBehaviour
    {
        private const float ShowSeconds = 4f;
        private const float FadeSeconds = 0.4f;

        private static AchievementToastView _instance;

        private readonly Queue<AchievementDefinition> _queue = new Queue<AchievementDefinition>();
        private CanvasGroup _group;
        private Text _title;
        private Text _name;
        private Text _desc;
        private float _timer;

        public static void Ensure()
        {
            if (_instance != null)
                return;
            var go = new GameObject("AchievementToast");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AchievementToastView>();
            _instance.Build();
            GameSession.AchievementUnlocked += _instance.Enqueue;
            Project.Infrastructure.Rendering.AutoQuality.Notified += _instance.EnqueueNotice;
            DailyMissionsHost.Notified += _instance.EnqueueNotice;
        }

        private readonly Queue<string> _notices = new Queue<string>();

        /// <summary>Genel bilgi bildirimi (ör. AutoQuality çözünürlük düşürme); başarım kuyruğuyla aynı kartı kullanır.</summary>
        private void EnqueueNotice(string text)
        {
            if (!string.IsNullOrEmpty(text))
                _notices.Enqueue(text);
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("AchievementToastCanvas", 900);
            canvas.transform.SetParent(transform, false);

            var panel = UiFactory.Panel(canvas.transform, UiTheme.PanelDark, UiSprites.ChamferRect);
            UiFactory.Anchor(panel, UiAnchor.TopRight, new Vector2(-40f, -40f), new Vector2(520f, 120f));
            _group = panel.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _title = UiFactory.Label(panel, Loc.Get("ach.toast_title", "BAŞARIM AÇILDI"), UiTheme.FontTiny, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(_title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -34f), new Vector2(-20f, -8f));
            _name = UiFactory.Label(panel, "", UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_name, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20f, -14f), new Vector2(-20f, 22f));
            _desc = UiFactory.Label(panel, "", UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            UiFactory.SetRect(_desc, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 8f), new Vector2(-20f, 36f));
        }

        private void Enqueue(AchievementDefinition def)
        {
            if (def != null)
                _queue.Enqueue(def);
        }

        private void Update()
        {
            if (_group == null)
                return;

            if (_timer <= 0f)
            {
                if (_queue.Count == 0 && _notices.Count == 0)
                {
                    _group.alpha = 0f;
                    return;
                }
                if (_queue.Count > 0)
                {
                    var def = _queue.Dequeue();
                    _title.text = Loc.Get("ach.toast_title", "BAŞARIM AÇILDI");
                    _name.text = def.title;
                    _desc.text = def.description;
                }
                else
                {
                    _title.text = Loc.Get("toast.info", "BİLGİ");
                    _name.text = _notices.Dequeue();
                    _desc.text = "";
                }
                _timer = ShowSeconds;
            }

            _timer -= Time.unscaledDeltaTime;
            _group.alpha = Mathf.Clamp01(Mathf.Min(_timer, ShowSeconds - _timer) / FadeSeconds);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                GameSession.AchievementUnlocked -= Enqueue;
                Project.Infrastructure.Rendering.AutoQuality.Notified -= EnqueueNotice;
                DailyMissionsHost.Notified -= EnqueueNotice;
                _instance = null;
            }
        }
    }
}
