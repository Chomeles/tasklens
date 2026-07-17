using TaskLens.App.Services.Interop;
using TaskLens.Core.Models;
using Taskmanager2.App.Services;

namespace TaskLens.App.SmokeTests;

/// <summary>
/// Windows-only smoke tests for the autostart source (tm2r-04): querying the real Run keys and
/// startup folders must not throw, and a shortcut dropped into the user's startup folder must
/// surface with its resolved target and the no-StartupApproved-record-means-enabled verdict.
/// </summary>
public class RegistryStartupSourceSmokeTests
{
    [Fact]
    public void Query_DoesNotThrow_AndReportsAvailable()
    {
        var snapshot = new RegistryStartupSource().Query();

        Assert.Equal(CatalogAvailability.Available, snapshot.Availability);
        Assert.All(snapshot.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Name)));
    }

    [Fact]
    public void OwnLnkInUserStartupFolder_IsListedWithResolvedTarget()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        Directory.CreateDirectory(folder);
        var stem = "TaskLensSmoke-" + Guid.NewGuid().ToString("N");
        var lnk = Path.Combine(folder, stem + ".lnk");
        var target = Environment.ProcessPath!;
        ShellLink.Create(lnk, target);
        try
        {
            var items = new RegistryStartupSource().Query().Items;

            var item = Assert.Single(items, i => i.Name == stem);
            Assert.Equal("Autostart-Ordner (Benutzer)", item.Source);
            Assert.Equal(target, item.Command, ignoreCase: true);
            Assert.True(item.Enabled); // no StartupApproved record → enabled
            Assert.NotNull(item.ToggleId);
        }
        finally
        {
            File.Delete(lnk);
        }
    }
}
