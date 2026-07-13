using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;
using Xunit;

namespace TaskLens.Core.Tests;

public class PawnIoDetectorTests
{
    [Fact]
    public void IsInstalled_ReturnsTrue_WhenDriverFileExists()
    {
        const string driversDir = @"C:\Windows\System32\drivers";
        var installed = PawnIoDetector.IsInstalled(
            driversDir,
            path => path == Path.Combine(driversDir, PawnIoDetector.DriverFileName));

        Assert.True(installed);
    }

    [Fact]
    public void IsInstalled_ReturnsFalse_WhenDriverFileMissing()
    {
        var installed = PawnIoDetector.IsInstalled(@"C:\Windows\System32\drivers", _ => false);

        Assert.False(installed);
    }
}

public class PawnIoBannerViewModelTests
{
    [Fact]
    public void ShowBanner_IsFalse_WhenPawnIoInstalled()
    {
        var vm = new PawnIoBannerViewModel(isPawnIoInstalled: true);

        Assert.False(vm.ShowBanner);
        Assert.Equal("", vm.BannerText);
    }

    [Fact]
    public void ShowBanner_IsTrue_WhenPawnIoMissing_WithInstallHint()
    {
        var vm = new PawnIoBannerViewModel(isPawnIoInstalled: false);

        Assert.True(vm.ShowBanner);
        Assert.Contains("PawnIO", vm.BannerText);
        Assert.Contains(PawnIoBannerViewModel.InstallHintUrl, vm.BannerText);
    }
}
