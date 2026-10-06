using System.Text;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Presentation.Replay
{
    public sealed partial class ReplayViewer
    {
        private const int SortOrder = 60;

        private Canvas _canvas;
        private Text _timeLabel;
        private Text _infoLabel;
        private Text _feedLabel;
        private Text _playLabel;
        private Slider _slider;
        private RectTransform _markerRail;
        private bool _sliderUpdating;
        private readonly StringBuilder _sb = new StringBuilder(256);

        private void BuildUi()
        {
            UiFactory.EnsureEventSystem();
            _canvas = UiFactory.CreateCanvas("[Tekrar UI]", SortOrder);
            var root = (RectTransform)_canvas.transform;

            _infoLabel = UiFactory.Label(root, "", UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_infoLabel, new Vector2(0f, 1f), new Vector2(0.6f, 1f), new Vector2(24f, -110f), new Vector2(-8f, -20f));
            UiFactory.AddShadow(_infoLabel, UiTheme.TextShadow, new Vector2(2f, -2f));

            _feedLabel = UiFactory.Label(root, "", UiTheme.FontSmall, TextAnchor.UpperRight, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetRect(_feedLabel, new Vector2(0.55f, 1f), new Vector2(1f, 1f), new Vector2(8f, -200f), new Vector2(-24f, -20f));
            UiFactory.AddShadow(_feedLabel, UiTheme.TextShadow, new Vector2(2f, -2f));

            var bar = UiFactory.Panel(root, UiTheme.PanelDark, UiSprites.ChamferRect);
            UiFactory.SetRect(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 16f), new Vector2(-24f, 244f));

            _timeLabel = UiFactory.Label(bar, "", UiTheme.FontSmall, TextAnchor.MiddleLeft, UiTheme.Khaki, FontStyle.Bold);
            UiFactory.SetRect(_timeLabel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -38f), new Vector2(-20f, -6f));

            _slider = UiFactory.Slider(bar, 0f, Mathf.Max(0.1f, _data.Duration), 0f, v =>
            {
                if (!_sliderUpdating)
                    Seek(v);
            });
            UiFactory.SetRect(_slider, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -78f), new Vector2(-20f, -42f));

            _markerRail = UiFactory.CreateRect("Isaretler", bar);
            UiFactory.SetRect(_markerRail, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -104f), new Vector2(-20f, -80f));
            BuildMarkers();

            var presets = UiFactory.HorizontalList(bar, 10f, 0, TextAnchor.MiddleLeft);
            UiFactory.SetRect(presets, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 70f), new Vector2(-20f, 70f + 52f));
            BuildSpeedPresets(presets);

            var row = UiFactory.HorizontalList(bar, 10f, 0, TextAnchor.MiddleLeft);
            UiFactory.SetRect(row, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 12f), new Vector2(-20f, 12f + 52f));
            var play = UiFactory.Button(row, "DURAKLAT", TogglePlay, UiButtonStyle.Primary);
            _playLabel = UiFactory.GetButtonLabel(play);
            UiFactory.LayoutSize(play, 170f, 52f);
            AddButton(row, "-5 sn", () => Seek(_time - 5f), 100f);
            AddButton(row, "+5 sn", () => Seek(_time + 5f), 100f);
            AddButton(row, "YAVAŞ", () => ChangeSpeed(-1), 110f);
            AddButton(row, "HIZLI", () => ChangeSpeed(1), 110f);
            UiFactory.FlexibleSpacer(row);
            AddButton(row, "KAMERA (F)", ToggleCamera, 170f);
            AddButton(row, "ÖNCEKİ", () => CycleTarget(-1), 120f);
            AddButton(row, "SONRAKİ", () => CycleTarget(1), 120f);
            var exit = UiFactory.Button(row, "ÇIKIŞ", Exit, UiButtonStyle.Danger);
            UiFactory.LayoutSize(exit, 120f, 52f);
        }

        private static void AddButton(Transform parent, string label, System.Action onClick, float width)
        {
            var b = UiFactory.Button(parent, label, onClick, UiButtonStyle.Default);
            UiFactory.LayoutSize(b, width, 52f);
        }

        private void BuildMarkers()
        {
            var duration = Mathf.Max(0.1f, _data.Duration);
            var kills = _data.Kills();
            for (var i = 0; i < kills.Count; i++)
            {
                var k = kills[i];
                var mine = k.Actor == _data.LocalPlayerId && k.Actor != k.Target;
                var died = k.Target == _data.LocalPlayerId;
                var color = mine ? UiTheme.Success : (died ? UiTheme.KillMarker : UiTheme.WithAlpha(UiTheme.Amber, 0.85f));
                var marker = UiFactory.Panel(_markerRail, color);
                marker.gameObject.name = "Olum_" + i;
                var nx = Mathf.Clamp01(k.Time / duration);
                marker.anchorMin = marker.anchorMax = new Vector2(nx, 0.5f);
                marker.pivot = new Vector2(0.5f, 0.5f);
                marker.sizeDelta = new Vector2(mine || died ? 8f : 5f, mine || died ? 22f : 16f);
                marker.anchoredPosition = Vector2.zero;
                var button = marker.gameObject.AddComponent<Button>();
                var graphic = marker.GetComponent<Graphic>();
                if (graphic != null)
                {
                    graphic.raycastTarget = true;
                    button.targetGraphic = graphic;
                }
                var t = k.Time;
                button.onClick.AddListener(() => Seek(Mathf.Max(0f, t - 3f)));
            }
        }

        private void UpdateUi()
        {
            if (_timeLabel == null)
                return;

            _sb.Length = 0;
            _sb.Append(Format(_time)).Append(" / ").Append(Format(_data.Duration)).Append("    ")
               .Append(Speeds[_speedIndex].ToString("0.##")).Append("x    ")
               .Append(_playing ? "OYNATILIYOR" : (_ended ? "BİTTİ" : "DURAKLATILDI"));
            UiFactory.SetText(_timeLabel, _sb.ToString());
            UiFactory.SetText(_playLabel, _playing ? "DURAKLAT" : (_ended ? "BAŞTAN" : "OYNAT"));

            _sliderUpdating = true;
            _slider.SetValueWithoutNotify(_time);
            _sliderUpdating = false;

            var target = _follow ? "TAKİP: " + NameOf(_followId) : "SERBEST KAMERA";
            UiFactory.SetText(_infoLabel, "TEKRAR  |  " + target + "\nBoşluk: oynat/duraklat  F: kamera  V: 1. şahıs  B: omuz  N: sonraki ölüm  1-5: hız  Tekerlek: FOV  F12: ekran  Tab: sonraki  ←/→: ±5 sn  ,/.: hız  Sağ tık: bakış  WASD/QE: uçuş  Esc: çıkış");

            _sb.Length = 0;
            var now = Time.unscaledTime;
            for (var i = 0; i < _feed.Count; i++)
                if (now - _feedTimes[i] < FeedLifetime)
                    _sb.AppendLine(_feed[i]);
            UiFactory.SetText(_feedLabel, _sb.ToString());
        }

        private static string Format(float seconds)
        {
            var s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }
    }
}
