using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Taskmanager2.App.Services;

/// <summary>
/// Per-process icons for the Prozesse list, like the real Task Manager. Resolves the executable
/// path via QueryFullProcessImageName, extracts the associated shell icon with System.Drawing, and
/// caches the resulting <see cref="ImageSource"/> per path (and per PID) forever — processes do not
/// change their icon. Called from x:Bind on the UI thread; extraction is fast and happens once per
/// unique executable.
/// </summary>
public static class ProcessIcons
{
    private static readonly ConcurrentDictionary<int, ImageSource?> ByPid = new();
    private static readonly ConcurrentDictionary<string, ImageSource?> ByPath = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? For(int pid) => ByPid.GetOrAdd(pid, static p =>
    {
        var path = TryGetImagePath(p);
        return path is null ? null : ByPath.GetOrAdd(path, TryExtractIcon);
    });

    private static ImageSource? TryExtractIcon(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }

            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            var image = new BitmapImage();
            image.SetSource(stream.AsRandomAccessStream());
            return image;
        }
        catch (Exception)
        {
            return null; // access denied / odd binaries — row simply renders without an icon
        }
    }

    private static string? TryGetImagePath(int pid)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var capacity = 1024;
            var buffer = new char[capacity];
            return QueryFullProcessImageName(handle, 0, buffer, ref capacity) && capacity > 0
                ? new string(buffer, 0, capacity)
                : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private const int ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, char[] exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
