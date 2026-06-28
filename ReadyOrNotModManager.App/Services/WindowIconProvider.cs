using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;

namespace ReadyOrNotModManager.App.Services;

public static class WindowIconProvider
{
    public static ImageSource? LoadPreferredIcon(string installDirectory)
    {
        var gameExecutable = ReadyOrNotLauncher.FindDirectExecutable(installDirectory);
        return LoadExecutableIcon(gameExecutable) ?? LoadExecutableIcon(GetCurrentExecutablePath());
    }

    private static ImageSource? LoadExecutableIcon(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        using var icon = DrawingIcon.ExtractAssociatedIcon(executablePath);
        if (icon is null)
        {
            return null;
        }

        using var bitmap = icon.ToBitmap();
        var handle = bitmap.GetHicon();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    private static string? GetCurrentExecutablePath()
    {
        return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
