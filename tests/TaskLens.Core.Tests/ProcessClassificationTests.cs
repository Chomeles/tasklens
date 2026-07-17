using TaskLens.Core.Models;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class ProcessClassificationTests
{
    private static ProcessSample Sample(string name, bool hasVisibleWindow = false) =>
        new(Pid: 1, Name: name, StartTimeUtc: DateTime.UtcNow, TotalCpuTime: TimeSpan.Zero,
            WorkingSetBytes: 0, IoReadBytes: 0, IoWriteBytes: 0, HasVisibleWindow: hasVisibleWindow);

    [Theory]
    [InlineData("svchost.exe")]
    [InlineData("csrss")]
    [InlineData("System")]
    [InlineData("Registry")]
    [InlineData("dwm.exe")]
    public void Classify_WellKnownSystemNames_AreSystemGroup(string name) =>
        Assert.Equal(ProcessGroup.System, ProcessClassification.Classify(Sample(name)));

    [Fact]
    public void Classify_VisibleWindow_IsAppsGroup() =>
        Assert.Equal(ProcessGroup.Apps, ProcessClassification.Classify(Sample("notepad.exe", hasVisibleWindow: true)));

    [Fact]
    public void Classify_NoWindow_NotSystem_IsBackgroundGroup() =>
        Assert.Equal(ProcessGroup.Background, ProcessClassification.Classify(Sample("SomeService.exe")));

    [Fact]
    public void Classify_SystemNameWins_EvenWithVisibleWindow() =>
        Assert.Equal(ProcessGroup.System, ProcessClassification.Classify(Sample("svchost.exe", hasVisibleWindow: true)));

    [Theory]
    [InlineData(ProcessGroup.Apps, "Apps")]
    [InlineData(ProcessGroup.Background, "Hintergrundprozesse")]
    [InlineData(ProcessGroup.System, "Windows-Prozesse")]
    public void Label_ReturnsGermanTaskManagerWording(ProcessGroup group, string expected) =>
        Assert.Equal(expected, ProcessClassification.Label(group));
}
