using System.Text.Json.Serialization;

namespace CeeThreeDeeCursors.Models;

public enum CrosshairStyle
{
    Cross,
    Dot,
    Circle,
    CrossWithDot,
    TStyle
}

public class CrosshairSettings
{
    public CrosshairStyle Style { get; set; } = CrosshairStyle.CrossWithDot;

    // Color as ARGB hex string e.g. "#FF00FF00"
    public string Color { get; set; } = "#FF00FF00";

    // Half-length of each arm in pixels
    public int Size { get; set; } = 10;

    // Gap between center and start of arm in pixels
    public int Gap { get; set; } = 3;

    // Line thickness in pixels
    public double Thickness { get; set; } = 2.0;

    // Dot radius for center dot / circle style
    public int DotRadius { get; set; } = 2;

    // Outline (dark border around lines for visibility)
    public bool OutlineEnabled { get; set; } = true;

    // Opacity 0.0 – 1.0
    public double Opacity { get; set; } = 1.0;

    // Monitor index (0 = primary)
    public int MonitorIndex { get; set; } = 0;

    // Custom offset from center (pixels)
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    // Hotkeys — format: "Modifier+Key" (e.g. "Shift+F9") or just "F5"
    public string ToggleHotkey { get; set; } = "Shift+F9";

    // Hotkey to open/focus the Settings window
    public string SettingsHotkey { get; set; } = "Shift+F10";
}
