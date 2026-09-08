using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    private readonly Timer timer;
    private Icon currentIcon;
    private bool? lastState;
    private bool isExiting;

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
