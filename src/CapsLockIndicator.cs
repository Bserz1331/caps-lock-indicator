using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

internal sealed class CapsLockIndicatorContext : ApplicationContext
{
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;
    private readonly ToolStripMenuItem statusItem;
    private readonly ToolStripMenuItem startupItem;
    private readonly Timer timer;
    private Icon currentIcon;
    private bool? lastState;
    private bool isExiting;

    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "CapsLockIndicator";

    public CapsLockIndicatorContext()
    {
        menu = new ContextMenuStrip();
        statusItem = new ToolStripMenuItem();
        statusItem.Enabled = false;
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem refreshItem = new ToolStripMenuItem("\u91cd\u65b0\u6574\u7406\u72c0\u614b");
        refreshItem.Click += delegate { UpdateState(); };
        menu.Items.Add(refreshItem);

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

        notifyIcon = new NotifyIcon();
        notifyIcon.ContextMenuStrip = menu;
        notifyIcon.DoubleClick += delegate { UpdateState(); };
        notifyIcon.Visible = true;

        timer = new Timer();
        timer.Interval = 100;
        timer.Tick += delegate { UpdateState(); };
        timer.Start();

        UpdateState();
    }

    private void UpdateState()
    {
        bool isOn = Control.IsKeyLocked(Keys.CapsLock);
        if (lastState.HasValue && lastState.Value == isOn) return;
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
    }

    private static Icon CreateStatusIcon(bool isOn)
    {
        Icon baseIcon = null;
        Bitmap bitmap = null;
        Graphics graphics = null;
        IntPtr iconHandle = IntPtr.Zero;

        try
        {
            baseIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (baseIcon == null) baseIcon = SystemIcons.Information;
            bitmap = baseIcon.ToBitmap();
            graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int diameter = Math.Max(8, bitmap.Width / 3);
            int x = bitmap.Width - diameter - 1;
            int y = bitmap.Height - diameter - 1;
            Rectangle dot = new Rectangle(x, y, diameter, diameter);
            using (Brush brush = new SolidBrush(isOn ? Color.FromArgb(47, 197, 112) : Color.FromArgb(145, 145, 145)))
            using (Pen pen = new Pen(Color.White, Math.Max(1, bitmap.Width / 16)))
            {
                graphics.FillEllipse(brush, dot);
                graphics.DrawEllipse(pen, dot);
            }

            iconHandle = bitmap.GetHicon();
            Icon result = (Icon)Icon.FromHandle(iconHandle).Clone();
            DestroyIcon(iconHandle);
            iconHandle = IntPtr.Zero;
            return result;
        }
        finally
        {
            if (iconHandle != IntPtr.Zero) DestroyIcon(iconHandle);
            if (graphics != null) graphics.Dispose();
            if (bitmap != null) bitmap.Dispose();
            if (baseIcon != null) baseIcon.Dispose();
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

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
        menu.Dispose();
        ExitThread();
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
