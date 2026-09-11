using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using Microsoft.Win32;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using WinFormsTimer = System.Windows.Forms.Timer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new CapsLockIndicatorContext());
    }
}

internal static class AppInfo
{
    public const string CurrentVersion = "1.2.0";
    public const string GitHubRepositoryUrl = "https://github.com/Bserz1331/caps-lock-indicator";
    public const string VersionManifestUrl = "https://raw.githubusercontent.com/Bserz1331/caps-lock-indicator/main/version.json";
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/Bserz1331/caps-lock-indicator/releases/latest";
    public const string LatestReleasePageUrl = "https://github.com/Bserz1331/caps-lock-indicator/releases/latest";
}

internal sealed class OverlaySettings
{
    private const string RegistryPath = @"Software\CapsLockIndicator\Overlay";
    private const string OnColorValueName = "OnColorArgb";
    private const string OffColorValueName = "OffColorArgb";
    private const string TransparencyValueName = "TransparencyPercent";
    private const string DisplayDurationValueName = "DisplayDurationSeconds";

    public static readonly Color DefaultOnColor = Color.FromArgb(38, 184, 101);
    public static readonly Color DefaultOffColor = Color.FromArgb(105, 113, 125);
    public const int DefaultTransparencyPercent = 80;
    public const int MaximumTransparencyPercent = 95;
    public const int DefaultDisplayDurationSeconds = 3;
    public const int MinimumDisplayDurationSeconds = 1;
    public const int MaximumDisplayDurationSeconds = 5;

    public Color OnColor { get; set; }
    public Color OffColor { get; set; }
    public int TransparencyPercent { get; set; }
    public int DisplayDurationSeconds { get; set; }

    public OverlaySettings()
    {
        Reset();
    }

    public OverlaySettings Clone()
    {
        return new OverlaySettings
        {
            OnColor = OnColor,
            OffColor = OffColor,
            TransparencyPercent = TransparencyPercent,
            DisplayDurationSeconds = DisplayDurationSeconds
        };
    }

    public void CopyFrom(OverlaySettings other)
    {
        if (other == null) return;
        OnColor = other.OnColor;
        OffColor = other.OffColor;
        TransparencyPercent = ClampTransparency(other.TransparencyPercent);
        DisplayDurationSeconds = ClampDuration(other.DisplayDurationSeconds);
    }

    public void Reset()
    {
        OnColor = DefaultOnColor;
        OffColor = DefaultOffColor;
        TransparencyPercent = DefaultTransparencyPercent;
        DisplayDurationSeconds = DefaultDisplayDurationSeconds;
    }

    public static OverlaySettings Load()
    {
        OverlaySettings settings = new OverlaySettings();
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key == null) return settings;
                settings.OnColor = ReadColor(key, OnColorValueName, settings.OnColor);
                settings.OffColor = ReadColor(key, OffColorValueName, settings.OffColor);
                settings.TransparencyPercent = ClampTransparency(ReadInt(key, TransparencyValueName, settings.TransparencyPercent));
                settings.DisplayDurationSeconds = ClampDuration(ReadInt(key, DisplayDurationValueName, settings.DisplayDurationSeconds));
            }
        }
        catch
        {
            // Corrupt or inaccessible settings should never prevent the tray tool from starting.
        }
        return settings;
    }

    public bool Save()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null) return false;
                key.SetValue(OnColorValueName, OnColor.ToArgb(), RegistryValueKind.DWord);
                key.SetValue(OffColorValueName, OffColor.ToArgb(), RegistryValueKind.DWord);
                key.SetValue(TransparencyValueName, ClampTransparency(TransparencyPercent), RegistryValueKind.DWord);
                key.SetValue(DisplayDurationValueName, ClampDuration(DisplayDurationSeconds), RegistryValueKind.DWord);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int ReadInt(RegistryKey key, string valueName, int fallback)
    {
        object value = key.GetValue(valueName);
        if (value == null) return fallback;
        int result;
        return Int32.TryParse(Convert.ToString(value), out result) ? result : fallback;
    }

    private static Color ReadColor(RegistryKey key, string valueName, Color fallback)
    {
        object value = key.GetValue(valueName);
        if (value == null) return fallback;
        try
        {
            Color stored = Color.FromArgb(Convert.ToInt32(value));
            return Color.FromArgb(255, stored.R, stored.G, stored.B);
        }
        catch
        {
            return fallback;
        }
    }

    private static int ClampTransparency(int value)
    {
        return Math.Max(0, Math.Min(MaximumTransparencyPercent, value));
    }

    private static int ClampDuration(int value)
    {
        return Math.Max(MinimumDisplayDurationSeconds, Math.Min(MaximumDisplayDurationSeconds, value));
    }
}

