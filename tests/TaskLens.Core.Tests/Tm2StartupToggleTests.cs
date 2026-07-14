using TaskLens.Core.Models;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

/// <summary>tm3-06: Aktivieren/Deaktivieren on <see cref="Tm2StartupViewModel"/>.</summary>
public class Tm2StartupToggleTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeStartupItemSource source = new();
    private readonly FakeStartupManager manager = new();
    private readonly Tm2StartupViewModel vm;

    public Tm2StartupToggleTests()
    {
        vm = new Tm2StartupViewModel(source, manager);
    }

    private static SystemSnapshot Snap() => new(
        TimestampUtc: Start,
        Processes: [],
        Sensors: [],
        SensorAvailability: SensorAvailability.Available,
        CpuTotalPercent: 0,
        MemoryUsedBytes: 0,
        MemoryTotalBytes: 0);

    private static StartupItem Item(string name, bool enabled = true) =>
        new(name, @"C:\app.exe", "Registry (HKLM)", enabled, ToggleId: "id-" + name);

    private void Refresh()
    {
        for (var i = 0; i < Tm2ServicesViewModel.QueryEveryNthTick; i++)
        {
            vm.ApplySnapshot(Snap());
        }
    }

    [Fact]
    public void Toggle_WithoutSelection_CannotExecute()
    {
        source.Snapshot = new StartupSnapshot([Item("alpha")], CatalogAvailability.Available);
        Refresh();

        Assert.False(vm.ToggleSelectedCommand.CanExecute(null));
        Assert.Equal("Aktivieren", vm.ToggleButtonText); // no selection: neutral offer
    }

    [Fact]
    public void Toggle_WithoutManager_CannotExecute()
    {
        var bare = new Tm2StartupViewModel(source);
        source.Snapshot = new StartupSnapshot([Item("alpha")], CatalogAvailability.Available);
        bare.ApplySnapshot(Snap());
        bare.SelectedRow = bare.Rows.Single();

        Assert.False(bare.ToggleSelectedCommand.CanExecute(null));
    }

    [Fact]
    public void Toggle_EnabledRow_DisablesIt_AndFlipsOptimistically()
    {
        source.Snapshot = new StartupSnapshot([Item("alpha", enabled: true)], CatalogAvailability.Available);
        Refresh();
        vm.SelectedRow = vm.Rows.Single();
        Assert.Equal("Deaktivieren", vm.ToggleButtonText);

        vm.ToggleSelectedCommand.Execute(null);

        var call = Assert.Single(manager.Calls);
        Assert.Equal("id-alpha", call.Item.ToggleId);
        Assert.False(call.Enabled);
        Assert.False(vm.Rows.Single().Enabled);
        Assert.Equal("Deaktiviert", vm.Rows.Single().StatusText);
        Assert.Equal("Aktivieren", vm.ToggleButtonText);
        Assert.Null(vm.LastActionError);
    }

    [Fact]
    public void Toggle_Success_ForcesRequeryOnNextTick()
    {
        source.Snapshot = new StartupSnapshot([Item("alpha")], CatalogAvailability.Available);
        Refresh();
        var queriesBefore = source.QueryCount;
        vm.SelectedRow = vm.Rows.Single();

        vm.ToggleSelectedCommand.Execute(null);
        vm.ApplySnapshot(Snap()); // would normally be skipped by the cadence

        Assert.Equal(queriesBefore + 1, source.QueryCount);
    }

    [Fact]
    public void Toggle_Failure_SurfacesError_AndKeepsRowState()
    {
        source.Snapshot = new StartupSnapshot([Item("alpha", enabled: true)], CatalogAvailability.Available);
        Refresh();
        vm.SelectedRow = vm.Rows.Single();
        manager.Result = ActionResult.Fail("Zugriff verweigert");

        vm.ToggleSelectedCommand.Execute(null);

        Assert.Equal("Zugriff verweigert", vm.LastActionError);
        Assert.True(vm.HasActionError);
        Assert.True(vm.Rows.Single().Enabled);
    }

    [Fact]
    public void SelectedRow_Removed_SelectionClears()
    {
        source.Snapshot = new StartupSnapshot([Item("alpha"), Item("beta")], CatalogAvailability.Available);
        Refresh();
        vm.SelectedRow = vm.Rows.Single(r => r.Name == "beta");

        source.Snapshot = new StartupSnapshot([Item("alpha")], CatalogAvailability.Available);
        Refresh();

        Assert.Null(vm.SelectedRow);
        Assert.False(vm.ToggleSelectedCommand.CanExecute(null));
    }
}
