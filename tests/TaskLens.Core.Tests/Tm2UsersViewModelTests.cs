using TaskLens.Core.Models;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

public class Tm2UsersViewModelTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeUserSessionSource source = new();
    private readonly FakeSessionActions actions = new();
    private readonly Tm2UsersViewModel vm;

    public Tm2UsersViewModelTests()
    {
        vm = new Tm2UsersViewModel(source, actions);
    }

    private static SystemSnapshot Snap() => new(
        TimestampUtc: Start,
        Processes: [],
        Sensors: [],
        SensorAvailability: SensorAvailability.Available,
        CpuTotalPercent: 0,
        MemoryUsedBytes: 0,
        MemoryTotalBytes: 0);

    /// <summary>Ticks exactly one divider period, so each call performs exactly one source query.</summary>
    private void Refresh()
    {
        for (var i = 0; i < Tm2ServicesViewModel.QueryEveryNthTick; i++)
        {
            vm.ApplySnapshot(Snap());
        }
    }

    [Fact]
    public void Rows_OrderedBySessionId()
    {
        source.Snapshot = new(
            [new UserSession(4, "gast", "Getrennt"), new UserSession(1, "orkan", "Aktiv")],
            CatalogAvailability.Available);

        Refresh();

        Assert.Equal([1, 4], vm.Rows.Select(r => r.SessionId));
        Assert.Equal(["orkan", "gast"], vm.Rows.Select(r => r.UserName));
        Assert.Equal(["1", "4"], vm.Rows.Select(r => r.SessionIdText));
    }

    [Fact]
    public void AccessDenied_ShowsBanner_NoRows_AndRecovers()
    {
        source.Snapshot = new([], CatalogAvailability.AccessDenied);
        Refresh();

        Assert.True(vm.ShowBanner);
        Assert.NotEqual("", vm.BannerText);
        Assert.Empty(vm.Rows);

        source.Snapshot = new([new UserSession(1, "orkan", "Aktiv")], CatalogAvailability.Available);
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
    public void StateChange_KeepsRowObject_UpdatesValues()
    {
        source.Snapshot = new([new UserSession(1, "orkan", "Aktiv")], CatalogAvailability.Available);
        Refresh();
        var row = vm.Rows.Single();

        source.Snapshot = new([new UserSession(1, "orkan", "Getrennt")], CatalogAvailability.Available);
        Refresh();

        Assert.Same(row, vm.Rows.Single());
        Assert.Equal("Getrennt", row.State);
    }

    [Fact]
    public void LoggedOffSession_DisappearsOnNextQuery()
    {
        source.Snapshot = new(
            [new UserSession(1, "orkan", "Aktiv"), new UserSession(2, "gast", "Getrennt")],
            CatalogAvailability.Available);
        Refresh();

        source.Snapshot = new([new UserSession(1, "orkan", "Aktiv")], CatalogAvailability.Available);
        Refresh();

        Assert.Equal([1], vm.Rows.Select(r => r.SessionId));
    }
    [Fact]
    public void SessionActions_RunOnSelectedRow_AndSurfaceErrors()
    {
        source.Snapshot = new([new UserSession(3, "orkan", "Aktiv")], CatalogAvailability.Available);
        vm.ApplySnapshot(Snap());
        vm.SelectedRow = vm.Rows.Single();

        Assert.True(vm.DisconnectSelectedCommand.CanExecute(null));
        vm.DisconnectSelectedCommand.Execute(null);
        vm.LogoffSelectedCommand.Execute(null);
        Assert.Equal([(3, "disconnect"), (3, "logoff")], actions.Calls);
        Assert.Null(vm.LastActionError);

        actions.Result = ActionResult.Fail("nope");
        vm.LogoffSelectedCommand.Execute(null);
        Assert.Equal("nope", vm.LastActionError);
    }

    [Fact]
    public void SessionActions_WithoutSelection_CannotExecute()
    {
        Assert.False(vm.DisconnectSelectedCommand.CanExecute(null));
        Assert.False(vm.LogoffSelectedCommand.CanExecute(null));
    }
}
