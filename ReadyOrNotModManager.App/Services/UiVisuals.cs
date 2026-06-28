using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MahApps.Metro.IconPacks;

namespace ReadyOrNotModManager.App.Services;

public enum VisualTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger
}

public enum DashboardStatusKind
{
    Game,
    Nexus,
    Update
}

public sealed record QueueStatusVisual(string Label, VisualTone Tone, PackIconMaterialKind Icon)
{
    public static QueueStatusVisual FromStatus(string? status)
    {
        var value = status?.Trim() ?? string.Empty;
        if (value.Contains("fail", StringComparison.OrdinalIgnoreCase) || value.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return new QueueStatusVisual("Error", VisualTone.Danger, PackIconMaterialKind.AlertCircleOutline);
        }

        if (value.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return new QueueStatusVisual("Missing", VisualTone.Warning, PackIconMaterialKind.AlertOutline);
        }

        if (value.Equals("Deployed", StringComparison.OrdinalIgnoreCase))
        {
            return new QueueStatusVisual("Deployed", VisualTone.Success, PackIconMaterialKind.CheckCircleOutline);
        }

        if (value.Equals("Downloaded", StringComparison.OrdinalIgnoreCase) || value.Equals("Imported archive", StringComparison.OrdinalIgnoreCase))
        {
            return new QueueStatusVisual(value, VisualTone.Info, PackIconMaterialKind.ArchiveCheckOutline);
        }

        if (value.Equals("Queued", StringComparison.OrdinalIgnoreCase) || value.Contains("requesting", StringComparison.OrdinalIgnoreCase) || value.Contains("open nexus", StringComparison.OrdinalIgnoreCase))
        {
            return new QueueStatusVisual(value.Length == 0 ? "Queued" : value, VisualTone.Warning, PackIconMaterialKind.ClockOutline);
        }

        return new QueueStatusVisual(value.Length == 0 ? "Unknown" : value, VisualTone.Neutral, PackIconMaterialKind.CircleOutline);
    }
}

public sealed record RecentActivityVisual(DateTimeOffset TimestampUtc, string TimeText, string Text, VisualTone Tone, PackIconMaterialKind Icon)
{
    public static RecentActivityVisual FromActivity(RecentActivityItem item)
    {
        var text = item.Text;
        var lower = text.ToLowerInvariant();
        var tone = VisualTone.Neutral;
        var icon = PackIconMaterialKind.CircleOutline;

        if (lower.Contains("failed") || lower.Contains("error"))
        {
            tone = VisualTone.Danger;
            icon = PackIconMaterialKind.AlertCircleOutline;
        }
        else if (lower.Contains("deploy"))
        {
            tone = VisualTone.Success;
            icon = PackIconMaterialKind.CheckCircleOutline;
        }
        else if (lower.Contains("download") || lower.Contains("import") || lower.Contains("added"))
        {
            tone = VisualTone.Info;
            icon = PackIconMaterialKind.DownloadCircleOutline;
        }
        else if (lower.Contains("profile") || lower.Contains("modpack") || lower.Contains("uninstall") || lower.Contains("delete"))
        {
            tone = VisualTone.Warning;
            icon = PackIconMaterialKind.FolderMultipleOutline;
        }

        return new RecentActivityVisual(item.TimestampUtc, item.TimestampUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture), text, tone, icon);
    }
}

public sealed record DashboardStatusVisual(DashboardStatusKind Kind, string Status, string HelperText, VisualTone Tone, PackIconMaterialKind Icon)
{
    public static DashboardStatusVisual FromStatus(DashboardStatusKind kind, string status)
    {
        var normalized = status.Trim();
        return kind switch
        {
            DashboardStatusKind.Game when normalized.Equals("Detected", StringComparison.OrdinalIgnoreCase) =>
                new DashboardStatusVisual(kind, status, "ReadyOrNot\\Content\\Paks located", VisualTone.Success, PackIconMaterialKind.GamepadVariantOutline),
            DashboardStatusKind.Game =>
                new DashboardStatusVisual(kind, status, "Choose or auto-detect the game folder", VisualTone.Danger, PackIconMaterialKind.GamepadVariantOutline),
            DashboardStatusKind.Nexus when normalized.StartsWith("Connected", StringComparison.OrdinalIgnoreCase) =>
                new DashboardStatusVisual(kind, status, "API key validated for this account", VisualTone.Success, PackIconMaterialKind.AccountCheckOutline),
            DashboardStatusKind.Nexus when normalized.Equals("Not tested", StringComparison.OrdinalIgnoreCase) =>
                new DashboardStatusVisual(kind, status, "Run a connection test from Settings", VisualTone.Warning, PackIconMaterialKind.AccountQuestionOutline),
            DashboardStatusKind.Nexus =>
                new DashboardStatusVisual(kind, status, "Nexus API access needs attention", VisualTone.Danger, PackIconMaterialKind.AccountCancelOutline),
            DashboardStatusKind.Update when normalized.Contains("up to date", StringComparison.OrdinalIgnoreCase) =>
                new DashboardStatusVisual(kind, status, "Latest GitHub release matches this build", VisualTone.Success, PackIconMaterialKind.Update),
            DashboardStatusKind.Update when normalized.Contains("available", StringComparison.OrdinalIgnoreCase) =>
                new DashboardStatusVisual(kind, status, "A newer GitHub release is available", VisualTone.Warning, PackIconMaterialKind.Update),
            DashboardStatusKind.Update when normalized.Contains("unable to check", StringComparison.OrdinalIgnoreCase) =>
                new DashboardStatusVisual(kind, status, "Click App Version to retry the GitHub release check", VisualTone.Warning, PackIconMaterialKind.Update),
            DashboardStatusKind.Update =>
                new DashboardStatusVisual(kind, status, "Release check has not completed", VisualTone.Neutral, PackIconMaterialKind.Update),
            _ => new DashboardStatusVisual(kind, status, "Status is unavailable", VisualTone.Neutral, PackIconMaterialKind.HelpCircleOutline)
        };
    }
}

public sealed class QueueStatusLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return QueueStatusVisual.FromStatus(value as string).Label;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class QueueStatusIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return QueueStatusVisual.FromStatus(value as string).Icon;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class QueueStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ToneBrushes.ForTone(QueueStatusVisual.FromStatus(value as string).Tone);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class VisualToneBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ToneBrushes.ForTone(value is VisualTone tone ? tone : VisualTone.Neutral);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

internal static class ToneBrushes
{
    public static System.Windows.Media.Brush ForTone(VisualTone tone)
    {
        var color = tone switch
        {
            VisualTone.Info => System.Windows.Media.Color.FromRgb(0x58, 0xA6, 0xFF),
            VisualTone.Success => System.Windows.Media.Color.FromRgb(0x7E, 0xD9, 0x92),
            VisualTone.Warning => System.Windows.Media.Color.FromRgb(0xD6, 0xA8, 0x4F),
            VisualTone.Danger => System.Windows.Media.Color.FromRgb(0xE0, 0x6C, 0x75),
            _ => System.Windows.Media.Color.FromRgb(0x8B, 0x98, 0xA5)
        };
        return new SolidColorBrush(color);
    }
}
