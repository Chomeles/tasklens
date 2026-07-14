using TaskLens.Core.Models;
using TaskLens.Core.Services;
using TaskLens.Core.ViewModels;

namespace TaskLens.Core.Tests;

/// <summary>tm3-01: end-task commands on <see cref="Tm2ProcessListViewModel"/>.</summary>
public class Tm2ProcessActionsTests
{
    private static readonly DateTime Start = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeProcessActionService actions = new();
    private readonly Tm2ProcessListViewModel vm;

    public Tm2ProcessActionsTests()
    {
        vm = new Tm2ProcessListViewModel(new ProcessListViewModel(), actions);
    }

    private static ProcessDelta Delta(int pid, string name) => new(
        new ProcessSample(pid, name, Start, TimeSpan.Zero, 1024, 0, 0), 0, 0, 0, 0);

    private static SystemSnapshot Snap(params ProcessDelta[] deltas) => new(
        TimestampUtc: Start,
        Processes: deltas,
        Sensors: [],
        SensorAvailability: SensorAvailability.Available,
        CpuTotalPercent: 0,
        MemoryUsedBytes: 0,
        MemoryTotalBytes: 0);

    [Fact]
    public void EndTask_WithoutSelection_CannotExecute()
    {
        vm.ApplySnapshot(Snap(Delta(1, "alpha")));

        Assert.False(vm.EndTaskCommand.CanExecute(null));
        Assert.False(vm.EndTreeCommand.CanExecute(null));
    }

    [Fact]
    public void EndTask_WithoutService_CannotExecute()
    {
        var bare = new Tm2ProcessListViewModel();
        bare.ApplySnapshot(Snap(Delta(1, "alpha")));
        bare.SelectedRow = bare.Rows.Single();

        Assert.False(bare.EndTaskCommand.CanExecute(null));
    }

    [Fact]
    public void EndTask_TerminatesSelectedPid_WithoutTree()
    {
        vm.ApplySnapshot(Snap(Delta(7, "alpha")));
        vm.SelectedRow = vm.Rows.Single();

        Assert.True(vm.EndTaskCommand.CanExecute(null));
        vm.EndTaskCommand.Execute(null);

        Assert.Equal([(7, false)], actions.Calls);
        Assert.Null(vm.LastActionError);
        Assert.False(vm.HasActionError);
    }

    [Fact]
    public void EndTree_PassesEntireTreeFlag()
    {
        vm.ApplySnapshot(Snap(Delta(7, "alpha")));
        vm.SelectedRow = vm.Rows.Single();

        vm.EndTreeCommand.Execute(null);

        Assert.Equal([(7, true)], actions.Calls);
    }

    [Fact]
    public void FailedAction_SurfacesError_AndNextSuccessClearsIt()
    {
        vm.ApplySnapshot(Snap(Delta(7, "alpha")));
        vm.SelectedRow = vm.Rows.Single();

        actions.Result = ActionResult.Fail("Zugriff verweigert");
        vm.EndTaskCommand.Execute(null);
        Assert.Equal("Zugriff verweigert", vm.LastActionError);
        Assert.True(vm.HasActionError);

        actions.Result = ActionResult.Ok;
        vm.EndTaskCommand.Execute(null);
        Assert.Null(vm.LastActionError);
        Assert.False(vm.HasActionError);
    }

    [Fact]
    public void SelectedRow_Exits_SelectionClears()
    {
        vm.ApplySnapshot(Snap(Delta(1, "alpha"), Delta(2, "beta")));
        vm.SelectedRow = vm.Rows.Single(r => r.Pid == 2);

        vm.ApplySnapshot(Snap(Delta(1, "alpha")));

        Assert.Null(vm.SelectedRow);
        Assert.False(vm.EndTaskCommand.CanExecute(null));
    }

    [Fact]
    public void SetPriority_PassesPidAndPriority()
    {
        vm.ApplySnapshot(Snap(Delta(7, "alpha")));
        vm.SelectedRow = vm.Rows.Single();

        vm.SetPriorityCommand.Execute(ProcessPriority.BelowNormal);

        Assert.Equal([(7, ProcessPriority.BelowNormal)], actions.PriorityCalls);
    }

    [Fact]
    public void EfficiencyMode_EnablesOnSelectedPid()
    {
        vm.ApplySnapshot(Snap(Delta(7, "alpha")));
        vm.SelectedRow = vm.Rows.Single();

        vm.EnableEfficiencyModeCommand.Execute(null);

        Assert.Equal([(7, true)], actions.EfficiencyCalls);
    }

    [Fact]
    public void OpenFileLocation_PassesSelectedPid()
    {
        vm.ApplySnapshot(Snap(Delta(7, "alpha")));
        vm.SelectedRow = vm.Rows.Single();

        vm.OpenFileLocationCommand.Execute(null);

        Assert.Equal([7], actions.OpenLocationPids);
    }

    [Fact]
    public void RunNewTask_TrimsCommand_AndSurfacesFailure()
    {
        vm.RunNewTask("  notepad.exe  ");
        Assert.Equal(["notepad.exe"], actions.RunCommands);
        Assert.Null(vm.LastActionError);

        actions.Result = ActionResult.Fail("Das System kann die angegebene Datei nicht finden");
        vm.RunNewTask("gibtsnicht.exe");
        Assert.Equal("Das System kann die angegebene Datei nicht finden", vm.LastActionError);
    }

    [Fact]
    public void RunNewTask_BlankCommand_DoesNothing()
    {
        vm.RunNewTask("   ");

        Assert.Empty(actions.RunCommands);
        Assert.Null(vm.LastActionError);
    }

    [Fact]
    public void ActionCommands_WithoutSelection_CannotExecute()
    {
        vm.ApplySnapshot(Snap(Delta(1, "alpha")));

        Assert.False(vm.SetPriorityCommand.CanExecute(ProcessPriority.Normal));
        Assert.False(vm.EnableEfficiencyModeCommand.CanExecute(null));
        Assert.False(vm.OpenFileLocationCommand.CanExecute(null));
    }

    [Fact]
    public void SelectedRow_FilteredOut_SelectionClears()
    {
        vm.ApplySnapshot(Snap(Delta(1, "alpha"), Delta(2, "beta")));
        vm.SelectedRow = vm.Rows.Single(r => r.Pid == 2);

        vm.Inner.Filter = "alpha";

        Assert.Null(vm.SelectedRow);
    }
}
