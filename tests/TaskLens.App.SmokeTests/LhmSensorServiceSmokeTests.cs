using TaskLens.App.Services;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.App.SmokeTests;

/// <summary>
/// Windows-only smoke test for the real LibreHardwareMonitor path. GitHub windows-latest runners
/// are VMs with no SuperIO/MSR/SMBus hardware and no PawnIO driver (research.md §10), so this
/// asserts the VM "no sensors" shape — no temperature/fan/power readings, degradation reported as
/// data — rather than specific sensors. The tree→model mapping itself is unit-tested in Core.
/// </summary>
public class LhmSensorServiceSmokeTests
{
    [Fact]
    public void Lifecycle_OpenSampleClose_YieldsVmShapedSnapshot()
    {
        SensorSnapshot snapshot;
        using (var service = new LhmSensorService())
        {
            // Computer.Open() enumerates hardware and can take a while on first run.
            Assert.True(service.WaitForFirstSample(TimeSpan.FromMinutes(2)), "sampling thread never published");
            snapshot = service.Sample();
        } // Dispose joins the thread and runs Computer.Close(); a wedge fails the test by timeout

        // A VM exposes no thermal, fan or power hardware — with or without a driver.
        Assert.DoesNotContain(
            snapshot.Readings,
            r => r.Kind is SensorKind.Temperature or SensorKind.Fan or SensorKind.Power);

        if (snapshot.Readings.Count == 0)
        {
            // An empty tree must be explained, never presented as healthy (CI runs elevated, so
            // the exact state is NoPawnIo/NoSensors depending on the runner image).
            Assert.NotEqual(SensorAvailability.Available, snapshot.Availability);
        }
        else
        {
            // Driverless sensors (e.g. CPU load) may still appear on a VM; then the snapshot is
            // healthy and every reading must be well-formed.
            Assert.Equal(SensorAvailability.Available, snapshot.Availability);
            Assert.All(snapshot.Readings, r =>
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Hardware));
                Assert.False(string.IsNullOrWhiteSpace(r.Name));
            });
        }
    }
}
