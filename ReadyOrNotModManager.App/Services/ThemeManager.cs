using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace ReadyOrNotModManager.App.Services;

public sealed record AppTheme(
    string Name,
    string DisplayName,
    MediaColor Shell,
    MediaColor ShellEnd,
    MediaColor Rail,
    MediaColor RailEnd,
    MediaColor Panel,
    MediaColor PanelEnd,
    MediaColor PanelAlt,
    MediaColor PanelAltEnd,
    MediaColor Line,
    MediaColor Text,
    MediaColor Muted,
    MediaColor Accent,
    MediaColor Blue,
    MediaColor Danger,
    MediaColor Success)
{
    public override string ToString()
    {
        return DisplayName;
    }
}

public static class ThemeManager
{
    public const string DefaultThemeName = "tactical";

    public static IReadOnlyList<AppTheme> Themes { get; } =
    [
        new(DefaultThemeName, "Tactical default", MediaColor.FromRgb(0x0E, 0x11, 0x13), MediaColor.FromRgb(0x16, 0x1D, 0x21), MediaColor.FromRgb(0x14, 0x19, 0x1D), MediaColor.FromRgb(0x1F, 0x28, 0x2E), MediaColor.FromRgb(0x1B, 0x22, 0x26), MediaColor.FromRgb(0x29, 0x34, 0x3A), MediaColor.FromRgb(0x22, 0x2B, 0x30), MediaColor.FromRgb(0x30, 0x3C, 0x42), MediaColor.FromRgb(0x33, 0x41, 0x49), MediaColor.FromRgb(0xE8, 0xEC, 0xEB), MediaColor.FromRgb(0xA3, 0xAF, 0xB0), MediaColor.FromRgb(0xC7, 0xA9, 0x57), MediaColor.FromRgb(0x6F, 0xA8, 0xC9), MediaColor.FromRgb(0xD7, 0x77, 0x66), MediaColor.FromRgb(0x7E, 0xB3, 0x86)),
        new("dark", "Dark mode", MediaColor.FromRgb(0x08, 0x0B, 0x0F), MediaColor.FromRgb(0x15, 0x19, 0x22), MediaColor.FromRgb(0x10, 0x14, 0x1B), MediaColor.FromRgb(0x1B, 0x22, 0x2D), MediaColor.FromRgb(0x18, 0x1E, 0x27), MediaColor.FromRgb(0x25, 0x2C, 0x37), MediaColor.FromRgb(0x20, 0x27, 0x31), MediaColor.FromRgb(0x2D, 0x35, 0x40), MediaColor.FromRgb(0x35, 0x43, 0x50), MediaColor.FromRgb(0xF1, 0xF5, 0xF4), MediaColor.FromRgb(0xAB, 0xB4, 0xB8), MediaColor.FromRgb(0x8F, 0xB6, 0xD9), MediaColor.FromRgb(0x6F, 0xA8, 0xC9), MediaColor.FromRgb(0xD7, 0x77, 0x66), MediaColor.FromRgb(0x7E, 0xB3, 0x86)),
        new("light", "Light mode", MediaColor.FromRgb(0xEA, 0xEE, 0xF0), MediaColor.FromRgb(0xD8, 0xE1, 0xE5), MediaColor.FromRgb(0xF7, 0xF9, 0xFA), MediaColor.FromRgb(0xE4, 0xEC, 0xF0), MediaColor.FromRgb(0xFF, 0xFF, 0xFF), MediaColor.FromRgb(0xEA, 0xF0, 0xF3), MediaColor.FromRgb(0xF1, 0xF5, 0xF6), MediaColor.FromRgb(0xDF, 0xE8, 0xEC), MediaColor.FromRgb(0xA9, 0xB8, 0xC0), MediaColor.FromRgb(0x18, 0x24, 0x2B), MediaColor.FromRgb(0x5B, 0x6B, 0x74), MediaColor.FromRgb(0x8B, 0x6B, 0x20), MediaColor.FromRgb(0x3E, 0x7F, 0xAA), MediaColor.FromRgb(0xB8, 0x43, 0x34), MediaColor.FromRgb(0x3F, 0x8B, 0x59)),
        new("claude", "Claude palette", MediaColor.FromRgb(0x17, 0x11, 0x0D), MediaColor.FromRgb(0x2B, 0x20, 0x18), MediaColor.FromRgb(0x20, 0x18, 0x13), MediaColor.FromRgb(0x3A, 0x2B, 0x20), MediaColor.FromRgb(0x2A, 0x22, 0x1B), MediaColor.FromRgb(0x44, 0x36, 0x2B), MediaColor.FromRgb(0x35, 0x2B, 0x22), MediaColor.FromRgb(0x50, 0x40, 0x32), MediaColor.FromRgb(0x54, 0x45, 0x37), MediaColor.FromRgb(0xF2, 0xE8, 0xDC), MediaColor.FromRgb(0xC4, 0xB5, 0xA6), MediaColor.FromRgb(0xD9, 0x77, 0x57), MediaColor.FromRgb(0x87, 0xA9, 0xB5), MediaColor.FromRgb(0xD7, 0x77, 0x66), MediaColor.FromRgb(0x7E, 0xB3, 0x86)),
        new("codex", "Codex palette", MediaColor.FromRgb(0x0B, 0x0F, 0x18), MediaColor.FromRgb(0x16, 0x24, 0x32), MediaColor.FromRgb(0x11, 0x18, 0x24), MediaColor.FromRgb(0x1B, 0x2D, 0x3C), MediaColor.FromRgb(0x18, 0x22, 0x2F), MediaColor.FromRgb(0x20, 0x35, 0x45), MediaColor.FromRgb(0x1F, 0x2B, 0x38), MediaColor.FromRgb(0x2B, 0x43, 0x54), MediaColor.FromRgb(0x38, 0x50, 0x60), MediaColor.FromRgb(0xEC, 0xF3, 0xF7), MediaColor.FromRgb(0xA8, 0xB8, 0xC2), MediaColor.FromRgb(0x74, 0xB9, 0xFF), MediaColor.FromRgb(0x70, 0xD6, 0xFF), MediaColor.FromRgb(0xE0, 0x6C, 0x75), MediaColor.FromRgb(0x8C, 0xD8, 0x9B)),
        new("purple", "Purple gradient", MediaColor.FromRgb(0x10, 0x0A, 0x1E), MediaColor.FromRgb(0x2B, 0x16, 0x4A), MediaColor.FromRgb(0x16, 0x0F, 0x2A), MediaColor.FromRgb(0x35, 0x1D, 0x5A), MediaColor.FromRgb(0x20, 0x16, 0x35), MediaColor.FromRgb(0x45, 0x28, 0x72), MediaColor.FromRgb(0x2B, 0x1E, 0x46), MediaColor.FromRgb(0x59, 0x35, 0x8C), MediaColor.FromRgb(0x6C, 0x56, 0x8F), MediaColor.FromRgb(0xF4, 0xED, 0xFF), MediaColor.FromRgb(0xC6, 0xB7, 0xDA), MediaColor.FromRgb(0xB9, 0x7A, 0xFF), MediaColor.FromRgb(0x80, 0xB7, 0xFF), MediaColor.FromRgb(0xE0, 0x6C, 0x8B), MediaColor.FromRgb(0x88, 0xD6, 0x9B)),
        new("hacker", "Hacker", MediaColor.FromRgb(0x03, 0x08, 0x06), MediaColor.FromRgb(0x05, 0x17, 0x10), MediaColor.FromRgb(0x05, 0x10, 0x0C), MediaColor.FromRgb(0x09, 0x22, 0x17), MediaColor.FromRgb(0x08, 0x18, 0x12), MediaColor.FromRgb(0x0F, 0x2E, 0x20), MediaColor.FromRgb(0x0C, 0x22, 0x19), MediaColor.FromRgb(0x13, 0x3B, 0x28), MediaColor.FromRgb(0x17, 0x44, 0x2F), MediaColor.FromRgb(0xD8, 0xFF, 0xE1), MediaColor.FromRgb(0x83, 0xB8, 0x90), MediaColor.FromRgb(0x3C, 0xFF, 0x7B), MediaColor.FromRgb(0x55, 0xBB, 0xFF), MediaColor.FromRgb(0xFF, 0x5F, 0x5F), MediaColor.FromRgb(0x68, 0xD9, 0x80))
    ];

