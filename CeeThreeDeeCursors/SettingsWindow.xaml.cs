using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using CeeThreeDeeCursors.Models;

namespace CeeThreeDeeCursors;

public partial class SettingsWindow : Window
{
    private CrosshairSettings _current;
    private readonly Action<CrosshairSettings> _onPreview;
    private readonly Action _toggleCrosshair;
    private readonly Func<bool> _isCrosshairVisible;
    private bool _loading = true;
    private Color _selectedColor;
    private CrosshairStyle _selectedStyle;
    private int _selectedMonitorIndex;
    private readonly List<Button> _styleButtons = new();
    private readonly List<Button> _monitorButtons = new();

    public CrosshairSettings? Result { get; private set; }

    public SettingsWindow(
        CrosshairSettings settings,
        Action<CrosshairSettings> onPreview,
        Action toggleCrosshair,
        Func<bool> isCrosshairVisible,
        string appVersionText)
    {
        _current = Clone(settings);
        _onPreview = onPreview;
        _toggleCrosshair = toggleCrosshair;
        _isCrosshairVisible = isCrosshairVisible;
        InitializeComponent();
        VersionText.Text = appVersionText;
        PopulateControls();
        UpdateToggleButtonText();
        _loading = false;
    }

    private void PopulateControls()
    {
        _selectedStyle = _current.Style;
        BuildStyleGrid();

        var screens = System.Windows.Forms.Screen.AllScreens;
        _selectedMonitorIndex = Math.Clamp(_current.MonitorIndex, 0, screens.Length - 1);
        BuildMonitorGrid();

        _selectedColor = ParseWpfColor(_current.Color);
        SizeSlider.Value = _current.Size;
        GapSlider.Value = _current.Gap;
        ThicknessSlider.Value = _current.Thickness;
        DotSlider.Value = _current.DotRadius;
        OpacitySlider.Value = _current.Opacity;
        OutlineCheck.IsChecked = _current.OutlineEnabled;
        OffsetXSlider.Value = _current.OffsetX;
        OffsetYSlider.Value = _current.OffsetY;
        HotkeyBox.Text = _current.ToggleHotkey;
        SettingsHotkeyBox.Text = _current.SettingsHotkey;

        UpdateLabels();
        UpdateColorPreview();
    }

