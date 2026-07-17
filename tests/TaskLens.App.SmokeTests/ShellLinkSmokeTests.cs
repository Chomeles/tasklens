using TaskLens.App.Services.Interop;

namespace TaskLens.App.SmokeTests;

/// <summary>
/// Windows-only smoke tests for the IShellLinkW interop (tm2r-04): a shortcut written through
/// IPersistFile::Save must resolve back to its target, garbage must come back as null, not throw.
/// xunit runs on MTA threads, so these also exercise the dedicated-STA-thread hop.
/// </summary>
public class ShellLinkSmokeTests
{
    [Fact]
    public void CreatedLnk_ResolvesBackToItsTarget()
    {
        var lnk = Path.Combine(Path.GetTempPath(), "TaskLensSmoke-" + Guid.NewGuid().ToString("N") + ".lnk");
        var target = Environment.ProcessPath!;
        ShellLink.Create(lnk, target);
        try
        {
            Assert.Equal(target, ShellLink.TryGetTarget(lnk)!, ignoreCase: true);
        }
        finally
        {
            File.Delete(lnk);
        }
    }

    [Fact]
    public void GarbageLnkFile_ResolvesToNull()
    {
        var path = Path.Combine(Path.GetTempPath(), "TaskLensSmoke-" + Guid.NewGuid().ToString("N") + ".lnk");
        File.WriteAllBytes(path, [0xDE, 0xAD, 0xBE, 0xEF]);
        try
        {
            Assert.Null(ShellLink.TryGetTarget(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
