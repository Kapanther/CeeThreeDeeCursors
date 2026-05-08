using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using CeeThreeDeeCursors.Models;

namespace CeeThreeDeeCursors;

/// <summary>
/// Full-screen transparent overlay window that draws the crosshair.
/// Click-through is achieved via Win32 WS_EX_TRANSPARENT | WS_EX_LAYERED.
/// </summary>
public partial class OverlayWindow : Window
{
    // Win32 constants
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    // SetWindowPos — used for heartbeat HWND_TOPMOST reassertion
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE     = 0x0002;
    private const uint SWP_NOSIZE     = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    private DispatcherTimer? _topmostTimer;

    // Hotkey registration
    private const int HOTKEY_TOGGLE_ID   = 9001;
    private const int HOTKEY_SETTINGS_ID = 9002;
    private const uint MOD_SHIFT   = 0x0004;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_ALT     = 0x0001;
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Called when the Settings global hotkey fires.</summary>
    public Action? OnSettingsHotkeyPressed { get; set; }

    private HwndSource? _hwndSource;
    private CrosshairSettings _settings;

    public OverlayWindow(CrosshairSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    public void ApplySettings(CrosshairSettings settings)
    {
        _settings = settings;
        PositionOnMonitor();
        DrawCrosshair();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        // Make window click-through, layered, invisible to taskbar / alt-tab
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        // Hook WndProc for hotkey messages
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);

        RegisterHotkeys(hwnd);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionOnMonitor();
        // Defer draw until after the layout pass so ActualWidth/Height are valid
        Dispatcher.InvokeAsync(DrawCrosshair, DispatcherPriority.Render);

        // Heartbeat: re-assert HWND_TOPMOST every 250 ms so games can't push us under
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _topmostTimer.Tick += (_, _) => ForceTopmost();
        _topmostTimer.Start();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(DrawCrosshair, DispatcherPriority.Render);
    }

    /// <summary>
    /// Re-asserts HWND_TOPMOST via Win32 SetWindowPos. This survives exclusive-fullscreen
    /// games that push WPF Topmost windows down the Z-order.
    /// </summary>
    private void ForceTopmost()
    {
        if (Visibility != Visibility.Visible) return;
        var helper = new WindowInteropHelper(this);
        SetWindowPos(helper.Handle, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // ── Positioning ──────────────────────────────────────────────────────────

    private void PositionOnMonitor()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        int idx = Math.Clamp(_settings.MonitorIndex, 0, screens.Length - 1);
        var screen = screens[idx];
        var wa = screen.Bounds;

        // Convert from physical pixels to WPF device-independent units
        var source = PresentationSource.FromVisual(this);
        double dpiX = 1.0, dpiY = 1.0;
        if (source?.CompositionTarget != null)
        {
            dpiX = source.CompositionTarget.TransformToDevice.M11;
            dpiY = source.CompositionTarget.TransformToDevice.M22;
        }

        Left = wa.Left / dpiX;
        Top = wa.Top / dpiY;
        Width = wa.Width / dpiX;
        Height = wa.Height / dpiY;
    }

    // ── Crosshair drawing ────────────────────────────────────────────────────

    private void DrawCrosshair()
    {
        CrosshairCanvas.Children.Clear();

        var color = ParseColor(_settings.Color);
        var brush = new SolidColorBrush(color) { Opacity = _settings.Opacity };
        var outlineBrush = new SolidColorBrush(Colors.Black) { Opacity = _settings.Opacity };

        double cx = CrosshairCanvas.ActualWidth / 2 + _settings.OffsetX;
        double cy = CrosshairCanvas.ActualHeight / 2 + _settings.OffsetY;

        switch (_settings.Style)
        {
            case CrosshairStyle.Cross:
                DrawCrossLines(cx, cy, brush, outlineBrush);
                break;
            case CrosshairStyle.Dot:
                DrawDot(cx, cy, brush, outlineBrush);
                break;
            case CrosshairStyle.Circle:
                DrawCircle(cx, cy, brush);
                break;
            case CrosshairStyle.CrossWithDot:
                DrawCrossLines(cx, cy, brush, outlineBrush);
                DrawDot(cx, cy, brush, outlineBrush);
                break;
            case CrosshairStyle.TStyle:
                DrawTStyle(cx, cy, brush, outlineBrush);
                break;
        }
    }

    private void DrawCrossLines(double cx, double cy, Brush brush, Brush outline)
    {
        int gap = _settings.Gap;
        int size = _settings.Size;
        double thick = _settings.Thickness;

        // Horizontal: left arm, right arm
        // Vertical:   top arm, bottom arm
        var arms = new (double x1, double y1, double x2, double y2)[]
        {
            (cx - gap - size, cy, cx - gap, cy),  // left
            (cx + gap,        cy, cx + gap + size, cy),  // right
            (cx, cy - gap - size, cx, cy - gap),  // top
            (cx, cy + gap,        cx, cy + gap + size),  // bottom
        };

        foreach (var (x1, y1, x2, y2) in arms)
        {
            if (_settings.OutlineEnabled)
                CrosshairCanvas.Children.Add(MakeLine(x1, y1, x2, y2, outline, thick + 2));
            CrosshairCanvas.Children.Add(MakeLine(x1, y1, x2, y2, brush, thick));
        }
    }

    private void DrawTStyle(double cx, double cy, Brush brush, Brush outline)
    {
        int gap = _settings.Gap;
        int size = _settings.Size;
        double thick = _settings.Thickness;

        // Same as Cross but no top arm
        var arms = new (double x1, double y1, double x2, double y2)[]
        {
            (cx - gap - size, cy, cx - gap, cy),
            (cx + gap,        cy, cx + gap + size, cy),
            (cx, cy + gap,        cx, cy + gap + size),
        };

        foreach (var (x1, y1, x2, y2) in arms)
        {
            if (_settings.OutlineEnabled)
                CrosshairCanvas.Children.Add(MakeLine(x1, y1, x2, y2, outline, thick + 2));
            CrosshairCanvas.Children.Add(MakeLine(x1, y1, x2, y2, brush, thick));
        }
    }

    private void DrawDot(double cx, double cy, Brush brush, Brush outline)
    {
        int r = _settings.DotRadius;
        if (_settings.OutlineEnabled)
            CrosshairCanvas.Children.Add(MakeEllipse(cx, cy, r + 1, outline));
        CrosshairCanvas.Children.Add(MakeEllipse(cx, cy, r, brush));
    }

    private void DrawCircle(double cx, double cy, Brush brush)
    {
        int r = _settings.Size;
        double thick = _settings.Thickness;
        var el = new Ellipse
        {
            Width = r * 2,
            Height = r * 2,
            Stroke = brush,
            StrokeThickness = thick,
            Fill = Brushes.Transparent
        };
        System.Windows.Controls.Canvas.SetLeft(el, cx - r);
        System.Windows.Controls.Canvas.SetTop(el, cy - r);
        CrosshairCanvas.Children.Add(el);
    }

    private static Line MakeLine(double x1, double y1, double x2, double y2, Brush brush, double thick)
        => new() { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = thick, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };

    private static Ellipse MakeEllipse(double cx, double cy, int r, Brush brush)
    {
        var el = new Ellipse { Width = r * 2, Height = r * 2, Fill = brush };
        System.Windows.Controls.Canvas.SetLeft(el, cx - r);
        System.Windows.Controls.Canvas.SetTop(el, cy - r);
        return el;
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.Lime; }
    }