    private void BuildStyleGrid()
    {
        StyleGridPanel.Children.Clear();
        _styleButtons.Clear();

        foreach (CrosshairStyle style in Enum.GetValues<CrosshairStyle>())
        {
            var button = new Button
            {
                Tag = style,
                Margin = new Thickness(4),
                Padding = new Thickness(6),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#585B70")),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(CreateCrosshairPreview(style));
            stack.Children.Add(new TextBlock
            {
                Text = style switch
                {
                    CrosshairStyle.CrossWithDot => "Cross+Dot",
                    CrosshairStyle.TStyle => "T-Style",
                    _ => style.ToString()
                },
                FontSize = 10,
                Foreground = Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            button.Content = stack;
            button.Click += OnStyleCardClicked;

            _styleButtons.Add(button);
            StyleGridPanel.Children.Add(button);
        }

        UpdateStyleSelectionVisuals();
    }

    private FrameworkElement CreateCrosshairPreview(CrosshairStyle style)
    {
        var canvas = new Canvas { Width = 46, Height = 34 };
        double cx = 23;
        double cy = 17;

        Line MakeLine(double x1, double y1, double x2, double y2, double t, Brush b) => new()
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            StrokeThickness = t,
            Stroke = b,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        void AddArm(double x1, double y1, double x2, double y2)
        {
            canvas.Children.Add(MakeLine(x1, y1, x2, y2, 3.2, Brushes.Black));
            canvas.Children.Add(MakeLine(x1, y1, x2, y2, 1.7, Brushes.Lime));
        }

        if (style is CrosshairStyle.Cross or CrosshairStyle.CrossWithDot or CrosshairStyle.TStyle)
        {
            AddArm(cx - 11, cy, cx - 4, cy);
            AddArm(cx + 4, cy, cx + 11, cy);
            if (style != CrosshairStyle.TStyle)
                AddArm(cx, cy - 11, cx, cy - 4);
            AddArm(cx, cy + 4, cx, cy + 11);
        }

        if (style is CrosshairStyle.Dot or CrosshairStyle.CrossWithDot)
        {
            var o = new Ellipse { Width = 8, Height = 8, Fill = Brushes.Black };
            Canvas.SetLeft(o, cx - 4);
            Canvas.SetTop(o, cy - 4);
            canvas.Children.Add(o);

            var d = new Ellipse { Width = 5, Height = 5, Fill = Brushes.Lime };
            Canvas.SetLeft(d, cx - 2.5);
            Canvas.SetTop(d, cy - 2.5);
            canvas.Children.Add(d);
        }

        if (style == CrosshairStyle.Circle)
        {
            canvas.Children.Add(new Ellipse
            {
                Width = 16,
                Height = 16,
                Stroke = Brushes.Lime,
                StrokeThickness = 1.8,
                Fill = Brushes.Transparent
            });
            Canvas.SetLeft(canvas.Children[^1], cx - 8);
            Canvas.SetTop(canvas.Children[^1], cy - 8);
        }

        return canvas;
    }

    private void OnStyleCardClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not CrosshairStyle style) return;
        _selectedStyle = style;
        UpdateStyleSelectionVisuals();
        if (!_loading)
            _onPreview(BuildFromControls());
    }

    private void UpdateStyleSelectionVisuals()
    {
        foreach (Button b in _styleButtons)
        {
            bool selected = b.Tag is CrosshairStyle s && s == _selectedStyle;
            b.BorderBrush = selected
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#89B4FA"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#585B70"));
            b.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            b.Background = selected
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#45475A"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
        }
    }

    private void BuildMonitorGrid()
    {
        MonitorGridPanel.Children.Clear();
        _monitorButtons.Clear();

        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var button = new Button
            {
                Tag = i,
                Margin = new Thickness(4),
                Padding = new Thickness(4),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#585B70")),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            var monitor = new Border
            {
                Width = 34,
                Height = 24,
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1.4),
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E")),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = (i + 1).ToString(),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold
                }
            };

            stack.Children.Add(monitor);
            stack.Children.Add(new TextBlock
            {
                Text = screen.Primary ? "Primary" : "Monitor",
                FontSize = 10,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            button.Content = stack;
            button.Click += OnMonitorCardClicked;

            _monitorButtons.Add(button);
            MonitorGridPanel.Children.Add(button);
        }

        UpdateMonitorSelectionVisuals();
    }

    private void OnMonitorCardClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not int idx) return;
        _selectedMonitorIndex = idx;
        UpdateMonitorSelectionVisuals();
        if (!_loading)
            _onPreview(BuildFromControls());
    }

    private void UpdateMonitorSelectionVisuals()
    {
        foreach (Button b in _monitorButtons)
        {
            bool selected = b.Tag is int idx && idx == _selectedMonitorIndex;
            b.BorderBrush = selected
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#89B4FA"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#585B70"));
            b.BorderThickness = selected ? new Thickness(2) : new Thickness(1);
            b.Background = selected
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#45475A"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
        }
    }

    private void UpdateLabels()
    {
        SizeLabel.Text = ((int)SizeSlider.Value).ToString();
        GapLabel.Text = ((int)GapSlider.Value).ToString();
        ThicknessLabel.Text = ThicknessSlider.Value.ToString("F1");
        DotLabel.Text = ((int)DotSlider.Value).ToString();
        OpacityLabel.Text = OpacitySlider.Value.ToString("F2");
        OffsetXLabel.Text = ((int)OffsetXSlider.Value).ToString();
        OffsetYLabel.Text = ((int)OffsetYSlider.Value).ToString();
    }


    private void UpdateColorPreview()
    {
        ColorPreviewBox.Background = new SolidColorBrush(_selectedColor);
        ColorHexBox.Text = _selectedColor.ToString();
    }

    private void OnPickColor(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(_selectedColor.A, _selectedColor.R, _selectedColor.G, _selectedColor.B)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _selectedColor = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
            UpdateColorPreview();

            if (!_loading)
            {
                _onPreview(BuildFromControls());
            }
        }
    }


    private CrosshairSettings BuildFromControls() => new()
    {
        Style = _selectedStyle,
        Color = _selectedColor.ToString(),
        Size = (int)SizeSlider.Value,
        Gap = (int)GapSlider.Value,
        Thickness = ThicknessSlider.Value,
        DotRadius = (int)DotSlider.Value,
        Opacity = OpacitySlider.Value,
        OutlineEnabled = OutlineCheck.IsChecked == true,
        MonitorIndex = _selectedMonitorIndex,
        OffsetX = (int)OffsetXSlider.Value,
        OffsetY = (int)OffsetYSlider.Value,
        ToggleHotkey = HotkeyBox.Text.Trim(),
        SettingsHotkey = SettingsHotkeyBox.Text.Trim()
    };

    private void OnPreviewChanged(object sender, System.EventArgs e)
    {
        if (_loading) return;
        UpdateLabels();
        _onPreview(BuildFromControls());
    }

    private void OnToggleCrosshairClicked(object sender, RoutedEventArgs e)
    {
        _toggleCrosshair();
        UpdateToggleButtonText();
    }

    private void UpdateToggleButtonText()
    {
        ToggleCrosshairButton.Content = _isCrosshairVisible() ? "Hide Crosshair" : "Show Crosshair";
    }

    private void OnCloseWindowClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTopBarMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            DragMove();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!TryNormalizeHotkey(HotkeyBox.Text, out string normalizedToggle))
        {
            System.Windows.MessageBox.Show(this, "Toggle hotkey is invalid. Use formats like F9, Shift+F9, or Ctrl+Alt+H.", "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Cancel = true;
            return;
        }

        if (!TryNormalizeHotkey(SettingsHotkeyBox.Text, out string normalizedSettings))
        {
            System.Windows.MessageBox.Show(this, "Settings hotkey is invalid. Use formats like F10, Shift+F10, or Ctrl+Alt+S.", "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Cancel = true;
            return;
        }

        if (string.Equals(normalizedToggle, normalizedSettings, StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(this, "Toggle and Settings hotkeys must be different.", "Hotkey Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Cancel = true;
            return;
        }

        HotkeyBox.Text = normalizedToggle;
        SettingsHotkeyBox.Text = normalizedSettings;

        // Treat close as Save.
        Result = BuildFromControls();
    }

    private void OnHotkeyBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox box) return;

        // Ignore modifier-only presses.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
            return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        var parts = new List<string>();
        if (ctrl) parts.Add("Ctrl");
        if (alt) parts.Add("Alt");
        if (shift) parts.Add("Shift");
        parts.Add(key.ToString());

        box.Text = string.Join("+", parts);
        box.CaretIndex = box.Text.Length;

        e.Handled = true;

        if (!_loading)
        {
            _onPreview(BuildFromControls());
        }
    }

    private static bool TryNormalizeHotkey(string input, out string normalized)
    {
        normalized = string.Empty;

        var parts = input.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        uint mods = 0;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    mods |= 0b001;
                    break;
                case "alt":
                    mods |= 0b010;
                    break;
                case "shift":
                    mods |= 0b100;
                    break;
                default:
                    return false;
            }
        }

        if (!Enum.TryParse<Key>(parts[^1], ignoreCase: true, out Key key) || key == Key.None)
            return false;

        var output = new List<string>();
        if ((mods & 0b001) != 0) output.Add("Ctrl");
        if ((mods & 0b010) != 0) output.Add("Alt");
        if ((mods & 0b100) != 0) output.Add("Shift");
        output.Add(key.ToString());

        normalized = string.Join("+", output);
        return true;
    }

    private static Color ParseWpfColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.Lime; }
    }

    private static CrosshairSettings Clone(CrosshairSettings s) => new()
    {
        Style = s.Style,
        Color = s.Color,
        Size = s.Size,
        Gap = s.Gap,
        Thickness = s.Thickness,
        DotRadius = s.DotRadius,
        Opacity = s.Opacity,
        OutlineEnabled = s.OutlineEnabled,
        MonitorIndex = s.MonitorIndex,
        OffsetX = s.OffsetX,
        OffsetY = s.OffsetY,
        ToggleHotkey = s.ToggleHotkey,
        SettingsHotkey = s.SettingsHotkey
    };
}