internal sealed class CapsLockIndicatorContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem statusItem;
    private readonly ToolStripMenuItem startupItem;
    private readonly Control uiInvoker;
    private readonly OverlaySettings overlaySettings;
    private readonly CapsLockOverlay overlay;
    private readonly WinFormsTimer timer;
    private Icon currentIcon;
    private bool? lastState;
    private bool isExiting;

    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "CapsLockIndicator";

    public CapsLockIndicatorContext()
    {
        overlaySettings = OverlaySettings.Load();
        uiInvoker = new Control();
        uiInvoker.CreateControl();

        menu = new ContextMenuStrip();
        statusItem = new ToolStripMenuItem();
        statusItem.Enabled = false;
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem refreshItem = new ToolStripMenuItem("\u91cd\u65b0\u6574\u7406\u72c0\u614b");
        refreshItem.Click += delegate { UpdateState(); };
        menu.Items.Add(refreshItem);

        ToolStripMenuItem settingsItem = new ToolStripMenuItem("\u6d6e\u52d5\u63d0\u793a\u8a2d\u5b9a\u2026");
        settingsItem.Click += delegate { ShowOverlaySettings(); };
        menu.Items.Add(settingsItem);

        ToolStripMenuItem updateItem = new ToolStripMenuItem("\u6aa2\u67e5\u66f4\u65b0");
        updateItem.Click += delegate { CheckForUpdates(); };
        menu.Items.Add(updateItem);

        startupItem = new ToolStripMenuItem("\u958b\u6a5f\u555f\u52d5");
        startupItem.Checked = IsStartupEnabled();
        startupItem.Click += delegate
        {
            bool enabled = !startupItem.Checked;
            if (SetStartupEnabled(enabled)) startupItem.Checked = enabled;
        };
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem supportItem = new ToolStripMenuItem("\u652f\u6301\u958b\u767c\u2026");
        supportItem.Click += delegate
        {
            using (SupportDialog dialog = new SupportDialog())
            {
                dialog.ShowDialog();
            }
        };
        menu.Items.Add(supportItem);
        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem exitItem = new ToolStripMenuItem("\u7d50\u675f Caps Lock \u6307\u793a\u5668");
        exitItem.Click += delegate { ExitApplication(); };
        menu.Items.Add(exitItem);

        overlay = new CapsLockOverlay(overlaySettings);
        notifyIcon = new NotifyIcon();
        notifyIcon.ContextMenuStrip = menu;
        notifyIcon.DoubleClick += delegate { UpdateState(); };
        notifyIcon.Visible = true;

        timer = new WinFormsTimer();
        timer.Interval = 100;
        timer.Tick += delegate { UpdateState(); };
        timer.Start();

        UpdateState();
    }

    private void UpdateState()
    {
        bool isOn = Control.IsKeyLocked(Keys.CapsLock);
        if (lastState.HasValue && lastState.Value == isOn) return;
        bool stateChanged = lastState.HasValue;
        lastState = isOn;

        if (currentIcon != null)
        {
            currentIcon.Dispose();
            currentIcon = null;
        }

        currentIcon = CreateStatusIcon(isOn);
        notifyIcon.Icon = currentIcon;
        notifyIcon.Text = isOn ? "Caps Lock\uff1a\u958b\u555f" : "Caps Lock\uff1a\u95dc\u9589";
        statusItem.Text = isOn ? "Caps Lock\uff1a\u958b\u555f" : "Caps Lock\uff1a\u95dc\u9589";

        if (stateChanged) overlay.ShowForState(isOn, GetForegroundWindow());
    }

    private void ShowOverlaySettings()
    {
        using (OverlaySettingsDialog dialog = new OverlaySettingsDialog(overlaySettings.Clone()))
        {
            if (dialog.ShowDialog() != DialogResult.OK) return;

            overlaySettings.CopyFrom(dialog.Settings);
            overlay.ApplySettings(overlaySettings);
            if (!overlaySettings.Save())
            {
                MessageBox.Show(
                    "設定已套用到目前執行中的工具，但無法保存到目前使用者設定。",
                    "Caps Lock 指示器",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    private void CheckForUpdates()
    {
        if (!UpdateChecker.TryCheckAsync(uiInvoker, ShowUpdateResult))
        {
            MessageBox.Show(
                "更新檢查正在進行中，請稍候。",
                "Caps Lock 指示器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void ShowUpdateResult(UpdateCheckResult result)
    {
        if (isExiting || result == null) return;

        if (!result.Succeeded)
        {
            MessageBox.Show(
                result.ErrorMessage,
                "檢查更新",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!result.UpdateAvailable)
        {
            MessageBox.Show(
                "目前已是最新版本。" + Environment.NewLine +
                "目前版本：" + result.CurrentVersion + Environment.NewLine +
                "遠端版本：" + result.LatestVersion,
                "檢查更新",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        string message = "發現新版本：" + result.LatestVersion + Environment.NewLine +
            "目前版本：" + result.CurrentVersion + Environment.NewLine + Environment.NewLine +
            "要開啟 GitHub 下載頁面嗎？";
        if (!String.IsNullOrWhiteSpace(result.ReleaseNotes))
        {
            string notes = result.ReleaseNotes.Trim();
            if (notes.Length > 800) notes = notes.Substring(0, 800) + "\u2026";
            message += Environment.NewLine + Environment.NewLine + notes;
        }

        if (MessageBox.Show(message, "檢查更新", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
        {
            OpenExternalUrl(result.DownloadUrl);
        }
    }

    private static Icon CreateStatusIcon(bool isOn)
    {
        const int size = 32;
        IntPtr iconHandle = IntPtr.Zero;

        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);

            Rectangle keycapBounds = new Rectangle(2, 2, size - 4, size - 4);
            using (GraphicsPath keycap = CreateRoundedRectanglePath(keycapBounds, 7))
            using (Brush fill = new SolidBrush(isOn ? Color.FromArgb(47, 197, 112) : Color.FromArgb(226, 229, 234)))
            using (Pen border = new Pen(isOn ? Color.FromArgb(24, 119, 68) : Color.FromArgb(93, 101, 111), 1.2F))
            {
                graphics.FillPath(fill, keycap);
                graphics.DrawPath(border, keycap);
            }

            string glyph = isOn ? "A" : "a";
            float fontSize = isOn ? 23F : 20F;
            Color glyphColor = isOn ? Color.White : Color.FromArgb(57, 64, 74);
            using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush glyphBrush = new SolidBrush(glyphColor))
            using (StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                graphics.DrawString(glyph, font, glyphBrush, new RectangleF(0, -1, size, size + 1), format);
            }

            iconHandle = bitmap.GetHicon();
            try
            {
                return (Icon)Icon.FromHandle(iconHandle).Clone();
            }
            finally
            {
                DestroyIcon(iconHandle);
                iconHandle = IntPtr.Zero;
            }
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static bool IsStartupEnabled()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false))
            {
                if (key == null) return false;
                object value = key.GetValue(StartupValueName);
                return String.Equals(Convert.ToString(value), GetStartupCommand(), StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool SetStartupEnabled(bool enabled)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupRegistryKey))
            {
                if (key == null) throw new InvalidOperationException("無法存取目前使用者的開機啟動設定。");

                if (enabled)
                {
                    key.SetValue(StartupValueName, GetStartupCommand(), RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(StartupValueName, false);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "無法更新開機啟動設定。" + Environment.NewLine + ex.Message,
                "Caps Lock 指示器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    private static string GetStartupCommand()
    {
        return "\"" + Application.ExecutablePath + "\"";
    }

    private void ExitApplication()
    {
        if (isExiting) return;
        isExiting = true;
        timer.Stop();
        timer.Dispose();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        if (currentIcon != null) currentIcon.Dispose();
        overlay.Hide();
        overlay.Dispose();
        menu.Dispose();
        uiInvoker.Dispose();
        ExitThread();
    }

    private static void OpenExternalUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "無法開啟瀏覽器。" + Environment.NewLine + url + Environment.NewLine + ex.Message,
                "Caps Lock 指示器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}

internal sealed class UpdateCheckResult
{
    public bool Succeeded { get; private set; }
    public bool UpdateAvailable { get; private set; }
    public string CurrentVersion { get; private set; }
    public string LatestVersion { get; private set; }
    public string DownloadUrl { get; private set; }
    public string ReleaseNotes { get; private set; }
    public string ErrorMessage { get; private set; }

    public static UpdateCheckResult Success(string currentVersion, string latestVersion, string downloadUrl, string releaseNotes)
    {
        Version current = new Version(currentVersion);
        Version latest = new Version(latestVersion);
        return new UpdateCheckResult
        {
            Succeeded = true,
            UpdateAvailable = latest > current,
            CurrentVersion = currentVersion,
            LatestVersion = latestVersion,
            DownloadUrl = NormalizeUrl(downloadUrl),
            ReleaseNotes = releaseNotes ?? String.Empty
        };
    }

    public static UpdateCheckResult Failure(string message)
    {
        return new UpdateCheckResult
        {
            Succeeded = false,
            ErrorMessage = message
        };
    }

    private static string NormalizeUrl(string value)
    {
        Uri uri;
        if (Uri.TryCreate(value, UriKind.Absolute, out uri) &&
            (String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return uri.ToString();
        }
        return AppInfo.LatestReleasePageUrl;
    }
}

internal static class UpdateChecker
{
    private static int isChecking;

    public static bool TryCheckAsync(Control dispatcher, Action<UpdateCheckResult> completed)
    {
        if (dispatcher == null || completed == null) throw new ArgumentNullException();
        if (Interlocked.Exchange(ref isChecking, 1) != 0) return false;

        ThreadPool.QueueUserWorkItem(delegate
        {
            UpdateCheckResult result;
            try
            {
                result = CheckLatest();
            }
            catch (Exception ex)
            {
                result = UpdateCheckResult.Failure("無法檢查更新。" + Environment.NewLine + ex.Message);
            }

            try
            {
                dispatcher.BeginInvoke(new Action(delegate
                {
                    Interlocked.Exchange(ref isChecking, 0);
                    completed(result);
                }));
            }
            catch
            {
                Interlocked.Exchange(ref isChecking, 0);
            }
        });
        return true;
    }

    private static UpdateCheckResult CheckLatest()
    {
        RemoteVersionInfo info = null;
        string manifestError = String.Empty;
        string releaseError = String.Empty;

        try
        {
            info = ParseVersionManifest(DownloadJson(AppInfo.VersionManifestUrl));
        }
        catch (Exception ex)
        {
            manifestError = ex.Message;
        }

        if (info == null)
        {
            try
            {
                info = ParseLatestRelease(DownloadJson(AppInfo.LatestReleaseApiUrl));
            }
            catch (Exception ex)
            {
                releaseError = ex.Message;
            }
        }

        if (info == null)
        {
            return UpdateCheckResult.Failure(
                "目前無法取得 GitHub 更新資訊。" + Environment.NewLine +
                "請確認網路連線，或稍後再試。" + Environment.NewLine + Environment.NewLine +
                "version.json：" + manifestError + Environment.NewLine +
                "GitHub Release：" + releaseError);
        }

        Version latestVersion;
        if (!TryParseVersion(info.Version, out latestVersion))
        {
            return UpdateCheckResult.Failure("GitHub 的版本資訊格式不正確：" + info.Version);
        }

        return UpdateCheckResult.Success(
            AppInfo.CurrentVersion,
            latestVersion.ToString(),
            info.DownloadUrl,
            info.ReleaseNotes);
    }

    private static RemoteVersionInfo ParseVersionManifest(string json)
    {
        Dictionary<string, object> values = Deserialize(json);
        string version = ReadString(values, "version");
        if (String.IsNullOrWhiteSpace(version)) throw new FormatException("缺少 version 欄位。");

        return new RemoteVersionInfo
        {
            Version = version,
            DownloadUrl = ReadString(values, "downloadUrl"),
            ReleaseNotes = ReadString(values, "notes")
        };
    }

    private static RemoteVersionInfo ParseLatestRelease(string json)
    {
        Dictionary<string, object> values = Deserialize(json);
        string version = ReadString(values, "tag_name");
        if (String.IsNullOrWhiteSpace(version)) throw new FormatException("Release 缺少 tag_name 欄位。");

        return new RemoteVersionInfo
        {
            Version = version,
            DownloadUrl = ReadString(values, "html_url"),
            ReleaseNotes = ReadString(values, "body")
        };
    }

    private static Dictionary<string, object> Deserialize(string json)
    {
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        Dictionary<string, object> values = serializer.Deserialize<Dictionary<string, object>>(json);
        if (values == null) throw new FormatException("回應不是有效的 JSON 物件。");
        return values;
    }

    private static string ReadString(Dictionary<string, object> values, string key)
    {
        object value;
        return values.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : String.Empty;
    }

    private static bool TryParseVersion(string value, out Version version)
    {
        string normalized = (value ?? String.Empty).Trim();
        while (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(1).Trim();
        }

        return Version.TryParse(normalized, out version);
    }

    private static string DownloadJson(string url)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";
        request.Accept = "application/json";
        request.UserAgent = "CapsLockIndicator/" + AppInfo.CurrentVersion;
        request.Timeout = 5000;
        request.ReadWriteTimeout = 5000;
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream stream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    private sealed class RemoteVersionInfo
    {
        public string Version;
        public string DownloadUrl;
        public string ReleaseNotes;
    }
}

internal sealed class CapsLockOverlay : Form
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;
    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;
    private const int HtTransparent = -1;
    private const int MaNoActivate = 3;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private static readonly IntPtr HwndTopmost = new IntPtr(-1);
    private readonly WinFormsTimer hideTimer;
    private Color onColor;
    private Color offColor;
    private bool isOn;

    public CapsLockOverlay(OverlaySettings settings)
    {
        Text = "Caps Lock";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        ShowIcon = false;
        BackColor = Color.FromArgb(24, 27, 33);
        ClientSize = new Size(240, 240);
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        using (GraphicsPath shape = CreateRoundedRectanglePath(new Rectangle(0, 0, ClientSize.Width, ClientSize.Height), 24))
        {
            Region = new Region(shape);
        }

        hideTimer = new WinFormsTimer();
        hideTimer.Tick += delegate
        {
            hideTimer.Stop();
            Hide();
        };

        ApplySettings(settings);
    }

    public void ApplySettings(OverlaySettings settings)
    {
        if (settings == null) return;
        onColor = settings.OnColor;
        offColor = settings.OffColor;
        Opacity = (100 - settings.TransparencyPercent) / 100.0;
        hideTimer.Interval = settings.DisplayDurationSeconds * 1000;
        if (Visible) Invalidate();
    }

    public void ShowForState(bool enabled, IntPtr targetWindow)
    {
        isOn = enabled;
        Screen screen = GetTargetScreen(targetWindow);
        Location = new Point(
            screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2,
            screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2);
        Invalidate();

        hideTimer.Stop();
        if (!Visible) Show();

        SetWindowPos(
            Handle,
            HwndTopmost,
            Location.X,
            Location.Y,
            Width,
            Height,
            SwpNoActivate | SwpShowWindow);
        hideTimer.Start();
    }

    private static Screen GetTargetScreen(IntPtr targetWindow)
    {
        if (targetWindow != IntPtr.Zero)
        {
            try
            {
                return Screen.FromHandle(targetWindow);
            }
            catch
            {
                // Fall back to the primary screen when the foreground handle is no longer valid.
            }
        }

        return Screen.PrimaryScreen;
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExLayered | WsExNoActivate;
            return parameters;
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmNcHitTest)
        {
            message.Result = new IntPtr(HtTransparent);
            return;
        }

        if (message.Msg == WmMouseActivate)
        {
            message.Result = new IntPtr(MaNoActivate);
            return;
        }

        base.WndProc(ref message);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        Rectangle cardBounds = new Rectangle(10, 10, ClientSize.Width - 20, ClientSize.Height - 20);
        using (GraphicsPath card = CreateRoundedRectanglePath(cardBounds, 20))
        using (Brush fill = new SolidBrush(isOn ? onColor : offColor))
        using (Pen border = new Pen(isOn ? Color.FromArgb(122, 245, 167) : Color.FromArgb(186, 192, 201), 2F))
        {
            e.Graphics.FillPath(fill, card);
            e.Graphics.DrawPath(border, card);
        }

        string glyph = isOn ? "A" : "a";
        float fontSize = isOn ? 150F : 132F;
        using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Brush glyphBrush = new SolidBrush(Color.White))
        using (StringFormat format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        })
        {
            e.Graphics.DrawString(glyph, font, glyphBrush, new RectangleF(0, 12, ClientSize.Width, 174), format);
        }

        using (Font captionFont = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Pixel))
        using (Brush captionBrush = new SolidBrush(Color.FromArgb(235, 240, 245)))
        using (StringFormat captionFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        })
        {
            e.Graphics.DrawString("CAPS LOCK", captionFont, captionBrush, new RectangleF(0, 196, ClientSize.Width, 24), captionFormat);
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    protected override void Dispose(bool disposing)
    {
        if (disposing && hideTimer != null) hideTimer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class OverlaySettingsDialog : Form
{
    private readonly OverlaySettings settings;
    private readonly Panel onColorSwatch;
    private readonly Panel offColorSwatch;
    private readonly Label onColorValue;
    private readonly Label offColorValue;
    private readonly TrackBar transparencyTrackBar;
    private readonly Label transparencyValue;
    private readonly TextBox displayDurationInput;

    public OverlaySettings Settings
    {
        get { return settings; }
    }

    public OverlaySettingsDialog(OverlaySettings initialSettings)
    {
        settings = (initialSettings ?? new OverlaySettings()).Clone();

        Text = "\u6d6e\u52d5\u63d0\u793a\u8a2d\u5b9a";
        BackColor = Color.FromArgb(20, 21, 23);
        ForeColor = Color.FromArgb(238, 240, 242);
        Font = new Font("Microsoft JhengHei UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(500, 390);
        AutoScaleMode = AutoScaleMode.Dpi;

        Controls.Add(MakeLabel("\u6d6e\u52d5\u63d0\u793a\u8a2d\u5b9a", 22, 18, 300, 32, 19F, ForeColor, FontStyle.Bold));
        Controls.Add(MakeLabel("\u8abf\u6574\u4e2d\u592e\u72c0\u614b\u63d0\u793a\u7684\u984f\u8272\u8207\u900f\u660e\u5ea6\uff0c\u6309\u300c\u5957\u7528\u300d\u5f8c\u5373\u6642\u751f\u6548\u3002", 22, 52, 450, 24, 9F, Color.FromArgb(166, 170, 177)));

        Controls.Add(MakeLabel("Caps Lock \u958b\u555f\u984f\u8272", 24, 100, 170, 26, 10F, ForeColor, FontStyle.Bold));
        onColorSwatch = MakeColorSwatch(settings.OnColor);
        onColorSwatch.SetBounds(210, 96, 34, 30);
        Controls.Add(onColorSwatch);
        onColorValue = MakeLabel(ColorHex(settings.OnColor), 254, 100, 100, 26, 9F, Color.FromArgb(195, 199, 205));
        Controls.Add(onColorValue);
        Button chooseOn = MakeButton("\u9078\u64c7\u984f\u8272", 370, 94, 104, 32, true);
        chooseOn.Click += delegate { ChooseColor(true); };
        Controls.Add(chooseOn);

        Controls.Add(MakeLabel("Caps Lock \u95dc\u9589\u984f\u8272", 24, 146, 170, 26, 10F, ForeColor, FontStyle.Bold));
        offColorSwatch = MakeColorSwatch(settings.OffColor);
        offColorSwatch.SetBounds(210, 142, 34, 30);
        Controls.Add(offColorSwatch);
        offColorValue = MakeLabel(ColorHex(settings.OffColor), 254, 146, 100, 26, 9F, Color.FromArgb(195, 199, 205));
        Controls.Add(offColorValue);
        Button chooseOff = MakeButton("\u9078\u64c7\u984f\u8272", 370, 140, 104, 32, true);
        chooseOff.Click += delegate { ChooseColor(false); };
        Controls.Add(chooseOff);

        Controls.Add(MakeLabel("\u900f\u660e\u5ea6", 24, 192, 120, 26, 10F, ForeColor, FontStyle.Bold));
        transparencyTrackBar = new TrackBar
        {
            Minimum = 0,
            Maximum = OverlaySettings.MaximumTransparencyPercent,
            TickFrequency = 10,
            SmallChange = 1,
            LargeChange = 10,
            Value = settings.TransparencyPercent,
            BackColor = BackColor
        };
        transparencyTrackBar.SetBounds(130, 184, 250, 42);
        transparencyTrackBar.ValueChanged += delegate
        {
            settings.TransparencyPercent = transparencyTrackBar.Value;
            UpdateTransparencyValue();
        };
        Controls.Add(transparencyTrackBar);
        transparencyValue = MakeLabel(String.Empty, 386, 192, 88, 26, 9F, Color.FromArgb(195, 199, 205), FontStyle.Regular, ContentAlignment.MiddleRight);
        Controls.Add(transparencyValue);
        Controls.Add(MakeLabel("\u9810\u8a2d\u70ba 80% \u900f\u660e\uff08\u8996\u7a97\u4e0d\u900f\u660e\u5ea6\u7d04 20%\uff09\uff0c\u70ba\u907f\u514d\u7121\u6cd5\u627e\u56de\uff0c\u6700\u9ad8\u53ef\u8a2d 95%\u3002", 24, 226, 450, 22, 8.5F, Color.FromArgb(132, 137, 145)));

        Controls.Add(MakeLabel("\u986f\u793a\u6642\u9593", 24, 262, 120, 26, 10F, ForeColor, FontStyle.Bold));
        displayDurationInput = new TextBox
        {
            Text = settings.DisplayDurationSeconds.ToString(),
            MaxLength = 1,
            TextAlign = HorizontalAlignment.Center,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(34, 36, 40),
            ForeColor = Color.FromArgb(238, 240, 242),
            Font = new Font("Microsoft JhengHei UI", 9F)
        };
        displayDurationInput.SetBounds(130, 256, 76, 30);
        displayDurationInput.KeyPress += delegate(object sender, KeyPressEventArgs e)
        {
            if (!Char.IsControl(e.KeyChar) && !Char.IsDigit(e.KeyChar)) e.Handled = true;
        };
        Controls.Add(displayDurationInput);
        Controls.Add(MakeLabel("\u79d2\uff08\u9810\u8a2d 3 \u79d2\uff0c\u53ef\u8a2d 1\u20135 \u79d2\uff09", 218, 262, 256, 26, 9F, Color.FromArgb(195, 199, 205)));

        Button reset = MakeButton("\u6062\u5fa9\u9810\u8a2d", 24, 326, 104, 34, false);
        reset.Click += delegate
        {
            settings.Reset();
            transparencyTrackBar.Value = settings.TransparencyPercent;
            displayDurationInput.Text = settings.DisplayDurationSeconds.ToString();
            UpdateColorControls();
            UpdateTransparencyValue();
        };
        Controls.Add(reset);

        Button cancel = MakeButton("\u53d6\u6d88", 302, 326, 80, 34, false);
        cancel.DialogResult = DialogResult.Cancel;
        Controls.Add(cancel);

        Button apply = MakeButton("\u5957\u7528", 390, 326, 84, 34, true);
        apply.Click += delegate
        {
            int duration;
            if (!Int32.TryParse(displayDurationInput.Text.Trim(), out duration) ||
                duration < OverlaySettings.MinimumDisplayDurationSeconds ||
                duration > OverlaySettings.MaximumDisplayDurationSeconds)
            {
                MessageBox.Show(
                    this,
                    "顯示時間請輸入 1 到 5 之間的整數。",
                    "浮動提示設定",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                displayDurationInput.Focus();
                displayDurationInput.SelectAll();
                return;
            }

            settings.DisplayDurationSeconds = duration;
            DialogResult = DialogResult.OK;
        };
        Controls.Add(apply);
        AcceptButton = apply;
        CancelButton = cancel;

        UpdateTransparencyValue();
    }

    private void ChooseColor(bool onState)
    {
        using (ColorDialog dialog = new ColorDialog())
        {
            dialog.AllowFullOpen = true;
            dialog.FullOpen = true;
            dialog.Color = onState ? settings.OnColor : settings.OffColor;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (onState) settings.OnColor = dialog.Color;
            else settings.OffColor = dialog.Color;
            UpdateColorControls();
        }
    }

    private void UpdateColorControls()
    {
        onColorSwatch.BackColor = settings.OnColor;
        offColorSwatch.BackColor = settings.OffColor;
        onColorValue.Text = ColorHex(settings.OnColor);
        offColorValue.Text = ColorHex(settings.OffColor);
    }

    private void UpdateTransparencyValue()
    {
        transparencyValue.Text = "\u900f\u660e " + settings.TransparencyPercent + "%";
    }

    private static Panel MakeColorSwatch(Color color)
    {
        Panel panel = new Panel { BackColor = color };
        panel.BorderStyle = BorderStyle.FixedSingle;
        return panel;
    }

    private static string ColorHex(Color color)
    {
        return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
    }

    private static Label MakeLabel(string text, int x, int y, int width, int height, float size,
        Color color, FontStyle style = FontStyle.Regular, ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        Label label = new Label
        {
            Text = text,
            ForeColor = color,
            BackColor = Color.Transparent,
            Font = new Font("Microsoft JhengHei UI", size, style),
            AutoSize = false,
            TextAlign = alignment
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static Button MakeButton(string text, int x, int y, int width, int height, bool accent)
    {
        Button button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ? Color.FromArgb(19, 39, 35) : Color.FromArgb(20, 22, 25),
            ForeColor = accent ? Color.FromArgb(79, 214, 163) : Color.FromArgb(231, 233, 236),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = accent ? Color.FromArgb(39, 111, 91) : Color.FromArgb(59, 64, 71);
        button.SetBounds(x, y, width, height);
        return button;
    }
}

internal sealed class SupportDialog : Form
{
    private const string KoFiUrl = "https://ko-fi.com/minz_space_cat";
    private const string Bep20Address = "0x7a4E3D8D9684196E4F96a6a28c49D3a1a785A0b5";
    private const string Trc20Address = "TNm7kRfeFo2wa1TVtz5EoNNBS8LFahoe7j";
    private readonly ToolTip toolTip;

    public SupportDialog()
    {
        Text = "\u652f\u6301\u958b\u767c";
        BackColor = Color.FromArgb(20, 21, 23);
        ForeColor = Color.FromArgb(238, 240, 242);
        Font = new Font("Microsoft JhengHei UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 360);
        AutoScaleMode = AutoScaleMode.Dpi;
        toolTip = new ToolTip();

        Controls.Add(MakeLabel("\u81ea\u9858\u652f\u6301", 16, 14, 160, 20, 9F, Color.FromArgb(82, 201, 151), FontStyle.Bold));
        Controls.Add(MakeLabel("\u652f\u6301\u958b\u767c", 16, 36, 300, 36, 20F, ForeColor, FontStyle.Bold));
        Controls.Add(MakeLabel("\u5982\u679c\u9019\u500b\u5c0f\u5de5\u5177\u5c0d\u4f60\u6709\u5e6b\u52a9\uff0c\u53ef\u4ee5\u652f\u6301\u5f8c\u7e8c\u7dad\u8b77\u8207\u6539\u5584\u3002", 16, 78, 580, 24, 10F, Color.FromArgb(166, 170, 177)));

        Panel koFi = CreateCard(Color.FromArgb(19, 39, 35), Color.FromArgb(39, 111, 91), 16, 112, 588, 62);
        koFi.Controls.Add(MakeLabel("\u2615", 16, 13, 34, 32, 17F, Color.White, FontStyle.Regular));
        koFi.Controls.Add(MakeLabel("\u900f\u904e Ko-fi \u652f\u6301", 58, 9, 280, 24, 12F, ForeColor, FontStyle.Bold));
        koFi.Controls.Add(MakeLabel("\u524d\u5f80\u5b87\u822a\u8c93\u7684 Ko-fi \u9801\u9762", 58, 35, 330, 18, 9F, Color.FromArgb(155, 161, 168)));
        Button open = MakeButton("\u958b\u555f \u2197", 486, 15, 86, 32, true);
        open.Click += delegate { OpenUrl(KoFiUrl); };
        koFi.Controls.Add(open);
        koFi.Cursor = Cursors.Hand;
        koFi.Click += delegate { OpenUrl(KoFiUrl); };
        Controls.Add(koFi);

        Controls.Add(CreateWalletCard("USDT | BEP20", "BNB Smart Chain", Bep20Address, 16, 188));
        Controls.Add(CreateWalletCard("USDT | TRC20", "TRON", Trc20Address, 322, 188));

        Panel warning = CreateCard(Color.FromArgb(42, 36, 20), Color.FromArgb(144, 103, 26), 16, 314, 588, 30);
        warning.Controls.Add(MakeLabel("\u8f49\u5e33\u524d\u8acb\u518d\u6b21\u78ba\u8a8d\uff1a\u50c5\u63a5\u53d7\u4e0a\u65b9\u6a19\u793a\u7db2\u8def\u7684 USDT\uff0c\u5efa\u8b70\u5148\u5c0f\u984d\u6e2c\u8a66\u3002", 12, 5, 564, 20, 8.5F, Color.FromArgb(238, 181, 43), FontStyle.Bold));
        Controls.Add(warning);
        Controls.Add(MakeLabel("\u652f\u6301\u4e0d\u5f71\u97ff Caps Lock \u72c0\u614b\u5075\u6e2c\u6216\u4efb\u4f55\u7a0b\u5f0f\u529f\u80fd\u3002", 16, 344, 588, 16, 8F, Color.FromArgb(115, 119, 126), FontStyle.Regular, ContentAlignment.MiddleCenter));
    }

    private Panel CreateWalletCard(string title, string network, string address, int x, int y)
    {
        Panel card = CreateCard(Color.FromArgb(15, 16, 18), Color.FromArgb(49, 53, 59), x, y, 282, 112);
        card.Controls.Add(MakeLabel(title, 14, 11, 240, 22, 11F, Color.FromArgb(235, 237, 240), FontStyle.Bold));
        card.Controls.Add(MakeLabel(network, 14, 34, 240, 18, 8.5F, Color.FromArgb(146, 151, 159)));
        Label addressLabel = MakeLabel(address, 14, 55, 252, 20, 8F, Color.FromArgb(202, 205, 210), FontStyle.Bold);
        addressLabel.AutoEllipsis = true;
        toolTip.SetToolTip(addressLabel, address);
        card.Controls.Add(addressLabel);
        Button copy = MakeButton("\u8907\u88fd\u5730\u5740", 14, 79, 100, 27, false);
        copy.Click += delegate
        {
            try
            {
                Clipboard.SetText(address);
                toolTip.Show("\u5df2\u8907\u88fd", copy, 1000);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "\u7121\u6cd5\u8907\u88fd\u5730\u5740\uff0c\u8acb\u7a0d\u5f8c\u518d\u8a66\u3002" + Environment.NewLine + ex.Message,
                    "\u652f\u6301\u958b\u767c", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        card.Controls.Add(copy);
        return card;
    }

    private static Panel CreateCard(Color fillColor, Color borderColor, int x, int y, int width, int height)
    {
        Panel panel = new Panel { BackColor = fillColor };
        panel.SetBounds(x, y, width, height);
        panel.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(borderColor))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
            }
        };
        return panel;
    }

    private static Label MakeLabel(string text, int x, int y, int width, int height, float size,
        Color color, FontStyle style = FontStyle.Regular, ContentAlignment alignment = ContentAlignment.MiddleLeft)
    {
        Label label = new Label
        {
            Text = text,
            ForeColor = color,
            BackColor = Color.Transparent,
            Font = new Font("Microsoft JhengHei UI", size, style),
            AutoSize = false,
            TextAlign = alignment
        };
        label.SetBounds(x, y, width, height);
        return label;
    }

    private static Button MakeButton(string text, int x, int y, int width, int height, bool accent)
    {
        Button button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ? Color.FromArgb(19, 39, 35) : Color.FromArgb(20, 22, 25),
            ForeColor = accent ? Color.FromArgb(79, 214, 163) : Color.FromArgb(231, 233, 236),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = accent ? Color.FromArgb(39, 111, 91) : Color.FromArgb(59, 64, 71);
        button.SetBounds(x, y, width, height);
        return button;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("\u7121\u6cd5\u958b\u555f\u700f\u89bd\u5668\uff0c\u7db2\u5740\u70ba\uff1a" + Environment.NewLine + url + Environment.NewLine + ex.Message,
                "\u652f\u6301\u958b\u767c", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && toolTip != null) toolTip.Dispose();
        base.Dispose(disposing);
    }
}
