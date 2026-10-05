using System;
using System.Text;
using Project.Online.Backend;
using Project.Online.Bootstrap;
using Project.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.Online.UI
{
    /// <summary>
    /// Tim paneli: oluştur / davet koduyla katıl, üyeler (en fazla 10), hazır, ayrıl.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineSquadPanel : MonoBehaviour
    {
        private const float WindowWidth = 640f;
        private const float WindowHeight = 720f;
        private const int MaxMembers = 10;
        private const float PollInterval = 3f;

        private Action _onClosed;
        private bool _closed;
        private bool _busy;
        private CanvasGroup _group;
        private float _fade;
        private float _pollTimer;

        private InputField _squadName;
        private InputField _inviteCode;
        private Text _inviteDisplay;
        private Text _membersLabel;
        private Text _status;
        private Toggle _readyToggle;
        private bool _readySuppress;
        private SquadInfo _current;

        public bool IsOpen => !_closed && this != null;

        public static OnlineSquadPanel Show(Transform parent, Action onClosed = null)
        {
            OnlineServices.Ensure();

            var root = OnlineUi.CreateDimRoot("[Online Tim]", parent, out var group);
            var panel = root.gameObject.AddComponent<OnlineSquadPanel>();
            panel._onClosed = onClosed;
            panel._group = group;
            panel.Build(root);
            _ = panel.RefreshSquadAsync(silent: true);
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
            var window = OnlineUi.CreateWindow(root, WindowWidth, WindowHeight, "TİM");
            var body = OnlineUi.CreateBody(window, 110f, 100f);

            _squadName = OnlineUi.CreateField(body, "Tim adı", false);
            var createBtn = UiFactory.Button(body, "TİM OLUŞTUR", OnCreateClicked, UiButtonStyle.Primary);
            UiFactory.LayoutSize(createBtn, -1f, UiTheme.ButtonHeight, 1f);

            _inviteCode = OnlineUi.CreateField(body, "Davet kodu", false);
            var joinBtn = UiFactory.Button(body, "KATIL", OnJoinClicked, UiButtonStyle.Default);
            UiFactory.LayoutSize(joinBtn, -1f, UiTheme.ButtonHeight, 1f);

            _inviteDisplay = UiFactory.Label(body, "Davet kodu: —", UiTheme.FontMedium, TextAnchor.MiddleLeft, UiTheme.Amber);
            UiFactory.LayoutSize(_inviteDisplay, -1f, 32f, 1f);

            _membersLabel = UiFactory.Label(body, "Üyeler: (yok)", UiTheme.FontSmall, TextAnchor.UpperLeft, UiTheme.Text);
            _membersLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _membersLabel.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.LayoutSize(_membersLabel, -1f, 160f, 1f);

            _readyToggle = UiFactory.Toggle(body, "Hazırım", false, OnReadyChanged);
            UiFactory.LayoutSize(_readyToggle, -1f, UiTheme.RowHeight, 1f);

            var leaveBtn = UiFactory.Button(body, "TİMDEN AYRIL", OnLeaveClicked, UiButtonStyle.Danger);
            UiFactory.LayoutSize(leaveBtn, -1f, UiTheme.ButtonHeight, 1f);

            var refreshBtn = UiFactory.Button(body, "YENİLE", () => { _ = RefreshSquadAsync(false); }, UiButtonStyle.Default);
            UiFactory.LayoutSize(refreshBtn, -1f, UiTheme.ButtonHeight, 1f);

            _status = OnlineUi.StatusLabel(body);
            OnlineUi.CreateFooter(window, out _, Close, "GERİ");
        }

        private void OnCreateClicked()
        {
            if (_busy || _closed) return;
            _ = CreateAsync();
        }

        private void OnJoinClicked()
        {
            if (_busy || _closed) return;
            _ = JoinAsync();
        }

        private void OnLeaveClicked()
        {
            if (_busy || _closed) return;
            _ = LeaveAsync();
        }

        private void OnReadyChanged(bool ready)
        {
            if (_readySuppress || _busy || _closed) return;
            _ = SetReadyAsync(ready);
        }

        private async System.Threading.Tasks.Task CreateAsync()
        {
            var name = _squadName != null ? (_squadName.text ?? "").Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                OnlineUi.SetStatus(_status, "Tim adı gerekli.", UiTheme.Accent);
                return;
            }

            _busy = true;
            OnlineUi.SetStatus(_status, "Tim oluşturuluyor…", UiTheme.TextDim);
            try
            {
                _current = await OnlineServices.Client.CreateSquadAsync(name, "tr");
                ApplySquad(_current);
                OnlineUi.SetStatus(_status, "Tim oluşturuldu.", UiTheme.HealthHigh);
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

        private async System.Threading.Tasks.Task JoinAsync()
        {
            var code = _inviteCode != null ? (_inviteCode.text ?? "").Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                OnlineUi.SetStatus(_status, "Davet kodu gerekli.", UiTheme.Accent);
                return;
            }

            _busy = true;
            OnlineUi.SetStatus(_status, "Katılınıyor…", UiTheme.TextDim);
            try
            {
                _current = await OnlineServices.Client.JoinSquadAsync(code);
                ApplySquad(_current);
                OnlineUi.SetStatus(_status, "Time katıldın.", UiTheme.HealthHigh);
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

        private async System.Threading.Tasks.Task LeaveAsync()
        {
            _busy = true;
            OnlineUi.SetStatus(_status, "Ayrılınıyor…", UiTheme.TextDim);
            try
            {
                await OnlineServices.Client.LeaveSquadAsync();
                _current = null;
                ApplySquad(null);
                OnlineUi.SetStatus(_status, "Timden ayrıldın.", UiTheme.TextDim);
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

        private async System.Threading.Tasks.Task SetReadyAsync(bool ready)
        {
            _busy = true;
            try
            {
                var status = await OnlineServices.Client.SetReadyAsync(ready);
                if (_current != null)
                    _current.AllReady = status.AllReady;
                OnlineUi.SetStatus(_status,
                    ready ? "Hazır olarak işaretlendin." : "Hazırlık kaldırıldı.",
                    UiTheme.HealthHigh);
                await RefreshSquadAsync(true);
            }
            catch (BackendApiException ex)
            {
                if (!_closed)
                {
                    OnlineUi.SetStatus(_status, ex.TurkishMessage, UiTheme.Accent);
                    RestoreReadyToggle(!ready);
                }
            }
            catch (Exception ex)
            {
                if (!_closed)
                {
                    OnlineUi.SetStatus(_status, ex.Message, UiTheme.Accent);
                    RestoreReadyToggle(!ready);
                }
            }
            finally { _busy = false; }
        }

        private async System.Threading.Tasks.Task RefreshSquadAsync(bool silent)
        {
            if (_closed) return;
            try
            {
                _current = await OnlineServices.Client.GetMySquadAsync();
                if (!_closed)
                {
                    ApplySquad(_current);
                    if (!silent)
                        OnlineUi.SetStatus(_status, "Güncellendi.", UiTheme.TextDim);
                }
            }
            catch (BackendApiException ex)
            {
                // 404 = tim yok
                if (!_closed)
                {
                    if (ex.StatusCode == 404)
                    {
                        _current = null;
                        ApplySquad(null);
                        if (!silent)
                            OnlineUi.SetStatus(_status, "Aktif tim yok.", UiTheme.TextDim);
                    }
                    else if (!silent)
                    {
                        OnlineUi.SetStatus(_status, ex.TurkishMessage, UiTheme.Accent);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_closed && !silent)
                    OnlineUi.SetStatus(_status, ex.Message, UiTheme.Accent);
            }
        }

        private void ApplySquad(SquadInfo squad)
        {
            if (_inviteDisplay != null)
            {
                _inviteDisplay.text = squad != null && !string.IsNullOrEmpty(squad.InviteCode)
                    ? "Davet kodu: " + squad.InviteCode
                    : "Davet kodu: —";
            }

            if (_membersLabel != null)
            {
                if (squad == null || squad.MemberIds == null || squad.MemberIds.Count == 0)
                {
                    _membersLabel.text = "Üyeler: (yok)";
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.Append("Üyeler (").Append(Mathf.Min(squad.MemberIds.Count, MaxMembers))
                        .Append('/').Append(MaxMembers).Append("):\n");
                    var me = OnlineServices.Client.CurrentPlayer != null
                        ? OnlineServices.Client.CurrentPlayer.Id
                        : Guid.Empty;
                    var count = Mathf.Min(squad.MemberIds.Count, MaxMembers);
                    for (var i = 0; i < count; i++)
                    {
                        var id = squad.MemberIds[i];
                        var ready = squad.ReadyMemberIds != null && squad.ReadyMemberIds.Contains(id);
                        var leader = id == squad.LeaderId;
                        sb.Append("• ");
                        if (id == me) sb.Append("Sen");
                        else sb.Append(ShortId(id));
                        if (leader) sb.Append(" [Lider]");
                        if (ready) sb.Append(" — HAZIR");
                        sb.Append('\n');
                    }

                    if (squad.AllReady)
                        sb.Append("Herkes hazır.");
                    _membersLabel.text = sb.ToString().TrimEnd();
                }
            }

            var myReady = false;
            if (squad != null && OnlineServices.Client.CurrentPlayer != null && squad.ReadyMemberIds != null)
                myReady = squad.ReadyMemberIds.Contains(OnlineServices.Client.CurrentPlayer.Id);
            RestoreReadyToggle(myReady);
        }

        private void RestoreReadyToggle(bool value)
        {
            if (_readyToggle == null) return;
            _readySuppress = true;
            _readyToggle.SetIsOnWithoutNotify(value);
            _readySuppress = false;
        }

        private static string ShortId(Guid id)
        {
            var s = id.ToString("N");
            return s.Length >= 8 ? s.Substring(0, 8) : s;
        }

        private void Update()
        {
            if (_group != null && _fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / UiTheme.FadeDuration);
                _group.alpha = _fade;
            }

            if (_closed || _busy || _current == null)
                return;

            _pollTimer += Time.unscaledDeltaTime;
            if (_pollTimer >= PollInterval)
            {
                _pollTimer = 0f;
                _ = RefreshSquadAsync(true);
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
