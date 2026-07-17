using TaskLens.Core.Models;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class Tm2ServicesViewModelTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeServiceCatalog catalog = new();
    private readonly FakeServiceControl control = new();
    private readonly Tm2ServicesViewModel vm;

    public Tm2ServicesViewModelTests()
    {
        vm = new Tm2ServicesViewModel(catalog, control);
    }

    private static SystemSnapshot Snap() => new(
        TimestampUtc: Start,
        Processes: [],
        Sensors: [],
        SensorAvailability: SensorAvailability.Available,
        CpuTotalPercent: 0,
        MemoryUsedBytes: 0,
        MemoryTotalBytes: 0);

    private static ServiceEntry Svc(
        string name, bool running = true, int? pid = null, string display = "", string description = "") =>
        new(name, display.Length == 0 ? name : display, pid, description, running);

    /// <summary>Ticks exactly one divider period, so each call performs exactly one catalog query.</summary>
    private void Refresh()
    {
        for (var i = 0; i < Tm2ServicesViewModel.QueryEveryNthTick; i++)
        {
            vm.ApplySnapshot(Snap());
        }
    }

    [Fact]
    public void Rows_GroupRunningFirst_AlphabeticalWithinGroups()
    {
        catalog.Snapshot = new(
            [Svc("Wuauserv", running: false), Svc("Themes"), Svc("Appinfo", running: false), Svc("BITS")],
            ServiceCatalogAvailability.Available);

        Refresh();

        Assert.Equal(["BITS", "Themes", "Appinfo", "Wuauserv"], vm.Rows.Select(r => r.Name));
        Assert.Equal(["Wird ausgeführt", "Wird ausgeführt", "Beendet", "Beendet"], vm.Rows.Select(r => r.StatusText));
    }

    [Fact]
    public void Filter_CaseInsensitive_OverNameDisplayNameAndDescription()
    {
        catalog.Snapshot = new(
            [
                Svc("Spooler"),
                Svc("PlugPlay", display: "Plug & Play mit SPOOLER-Anteil"),
                Svc("BITS", description: "Überträgt Dateien im Spooler-Leerlauf."),
                Svc("Themes"),
            ],
            ServiceCatalogAvailability.Available);
        Refresh();

        vm.Filter = "spooler";

        Assert.Equal(["BITS", "PlugPlay", "Spooler"], vm.Rows.Select(r => r.Name));
        // Filtering works on the cached rows — it must not hit the catalog again.
        Assert.Equal(1, catalog.QueryCount);
    }

    [Fact]
    public void AccessDenied_ShowsBanner_NoRows_AndRecovers()
    {
        catalog.Snapshot = new([], ServiceCatalogAvailability.AccessDenied);
        Refresh();

        Assert.True(vm.ShowBanner);
        Assert.NotEqual("", vm.BannerText);
        Assert.Empty(vm.Rows);

        catalog.Snapshot = new([Svc("Themes", pid: 1204)], ServiceCatalogAvailability.Available);
        Refresh();

        Assert.False(vm.ShowBanner);
        Assert.Equal("", vm.BannerText);
        Assert.Single(vm.Rows);
    }

    [Fact]
    public void ApplySnapshot_QueriesCatalog_OnlyEveryNthTick()
    {
        vm.ApplySnapshot(Snap());
        Assert.Equal(1, catalog.QueryCount); // first tick queries immediately

        for (var i = 1; i < 3 * Tm2ServicesViewModel.QueryEveryNthTick; i++)
        {
            vm.ApplySnapshot(Snap());
        }

        Assert.Equal(3, catalog.QueryCount);
    }

    [Fact]
    public void EmptyCatalog_NoRows_NoBanner()
    {
        Refresh();

        Assert.Empty(vm.Rows);
        Assert.False(vm.ShowBanner);
    }

    [Fact]
    public void StatusChange_KeepsRowObject_UpdatesTexts_AndRegroups()
    {
        catalog.Snapshot = new([Svc("Themes"), Svc("Wuauserv", pid: 2048)], ServiceCatalogAvailability.Available);
        Refresh();
        var wuauserv = vm.Rows.Single(r => r.Name == "Wuauserv");
        Assert.Equal("2048", wuauserv.PidText);

        catalog.Snapshot = new([Svc("Themes"), Svc("Wuauserv", running: false)], ServiceCatalogAvailability.Available);
        Refresh();

        Assert.Same(wuauserv, vm.Rows.Single(r => r.Name == "Wuauserv"));
        Assert.Equal("", wuauserv.PidText);
        Assert.Equal("Beendet", wuauserv.StatusText);
        Assert.Equal(["Themes", "Wuauserv"], vm.Rows.Select(r => r.Name)); // regrouped: stopped last
    }

    [Fact]
    public void UninstalledService_DisappearsOnNextQuery()
    {
        catalog.Snapshot = new([Svc("Themes"), Svc("Wuauserv")], ServiceCatalogAvailability.Available);
        Refresh();

        catalog.Snapshot = new([Svc("Themes")], ServiceCatalogAvailability.Available);
        Refresh();

        Assert.Equal(["Themes"], vm.Rows.Select(r => r.Name));
    }
    [Fact]
    public void ServiceActions_RunOnSelectedRow_AndForceRequery()
    {
        catalog.Snapshot = new([Svc("Spooler", running: false)], ServiceCatalogAvailability.Available);
        Refresh();
        vm.SelectedRow = vm.Rows.Single();

        Assert.True(vm.StartSelectedCommand.CanExecute(null));
        vm.StartSelectedCommand.Execute(null);
        vm.StopSelectedCommand.Execute(null);
        vm.RestartSelectedCommand.Execute(null);

        Assert.Equal([("Spooler", "start"), ("Spooler", "stop"), ("Spooler", "restart")], control.Calls);
        Assert.Null(vm.LastActionError);

        var queriesBefore = catalog.QueryCount;
        vm.ApplySnapshot(Snap()); // action reset the tick divider — next tick re-queries immediately
        Assert.Equal(queriesBefore + 1, catalog.QueryCount);
    }

    [Fact]
    public void FailedServiceAction_SurfacesError()
    {
        catalog.Snapshot = new([Svc("Spooler")], ServiceCatalogAvailability.Available);
        Refresh();
        vm.SelectedRow = vm.Rows.Single();
        control.Result = ActionResult.Fail("Zugriff verweigert");

        vm.StopSelectedCommand.Execute(null);

        Assert.Equal("Zugriff verweigert", vm.LastActionError);
        Assert.True(vm.HasActionError);
    }

    [Fact]
    public void ServiceActions_WithoutSelection_CannotExecute()
    {
        Assert.False(vm.StartSelectedCommand.CanExecute(null));
        Assert.False(vm.StopSelectedCommand.CanExecute(null));
        Assert.False(vm.RestartSelectedCommand.CanExecute(null));
    }
}
