using Harekat.Launcher.Services;

namespace Harekat.Launcher;

public sealed class MainForm : Form
{
    private readonly LauncherSettings _settings;
    private readonly BackendApiClient _api;
    private readonly PatchService _patch = new();
    private readonly string _installDir;

    private readonly ListBox _newsList = new();
    private readonly TextBox _newsBody = new();
    private readonly Label _statusLabel = new();
    private readonly Label _versionLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly TextBox _username = new();
    private readonly TextBox _password = new();
    private readonly Button _loginBtn = new();
    private readonly Button _updateBtn = new();
    private readonly Button _playBtn = new();

    private ClientVersionDto? _remote;
    private string? _accessToken;
    private bool _busy;

    public MainForm(LauncherSettings settings)
    {
        _settings = settings;
        _api = new BackendApiClient(settings.ApiBaseUrl);
        _installDir = ResolveInstallDir(settings);

        Text = "HAREKÂT Launcher";
        Width = 920;
        Height = 620;
        MinimumSize = new Size(780, 520);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 22, 18);
        ForeColor = Color.FromArgb(230, 232, 220);
        Font = new Font("Segoe UI", 10f);

        BuildUi();
        Shown += async (_, _) => await RefreshAsync();
        FormClosed += (_, _) => _api.Dispose();
    }

    private void BuildUi()
    {
        var header = new Label
        {
            Text = "HAREKÂT",
            Font = new Font("Segoe UI Semibold", 22f),
            ForeColor = Color.FromArgb(200, 180, 90),
            AutoSize = true,
            Location = new Point(24, 16)
        };

        _versionLabel.AutoSize = true;
        _versionLabel.Location = new Point(24, 56);
        _versionLabel.ForeColor = Color.FromArgb(160, 170, 150);

        var newsTitle = new Label
        {
            Text = _settings.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "News" : "Haberler",
            Location = new Point(24, 90),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 11f)
        };

        _newsList.Location = new Point(24, 118);
        _newsList.Size = new Size(320, 280);
        _newsList.BackColor = Color.FromArgb(28, 34, 28);
        _newsList.ForeColor = ForeColor;
        _newsList.BorderStyle = BorderStyle.FixedSingle;
        _newsList.SelectedIndexChanged += (_, _) =>
        {
            if (_newsList.SelectedItem is NewsItemDto n)
                _newsBody.Text = $"{n.Title}\r\n\r\n{n.Body}\r\n\r\n{n.PublishedAt:u}";
        };

        _newsBody.Location = new Point(360, 118);
        _newsBody.Size = new Size(520, 280);
        _newsBody.Multiline = true;
        _newsBody.ReadOnly = true;
        _newsBody.ScrollBars = ScrollBars.Vertical;
        _newsBody.BackColor = Color.FromArgb(28, 34, 28);
        _newsBody.ForeColor = ForeColor;
        _newsBody.BorderStyle = BorderStyle.FixedSingle;

        var loginPanel = new Panel
        {
            Location = new Point(24, 420),
            Size = new Size(420, 100)
        };
        var userLbl = new Label { Text = "Kullanıcı", Location = new Point(0, 4), AutoSize = true };
        _username.Location = new Point(0, 26);
        _username.Width = 180;
        _username.BackColor = Color.FromArgb(28, 34, 28);
        _username.ForeColor = ForeColor;
        var passLbl = new Label { Text = "Şifre", Location = new Point(200, 4), AutoSize = true };
        _password.Location = new Point(200, 26);
        _password.Width = 180;
        _password.UseSystemPasswordChar = true;
        _password.BackColor = Color.FromArgb(28, 34, 28);
        _password.ForeColor = ForeColor;
        _loginBtn.Text = "Giriş";
        _loginBtn.Location = new Point(0, 60);
        _loginBtn.Width = 100;
        _loginBtn.FlatStyle = FlatStyle.Flat;
        _loginBtn.Click += async (_, _) => await LoginAsync();
        loginPanel.Controls.AddRange([userLbl, _username, passLbl, _password, _loginBtn]);

        _updateBtn.Text = "Güncelle";
        _updateBtn.Location = new Point(620, 430);
        _updateBtn.Size = new Size(120, 40);
        _updateBtn.FlatStyle = FlatStyle.Flat;
        _updateBtn.Enabled = false;
        _updateBtn.Click += async (_, _) => await UpdateAsync();

        _playBtn.Text = "OYNA";
        _playBtn.Location = new Point(760, 430);
        _playBtn.Size = new Size(120, 40);
        _playBtn.FlatStyle = FlatStyle.Flat;
        _playBtn.BackColor = Color.FromArgb(70, 110, 60);
        _playBtn.ForeColor = Color.White;
        _playBtn.Click += (_, _) => Play();

        _progress.Location = new Point(24, 530);
        _progress.Size = new Size(856, 18);
        _progress.Style = ProgressBarStyle.Continuous;

        _statusLabel.Location = new Point(24, 552);
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = Color.FromArgb(160, 170, 150);
        _statusLabel.Text = "Hazırlanıyor…";

        Controls.AddRange([
            header, _versionLabel, newsTitle, _newsList, _newsBody,
            loginPanel, _updateBtn, _playBtn, _progress, _statusLabel
        ]);
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var local = PatchService.ReadLocalVersion(_installDir);
            _statusLabel.Text = "Sunucuya bağlanılıyor…";

            var newsTask = _api.GetNewsAsync(_settings.Language);
            var verTask = _api.GetVersionAsync(_settings.Channel);
            await Task.WhenAll(newsTask, verTask);

            _newsList.Items.Clear();
            foreach (var n in newsTask.Result)
                _newsList.Items.Add(n);
            if (_newsList.Items.Count > 0)
                _newsList.SelectedIndex = 0;

            _remote = verTask.Result;
            var needsUpdate = PatchService.CompareVersions(local, _remote.Version) < 0;
            _versionLabel.Text = $"Yerel: {local}  ·  Sunucu: {_remote.Version} ({_remote.Channel})"
                                 + (needsUpdate ? "  — güncelleme var" : "  — güncel");
            _updateBtn.Enabled = needsUpdate;
            _playBtn.Enabled = !(_remote.Mandatory && needsUpdate);
            _statusLabel.Text = needsUpdate
                ? (_remote.Mandatory ? "Zorunlu güncelleme gerekli." : "Güncelleme mevcut.")
                : "Hazır.";
            _progress.Value = 0;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Bağlantı hatası: " + ex.Message;
            _playBtn.Enabled = true;
            _updateBtn.Enabled = false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoginAsync()
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var auth = await _api.LoginAsync(_username.Text.Trim(), _password.Text);
            _accessToken = auth.AccessToken;
            _statusLabel.Text = "Giriş başarılı. Token oyuna aktarılacak.";
            _loginBtn.Text = "Giriş ✓";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            MessageBox.Show(this, ex.Message, "Giriş", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task UpdateAsync()
    {
        if (_busy || _remote is null) return;
        SetBusy(true);
        _playBtn.Enabled = false;
        try
        {
            var progress = new Progress<(string Status, int Percent)>(p =>
            {
                _statusLabel.Text = p.Status;
                _progress.Value = Math.Clamp(p.Percent, 0, 100);
            });
            await _patch.ApplyAsync(_remote, _installDir, progress);
            _updateBtn.Enabled = false;
            _playBtn.Enabled = true;
            _versionLabel.Text = $"Yerel: {_remote.Version}  ·  Sunucu: {_remote.Version} ({_remote.Channel})  — güncel";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Güncelleme hatası: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Güncelleme", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (_remote is { Mandatory: false })
                _playBtn.Enabled = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Play()
    {
        try
        {
            var exe = Path.GetFullPath(Path.Combine(_installDir, _settings.GameExecutable));
            GameProcess.Start(exe, _settings.GameArgs, _accessToken);
            _statusLabel.Text = "Oyun başlatıldı.";
            WindowState = FormWindowState.Minimized;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "OYNA", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _statusLabel.Text = ex.Message;
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UseWaitCursor = busy;
        _loginBtn.Enabled = !busy;
        if (!busy && _remote is not null)
        {
            var local = PatchService.ReadLocalVersion(_installDir);
            var needs = PatchService.CompareVersions(local, _remote.Version) < 0;
            _updateBtn.Enabled = needs;
        }
        else if (busy)
        {
            _updateBtn.Enabled = false;
        }
    }

    private static string ResolveInstallDir(LauncherSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.InstallDir))
            return Path.GetFullPath(settings.InstallDir);
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, settings.GameRelativeDir));
    }
}
