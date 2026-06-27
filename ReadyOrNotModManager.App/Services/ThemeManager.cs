using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace ReadyOrNotModManager.App.Services;

public sealed record AppTheme(
    string Name,
    string DisplayName,
    MediaColor Shell,
    MediaColor Rail,
    MediaColor Panel,
    MediaColor PanelAlt,
    MediaColor Line,
    MediaColor Text,
    MediaColor Muted,
    MediaColor Accent,
    MediaColor Blue,
    MediaColor Danger,
    MediaColor Success);

public static class ThemeManager
{
    public const string DefaultThemeName = "tactical";

    public static IReadOnlyList<AppTheme> Themes { get; } =
    [
        new(DefaultThemeName, "Tactical default", MediaColor.FromRgb(0x0E, 0x11, 0x13), MediaColor.FromRgb(0x14, 0x19, 0x1D), MediaColor.FromRgb(0x1B, 0x22, 0x26), MediaColor.FromRgb(0x22, 0x2B, 0x30), MediaColor.FromRgb(0x33, 0x41, 0x49), MediaColor.FromRgb(0xE8, 0xEC, 0xEB), MediaColor.FromRgb(0xA3, 0xAF, 0xB0), MediaColor.FromRgb(0xC7, 0xA9, 0x57), MediaColor.FromRgb(0x6F, 0xA8, 0xC9), MediaColor.FromRgb(0xD7, 0x77, 0x66), MediaColor.FromRgb(0x7E, 0xB3, 0x86)),
        new("claude", "Claude", MediaColor.FromRgb(0x16, 0x12, 0x0F), MediaColor.FromRgb(0x20, 0x1A, 0x15), MediaColor.FromRgb(0x2A, 0x22, 0x1B), MediaColor.FromRgb(0x35, 0x2B, 0x22), MediaColor.FromRgb(0x54, 0x45, 0x37), MediaColor.FromRgb(0xF2, 0xE8, 0xDC), MediaColor.FromRgb(0xC4, 0xB5, 0xA6), MediaColor.FromRgb(0xD9, 0x77, 0x57), MediaColor.FromRgb(0x87, 0xA9, 0xB5), MediaColor.FromRgb(0xD7, 0x77, 0x66), MediaColor.FromRgb(0x7E, 0xB3, 0x86)),
        new("chatgpt", "ChatGPT", MediaColor.FromRgb(0x0D, 0x17, 0x14), MediaColor.FromRgb(0x12, 0x20, 0x1C), MediaColor.FromRgb(0x1A, 0x2C, 0x27), MediaColor.FromRgb(0x21, 0x38, 0x32), MediaColor.FromRgb(0x32, 0x56, 0x4D), MediaColor.FromRgb(0xE7, 0xF3, 0xEF), MediaColor.FromRgb(0xA8, 0xC2, 0xBA), MediaColor.FromRgb(0x10, 0xA3, 0x7F), MediaColor.FromRgb(0x6F, 0xA8, 0xC9), MediaColor.FromRgb(0xD7, 0x77, 0x66), MediaColor.FromRgb(0x7E, 0xB3, 0x86)),
        new("hacker", "Hacker", MediaColor.FromRgb(0x03, 0x08, 0x06), MediaColor.FromRgb(0x05, 0x10, 0x0C), MediaColor.FromRgb(0x08, 0x18, 0x12), MediaColor.FromRgb(0x0C, 0x22, 0x19), MediaColor.FromRgb(0x17, 0x44, 0x2F), MediaColor.FromRgb(0xD8, 0xFF, 0xE1), MediaColor.FromRgb(0x83, 0xB8, 0x90), MediaColor.FromRgb(0x3C, 0xFF, 0x7B), MediaColor.FromRgb(0x55, 0xBB, 0xFF), MediaColor.FromRgb(0xFF, 0x5F, 0x5F), MediaColor.FromRgb(0x68, 0xD9, 0x80))
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
        SetBrush(resources, "ShellBrush", theme.Shell);
        SetBrush(resources, "RailBrush", theme.Rail);
        SetBrush(resources, "PanelBrush", theme.Panel);
        SetBrush(resources, "PanelAltBrush", theme.PanelAlt);
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
}