    public static string ResolveThemeName(string? name)
    {
        return Themes.Any(theme => theme.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ? name!.ToLowerInvariant()
            : DefaultThemeName;
    }

    public static AppTheme GetTheme(string? name)
    {
        var resolved = ResolveThemeName(name);
        return Themes.First(theme => theme.Name == resolved);
    }

    public static void ApplyTheme(ResourceDictionary resources, string? name)
    {
        var theme = GetTheme(name);
        SetGradientBrush(resources, "ShellBrush", theme.Shell, theme.ShellEnd);
        SetGradientBrush(resources, "RailBrush", theme.Rail, theme.RailEnd);
        SetGradientBrush(resources, "PanelBrush", theme.Panel, theme.PanelEnd);
        SetGradientBrush(resources, "PanelAltBrush", theme.PanelAlt, theme.PanelAltEnd);
        SetBrush(resources, "LineBrush", theme.Line);
        SetBrush(resources, "TextBrush", theme.Text);
        SetBrush(resources, "MutedBrush", theme.Muted);
        SetBrush(resources, "AccentBrush", theme.Accent);
        SetBrush(resources, "BlueBrush", theme.Blue);
        SetBrush(resources, "DangerBrush", theme.Danger);
        SetBrush(resources, "SuccessBrush", theme.Success);
    }

    private static void SetBrush(ResourceDictionary resources, string key, MediaColor color)
    {
        resources[key] = new SolidColorBrush(color);
    }

    private static void SetGradientBrush(ResourceDictionary resources, string key, MediaColor start, MediaColor end)
    {
        resources[key] = new LinearGradientBrush(start, end, new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
    }
}
