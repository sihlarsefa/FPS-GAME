using System;
using Project.Online.Backend;
using Project.Online.Bootstrap;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Online.UI
{
    /// <summary>
    /// Eşleştirme paneli: kuyruğa gir, bekleme süresi, iptal, bilet durumu yoklama.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineMatchmakingPanel : MonoBehaviour
    {
        private const float WindowWidth = 560f;
        private const float WindowHeight = 420f;
        private const float PollInterval = 2f;

        private Action _onClosed;
        private bool _closed;
        private bool _busy;
        private CanvasGroup _group;
        private float _fade;
        private float _pollTimer;

        private QueueInfo _ticket;
        private Text _waitLabel;
        private Text _statusLabel;
        private Text _status;
        private Button _enqueueButton;
        private Button _cancelButton;

        public bool IsOpen => !_closed && this != null;

        public static OnlineMatchmakingPanel Show(Transform parent, Action onClosed = null)
        {
            OnlineServices.Ensure();

            var root = OnlineUi.CreateDimRoot("[Online Eşleştirme]", parent, out var group);
            var panel = root.gameObject.AddComponent<OnlineMatchmakingPanel>();
            panel._onClosed = onClosed;
            panel._group = group;
            panel.Build(root);
            return panel;
        }

        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            ClearSelection();
            var callback = _onClosed;
            _onClosed = null;

            gameObject.SetActive(false);
            UiFactory.DestroySafe(gameObject);

            if (callback == null)
                return;
            try { callback(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void Build(RectTransform root)
        {
            var window = OnlineUi.CreateWindow(root, WindowWidth, WindowHeight, "EŞLEŞTİRME");
            var body = OnlineUi.CreateBody(window, 110f, 100f);

            var info = UiFactory.Label(body,
                "Tim hazırsa kuyruğa gir. Bölge: TR",
                UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.TextMuted);
            UiFactory.LayoutSize(info, -1f, 36f, 1f);

            _waitLabel = UiFactory.Label(body, "Bekleme: —", UiTheme.FontLarge, TextAnchor.MiddleLeft, UiTheme.Amber);
            UiFactory.LayoutSize(_waitLabel, -1f, 48f, 1f);

            _statusLabel = UiFactory.Label(body, "Durum: Beklemede", UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Text);
            UiFactory.LayoutSize(_statusLabel, -1f, 32f, 1f);

            _enqueueButton = UiFactory.Button(body, "KUYRUĞA GİR", OnEnqueueClicked, UiButtonStyle.Primary);
            UiFactory.LayoutSize(_enqueueButton, -1f, UiTheme.ButtonHeight, 1f);

            _cancelButton = UiFactory.Button(body, "İPTAL", OnCancelClicked, UiButtonStyle.Danger);
            UiFactory.LayoutSize(_cancelButton, -1f, UiTheme.ButtonHeight, 1f);
            UiWidgets.SetInteractable(_cancelButton, false);

            _status = OnlineUi.StatusLabel(body);
            OnlineUi.CreateFooter(window, out _, Close, "GERİ");
        }

        private void OnEnqueueClicked()
        {
            if (_busy || _closed) return;
            _ = EnqueueAsync();
        }

        private void OnCancelClicked()
        {
            if (_busy || _closed) return;
            _ = CancelAsync();
        }

        private async System.Threading.Tasks.Task EnqueueAsync()
        {
            _busy = true;
            OnlineUi.SetStatus(_status, "Kuyruğa giriliyor…", UiTheme.TextDim);
            try
            {
                _ticket = await OnlineServices.Client.EnqueueAsync("tr");
                if (_closed) return;
                ApplyTicket(_ticket);
                OnlineUi.SetStatus(_status, "Kuyrukta.", UiTheme.HealthHigh);
                UiWidgets.SetInteractable(_enqueueButton, false);
                UiWidgets.SetInteractable(_cancelButton, true);
                _pollTimer = 0f;
            }
            catch (BackendApiException ex)
            {
                if (!_closed) OnlineUi.SetStatus(_status, ex.TurkishMessage, UiTheme.Accent);
            }
            catch (Exception ex)
            {
                if (!_closed) OnlineUi.SetStatus(_status, ex.Message, UiTheme.Accent);
            }
            finally { _busy = false; }
        }

        private async System.Threading.Tasks.Task CancelAsync()
        {
            _busy = true;
            OnlineUi.SetStatus(_status, "İptal ediliyor…", UiTheme.TextDim);
            try
            {
                await OnlineServices.Client.CancelQueueAsync();
                if (_closed) return;
                _ticket = null;
                ApplyTicket(null);
                OnlineUi.SetStatus(_status, "Kuyruk iptal edildi.", UiTheme.TextDim);
                UiWidgets.SetInteractable(_enqueueButton, true);
                UiWidgets.SetInteractable(_cancelButton, false);
            }
            catch (BackendApiException ex)
            {
                if (!_closed) OnlineUi.SetStatus(_status, ex.TurkishMessage, UiTheme.Accent);
            }
            catch (Exception ex)
            {
                if (!_closed) OnlineUi.SetStatus(_status, ex.Message, UiTheme.Accent);
            }
            finally { _busy = false; }
        }

        private async System.Threading.Tasks.Task PollTicketAsync()
        {
            if (_ticket.TicketId == Guid.Empty || _busy || _closed)
                return;

            _busy = true;
            try
            {
                var info = await OnlineServices.Client.GetTicketAsync(_ticket.TicketId);
                if (_closed) return;
                _ticket = info;
                ApplyTicket(info);

                var status = (info.Status ?? "").Trim();
                if (string.Equals(status, "Matched", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(status, "1", StringComparison.Ordinal))
                {
                    OnlineUi.SetStatus(_status, "Maç bulundu!", UiTheme.HealthHigh);
                    UiWidgets.SetInteractable(_enqueueButton, false);
                    UiWidgets.SetInteractable(_cancelButton, false);
                }
            }
            catch (BackendApiException ex)
            {
                if (!_closed)
                {
                    if (ex.StatusCode == 404)
                    {
                        _ticket = null;
                        ApplyTicket(null);
                        UiWidgets.SetInteractable(_enqueueButton, true);
                        UiWidgets.SetInteractable(_cancelButton, false);
                        OnlineUi.SetStatus(_status, "Bilet geçersiz veya süresi doldu.", UiTheme.TextDim);
                    }
                    else
                    {
                        OnlineUi.SetStatus(_status, ex.TurkishMessage, UiTheme.Accent);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_closed)
                    OnlineUi.SetStatus(_status, ex.Message, UiTheme.Accent);
            }
            finally { _busy = false; }
        }

        private void ApplyTicket(QueueInfo info)
        {
            if (_statusLabel != null)
            {
                if (info == null)
                    _statusLabel.text = "Durum: Beklemede";
                else
                    _statusLabel.text = "Durum: " + StatusTr(info.Status);
            }

            RefreshWaitTime();
        }

        private void RefreshWaitTime()
        {
            if (_waitLabel == null)
                return;

            if (_ticket == null || _ticket.EnqueuedAt == default)
            {
                _waitLabel.text = "Bekleme: —";
                return;
            }

            var elapsed = DateTimeOffset.UtcNow - _ticket.EnqueuedAt;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;
            _waitLabel.text = "Bekleme: " + FormatDuration(elapsed);
        }

        private static string StatusTr(string status)
        {
            if (string.IsNullOrEmpty(status))
                return "Bilinmiyor";
            if (string.Equals(status, "Queued", StringComparison.OrdinalIgnoreCase) || status == "0")
                return "Kuyrukta";
            if (string.Equals(status, "Matched", StringComparison.OrdinalIgnoreCase) || status == "1")
                return "Eşleşti";
            return status;
        }

        private static string FormatDuration(TimeSpan t)
        {
            if (t.TotalHours >= 1)
                return string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:00}:{1:00}", (int)t.TotalMinutes, t.Seconds);
        }

        private void Update()
        {
            if (_group != null && _fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
                _group.alpha = _fade;
            }

            if (_closed)
                return;

            RefreshWaitTime();

            if (_ticket == null || _ticket.TicketId == Guid.Empty || _busy)
                return;

            var status = _ticket.Status ?? "";
            if (string.Equals(status, "Matched", StringComparison.OrdinalIgnoreCase) || status == "1")
                return;

            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer >= PollInterval)
            {
                _pollTimer = 0f;
                _ = PollTicketAsync();
            }
        }

        private void ClearSelection()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null &&
                es.currentSelectedGameObject.transform.IsChildOf(transform))
                es.SetSelectedGameObject(null);
        }
    }
}
