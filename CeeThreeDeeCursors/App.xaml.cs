using System.Windows;
using Application = System.Windows.Application;
using CeeThreeDeeCursors.Models;
using CeeThreeDeeCursors.Services;

namespace CeeThreeDeeCursors;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ToolStripMenuItem? _toggleMenuItem;
    private OverlayWindow? _overlay;
    private CrosshairSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settings = SettingsService.Load();

        _overlay = new OverlayWindow(_settings);
        _overlay.OnSettingsHotkeyPressed = OpenSettings;
        _overlay.IsVisibleChanged += (_, _) => UpdateToggleMenuText();
        _overlay.Show();
        _overlay.Visibility = Visibility.Hidden;

        BuildTrayIcon();
    }

    private void BuildTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        _toggleMenuItem = new System.Windows.Forms.ToolStripMenuItem("Show Crosshair");
        _toggleMenuItem.Click += (_, _) => ToggleCrosshairVisibility();

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings…");
        settingsItem.Click += (_, _) => OpenSettings();

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            Shutdown();
        };

        menu.Items.Add(_toggleMenuItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "CeeThreeDeeCursors",
            Icon = LoadTrayIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => OpenSettings();
        UpdateToggleMenuText();
    }

    private void ToggleCrosshairVisibility()
    {
        if (_overlay == null) return;

        _overlay.Visibility = _overlay.Visibility == Visibility.Visible
            ? Visibility.Hidden
            : Visibility.Visible;

        UpdateToggleMenuText();
    }

    private void UpdateToggleMenuText()
    {
        if (_toggleMenuItem == null || _overlay == null) return;
        _toggleMenuItem.Text = _overlay.Visibility == Visibility.Visible
            ? "Hide Crosshair"
            : "Show Crosshair";
    }

    private bool _settingsOpen = false;

    private void OpenSettings()
    {
        if (_settingsOpen) return;
        _settingsOpen = true;

        string versionText = $"v{GetType().Assembly.GetName().Version?.ToString(3) ?? "1.0.0"} (Built For Louie K)";

        var win = new SettingsWindow(
            _settings,
            preview => { _overlay?.ApplySettings(preview); },
            ToggleCrosshairVisibility,
            IsCrosshairVisible,
            versionText);

        // Force to front — tray-spawned windows often appear behind game overlays
        win.Topmost = true;
        win.ContentRendered += (_, _) => { win.Topmost = false; win.Activate(); };

        bool? result = win.ShowDialog();
        _settingsOpen = false;

        if (win.Result != null)
        {
            _settings = win.Result;
            SettingsService.Save(_settings);
            _overlay?.ApplySettings(_settings);
        }
        else
        {
            // Revert live preview to saved settings
            _overlay?.ApplySettings(_settings);
        }
    }

    private bool IsCrosshairVisible() => _overlay?.Visibility == Visibility.Visible;

    private static System.Drawing.Icon LoadTrayIcon()
    {
        // Try to load file-based icon first
        try
        {
            string iconPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Resources", "crosshair.ico");
            if (System.IO.File.Exists(iconPath))
                return new System.Drawing.Icon(iconPath);
        }
        catch { }

        // Generate a crosshair icon programmatically (32×32, dark bg + green crosshair)
        return GenerateCrosshairIcon(32);
    }

    private static System.Drawing.Icon GenerateCrosshairIcon(int size)
    {
        var bmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(bmp);

        g.Clear(System.Drawing.Color.Transparent);

        // Dark rounded background
        using var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(210, 20, 20, 30));
        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

        // Green crosshair lines
        int cx = size / 2;
        int cy = size / 2;
        int arm = size / 2 - 4;
        int gap = 3;

        using var outlinePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(180, 0, 0, 0), 3f);
        outlinePen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        outlinePen.EndCap   = System.Drawing.Drawing2D.LineCap.Round;

        using var crossPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 0, 230, 80), 1.5f);
        crossPen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
        crossPen.EndCap   = System.Drawing.Drawing2D.LineCap.Round;

        // Draw outline first, then colored lines on top
        (int x1, int y1, int x2, int y2)[] arms =
        [
            (cx - gap - arm, cy, cx - gap, cy),
            (cx + gap,       cy, cx + gap + arm, cy),
            (cx, cy - gap - arm, cx, cy - gap),
            (cx, cy + gap,       cx, cy + gap + arm),
        ];

        foreach (var (x1, y1, x2, y2) in arms)
        {
            g.DrawLine(outlinePen, x1, y1, x2, y2);
        }
        foreach (var (x1, y1, x2, y2) in arms)
        {
            g.DrawLine(crossPen, x1, y1, x2, y2);
        }

        // Center dot
        int dr = 2;
        g.FillEllipse(outlinePen.Brush, cx - dr - 1, cy - dr - 1, (dr + 1) * 2, (dr + 1) * 2);
        g.FillEllipse(crossPen.Brush,   cx - dr,     cy - dr,     dr * 2,        dr * 2);

        // Convert Bitmap → Icon
        IntPtr hIcon = bmp.GetHicon();
        var icon = System.Drawing.Icon.FromHandle(hIcon);
        return icon;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}

