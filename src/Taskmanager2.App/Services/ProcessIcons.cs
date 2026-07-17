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

    private static ImageSource? defaultIcon;
    private static bool defaultIconLoaded;

    public static ImageSource? For(int pid) => ByPid.GetOrAdd(pid, static p =>
    {
        var path = TryGetImagePath(p);
        var icon = path is null ? null : ByPath.GetOrAdd(path, TryExtractIcon);
        return icon ?? DefaultIcon();
    });

    /// <summary>Shell stock application icon — the real TM shows it for icon-less processes.</summary>
    private static ImageSource? DefaultIcon()
    {
        if (!defaultIconLoaded)
        {
            defaultIconLoaded = true;
            var info = new ShStockIconInfo { cbSize = (uint)Marshal.SizeOf<ShStockIconInfo>() };
            if (SHGetStockIconInfo(SiidApplication, ShgsiIcon | ShgsiSmallIcon, ref info) == 0 && info.hIcon != IntPtr.Zero)
            {
                try
                {
                    using var icon = System.Drawing.Icon.FromHandle(info.hIcon);
                    using var bitmap = icon.ToBitmap();
                    using var stream = new MemoryStream();
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    var image = new BitmapImage();
                    image.SetSource(stream.AsRandomAccessStream());
                    defaultIcon = image;
                }
                catch (Exception)
                {
                    defaultIcon = null;
                }
                finally
                {
                    DestroyIcon(info.hIcon);
                }
            }
        }

        return defaultIcon;
    }

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

    private const uint SiidApplication = 2;
    private const uint ShgsiIcon = 0x100;
    private const uint ShgsiSmallIcon = 0x1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShStockIconInfo
    {
        public uint cbSize;
        public IntPtr hIcon;
        public int iSysImageIndex;
        public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPath;
    }

    [DllImport("shell32.dll", SetLastError = false)]
    private static extern int SHGetStockIconInfo(uint siid, uint flags, ref ShStockIconInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