    // ── Hotkey ───────────────────────────────────────────────────────────────

    private void RegisterHotkeys(IntPtr hwnd)
    {
        var (mods1, vk1) = ParseHotkey(_settings.ToggleHotkey);
        if (vk1 != 0) RegisterHotKey(hwnd, HOTKEY_TOGGLE_ID, mods1, vk1);

        var (mods2, vk2) = ParseHotkey(_settings.SettingsHotkey);
        if (vk2 != 0) RegisterHotKey(hwnd, HOTKEY_SETTINGS_ID, mods2, vk2);
    }

    private static (uint mods, uint vk) ParseHotkey(string hotkeyStr)
    {
        uint mods = 0;
        var parts = hotkeyStr.Split('+');
        foreach (var part in parts.Take(parts.Length - 1))
        {
            switch (part.Trim().ToLowerInvariant())
            {
                case "shift":   mods |= MOD_SHIFT;   break;
                case "ctrl":
                case "control": mods |= MOD_CONTROL; break;
                case "alt":     mods |= MOD_ALT;     break;
            }
        }
        string keyName = parts.Last().Trim();
        if (Enum.TryParse<Key>(keyName, out var key))
            return (mods, (uint)KeyInterop.VirtualKeyFromKey(key));
        return (0, 0);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == HOTKEY_TOGGLE_ID)
            {
                Visibility = Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
                handled = true;
            }
            else if (id == HOTKEY_SETTINGS_ID)
            {
                OnSettingsHotkeyPressed?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    protected override void OnClosed(EventArgs e)
    {
        _topmostTimer?.Stop();
        var helper = new WindowInteropHelper(this);
        UnregisterHotKey(helper.Handle, HOTKEY_TOGGLE_ID);
        UnregisterHotKey(helper.Handle, HOTKEY_SETTINGS_ID);
        _hwndSource?.RemoveHook(WndProc);
        base.OnClosed(e);
    }
}
