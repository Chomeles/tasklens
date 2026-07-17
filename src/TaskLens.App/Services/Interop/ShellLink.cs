using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskLens.App.Services.Interop;

/// <summary>
/// Minimal <c>IShellLinkW</c>/<c>IPersistFile</c> COM interop for .lnk files (tm2r-04):
/// <see cref="TryGetTarget"/> resolves a shortcut's stored target path for the Autostart page,
/// <see cref="Create"/> writes a shortcut (used by the smoke test to exercise the round trip).
/// The ShellLink coclass is apartment-threaded — calls from an MTA thread (engine tick, xunit)
/// hop onto a short-lived dedicated STA thread; on an already-STA thread they run inline.
/// </summary>
internal static class ShellLink
{
    /// <summary>
    /// The shortcut's target path, or null when the file is not a readable .lnk or stores no
    /// path-based target (e.g. shell-namespace-only links). Never throws.
    /// </summary>
    public static string? TryGetTarget(string lnkPath)
    {
        try
        {
            return RunSta(() =>
            {
                var link = (IShellLinkW)new ShellLinkCoClass();
                try
                {
                    ((IPersistFile)link).Load(lnkPath, StgmRead);
                    var buffer = new StringBuilder(MaxPath);
                    link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
                    var target = buffer.ToString();
                    return target.Length > 0 ? target : null;
                }
                finally
                {
                    Marshal.ReleaseComObject(link);
                }
            });
        }
        catch (Exception)
        {
            return null; // broken/foreign .lnk — callers fall back to the .lnk path itself
        }
    }

    /// <summary>Writes a .lnk pointing at <paramref name="targetPath"/>; throws on failure.</summary>
    public static void Create(string lnkPath, string targetPath) => RunSta<object?>(() =>
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        try
        {
            link.SetPath(targetPath);
            ((IPersistFile)link).Save(lnkPath, fRemember: true);
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    });

    private static T RunSta<T>(Func<T> func)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return func();
        }

        var result = default(T)!;
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception e)
            {
                error = ExceptionDispatchInfo.Capture(e);
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
        return result;
    }

    // ponytail: MAX_PATH buffer — IShellLinkW::GetPath is documented against MAX_PATH; longer
    // targets in a startup folder are exotic enough to accept truncation-to-miss.
    private const int MaxPath = 260;

    private const uint StgmRead = 0;

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass
    {
    }

    /// <summary>Full vtable in declaration order — COM dispatches by slot, not by name.</summary>
    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out][MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    /// <summary>Includes the inherited IPersist slot (GetClassID) — same vtable rule as above.</summary>
    [ComImport]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassId);

        [PreserveSig]
        int IsDirty();

        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile(out IntPtr ppszFileName);
    }
}
