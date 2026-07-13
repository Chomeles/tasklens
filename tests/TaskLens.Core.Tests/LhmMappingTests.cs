using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.Core.Tests;

public class LhmMappingTests
{
    [Theory]
    [InlineData("Temperature", SensorKind.Temperature)]
    [InlineData("Load", SensorKind.Load)]
    [InlineData("Clock", SensorKind.Clock)]
    [InlineData("Fan", SensorKind.Fan)]
    [InlineData("Power", SensorKind.Power)]
    [InlineData("Voltage", SensorKind.Voltage)]
    public void MapKind_MapsEveryShownLhmType(string lhmName, SensorKind expected)
    {
        Assert.Equal(expected, LhmMapping.MapKind(lhmName));
    }

    [Theory]
    [InlineData("Control")]
    [InlineData("Data")]
    [InlineData("SmallData")]
    [InlineData("Throughput")]
    [InlineData("Level")]
    [InlineData("Factor")]
    [InlineData("Frequency")]
    [InlineData("Energy")]
    [InlineData("Current")]
    [InlineData("")]
    [InlineData("temperature")] // LHM enum names are case-sensitive
    public void MapKind_UnshownTypes_ReturnNull(string lhmName)
    {
        Assert.Null(LhmMapping.MapKind(lhmName));
    }

    [Theory]
    [InlineData(true, false, false, SensorAvailability.Available)] // readings trump everything
    [InlineData(true, true, true, SensorAvailability.Available)]
    [InlineData(false, false, false, SensorAvailability.NoAdmin)] // elevation first: most actionable
    [InlineData(false, false, true, SensorAvailability.NoAdmin)]
    [InlineData(false, true, false, SensorAvailability.NoPawnIo)]
    [InlineData(false, true, true, SensorAvailability.NoSensors)] // fully provisioned, still empty: the VM state
    public void ClassifyAvailability_Precedence(
        bool hasReadings, bool isElevated, bool isPawnIoInstalled, SensorAvailability expected)
    {
        Assert.Equal(expected, LhmMapping.ClassifyAvailability(hasReadings, isElevated, isPawnIoInstalled));
    }

    [Fact]
    public void BuildSnapshot_MapsRows_DropsUnshownAndBlank_KeepsOrderAndNullValues()
    {
        var snapshot = LhmMapping.BuildSnapshot(
            [
                new LhmSensorRow("AMD Ryzen 9 5950X", "Core (Tctl/Tdie)", "Temperature", 61.5f),
                new LhmSensorRow("AMD Ryzen 9 5950X", "Fan Control #1", "Control", 40f), // unshown kind
                new LhmSensorRow("", "CPU Total", "Load", 12f), // blank hardware
                new LhmSensorRow("AMD Ryzen 9 5950X", " ", "Load", 12f), // blank name
                new LhmSensorRow("NVIDIA RTX 4080", "GPU Core", "Temperature", null), // sensor without a value
                new LhmSensorRow("NVIDIA RTX 4080", "GPU Fan", "Fan", 1180f),
            ],
            isElevated: true,
            isPawnIoInstalled: true);

        Assert.Equal(SensorAvailability.Available, snapshot.Availability);
        Assert.Equal(
            [
                new SensorReading("AMD Ryzen 9 5950X", "Core (Tctl/Tdie)", SensorKind.Temperature, 61.5f),
                new SensorReading("NVIDIA RTX 4080", "GPU Core", SensorKind.Temperature, null),
                new SensorReading("NVIDIA RTX 4080", "GPU Fan", SensorKind.Fan, 1180f),
            ],
            snapshot.Readings);
    }

    [Fact]
    public void BuildSnapshot_EmptyTree_ClassifiesFromFlags()
    {
        var snapshot = LhmMapping.BuildSnapshot([], isElevated: false, isPawnIoInstalled: false);

        Assert.Empty(snapshot.Readings);
        Assert.Equal(SensorAvailability.NoAdmin, snapshot.Availability);
    }

    [Fact]
    public void BuildSnapshot_OnlyUnshownRows_CountsAsEmpty()
    {
        var snapshot = LhmMapping.BuildSnapshot(
            [new LhmSensorRow("Nuvoton NCT6798D", "Fan Control #1", "Control", 40f)],
            isElevated: true,
            isPawnIoInstalled: false);

        Assert.Empty(snapshot.Readings);
        Assert.Equal(SensorAvailability.NoPawnIo, snapshot.Availability);
    }
}
