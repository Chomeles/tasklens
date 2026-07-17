using TaskLens.Core.Models;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class Tm2StartupViewModelTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeStartupItemSource source = new();
    private readonly Tm2StartupViewModel vm;

    public Tm2StartupViewModelTests()
    {
        vm = new Tm2StartupViewModel(source);
    }

    private static SystemSnapshot Snap() => new(
        TimestampUtc: Start,
        Processes: [],
        Sensors: [],
        SensorAvailability: SensorAvailability.Available,
        CpuTotalPercent: 0,
        MemoryUsedBytes: 0,
        MemoryTotalBytes: 0);

    private static StartupItem Item(
        string name, string source = "Registry (HKLM)", string command = @"C:\app.exe", bool enabled = true) =>
        new(name, command, source, enabled);

    /// <summary>Ticks exactly one divider period, so each call performs exactly one source query.</summary>
    private void Refresh()
    {
        for (var i = 0; i < Tm2ServicesViewModel.QueryEveryNthTick; i++)
        {
            vm.ApplySnapshot(Snap());
        }
    }

    [Fact]
    public void Rows_AlphabeticalByName_ThenBySource()
    {
        source.Snapshot = new(
            [
                Item("Updater", source: "Registry (HKCU)"),
                Item("Cloud-Sync"),
                Item("Updater", source: "Registry (HKLM)"),
                Item("aTuner"),
            ],
            CatalogAvailability.Available);

        Refresh();

        Assert.Equal(["aTuner", "Cloud-Sync", "Updater", "Updater"], vm.Rows.Select(r => r.Name));
        Assert.Equal(["Registry (HKCU)", "Registry (HKLM)"], vm.Rows.Skip(2).Select(r => r.Source));
    }

    [Fact]
    public void Publisher_FlowsToRow_EmptyRendersAsDash()
    {
        source.Snapshot = new(
            [
                Item("Cloud-Sync") with { Publisher = "Contoso GmbH" },
                Item("Updater"),
            ],
            CatalogAvailability.Available);

        Refresh();

        Assert.Equal(["Contoso GmbH", "—"], vm.Rows.Select(r => r.PublisherText));
        Assert.Equal(["Contoso GmbH", ""], vm.Rows.Select(r => r.Publisher));
    }

    [Fact]
    public void AccessDenied_ShowsBanner_NoRows_AndRecovers()
    {
        source.Snapshot = new([], CatalogAvailability.AccessDenied);
        Refresh();

        Assert.True(vm.ShowBanner);
        Assert.NotEqual("", vm.BannerText);
        Assert.Empty(vm.Rows);

        source.Snapshot = new([Item("Cloud-Sync")], CatalogAvailability.Available);
        Refresh();

        Assert.False(vm.ShowBanner);
        Assert.Equal("", vm.BannerText);
        Assert.Single(vm.Rows);
    }

    [Fact]
    public void ApplySnapshot_QueriesSource_OnlyEveryNthTick()
    {
        vm.ApplySnapshot(Snap());
        Assert.Equal(1, source.QueryCount); // first tick queries immediately

        for (var i = 1; i < 3 * Tm2ServicesViewModel.QueryEveryNthTick; i++)
        {
            vm.ApplySnapshot(Snap());
        }

        Assert.Equal(3, source.QueryCount);
    }

    [Fact]
    public void EmptySource_NoRows_NoBanner()
    {
        Refresh();

        Assert.Empty(vm.Rows);
        Assert.False(vm.ShowBanner);
    }

    [Fact]
    public void DisabledItem_KeepsRowObject_UpdatesStatusText()
    {
        source.Snapshot = new([Item("Cloud-Sync"), Item("Updater")], CatalogAvailability.Available);
        Refresh();
        var updater = vm.Rows.Single(r => r.Name == "Updater");
        Assert.Equal("Aktiviert", updater.StatusText);

        source.Snapshot = new(
            [Item("Cloud-Sync"), Item("Updater", enabled: false, command: @"C:\updater.exe /quiet")],
            CatalogAvailability.Available);
        Refresh();

        Assert.Same(updater, vm.Rows.Single(r => r.Name == "Updater"));
        Assert.Equal("Deaktiviert", updater.StatusText);
        Assert.Equal(@"C:\updater.exe /quiet", updater.Command);
    }

    [Fact]
    public void RemovedItem_DisappearsOnNextQuery()
    {
        source.Snapshot = new([Item("Cloud-Sync"), Item("Updater")], CatalogAvailability.Available);
        Refresh();

        source.Snapshot = new([Item("Cloud-Sync")], CatalogAvailability.Available);
        Refresh();

        Assert.Equal(["Cloud-Sync"], vm.Rows.Select(r => r.Name));
    }

    [Fact]
    public void SameNameAndSource_DifferentToggleIds_AreDistinctRows_NoReconcileCrash()
    {
        // Foo.lnk + Foo.exe in the same startup folder: identical stem and source, distinct
        // ToggleIds. With a (Source, Name)-only key both collapsed onto one row instance,
        // which crashed CollectionReconciler.Reconcile (Move past the end) on the second pass.
        source.Snapshot = new(
            [
                Item("Foo", source: "Autostart-Ordner (Benutzer)") with { ToggleId = "HKCU\nStartupFolder\nFoo.lnk" },
                Item("Foo", source: "Autostart-Ordner (Benutzer)") with { ToggleId = "HKCU\nStartupFolder\nFoo.exe" },
            ],
            CatalogAvailability.Available);

        Refresh();
        Refresh();

        Assert.Equal(2, vm.Rows.Count);
        Assert.NotSame(vm.Rows[0], vm.Rows[1]);
    }

    [Fact]
    public void SameName_DifferentSources_AreDistinctRows()
    {
        source.Snapshot = new(
            [Item("Updater", source: "Registry (HKLM)"), Item("Updater", source: "Registry (HKCU)")],
            CatalogAvailability.Available);

        Refresh();

        Assert.Equal(2, vm.Rows.Count);
        Assert.NotSame(vm.Rows[0], vm.Rows[1]);
    }
}
