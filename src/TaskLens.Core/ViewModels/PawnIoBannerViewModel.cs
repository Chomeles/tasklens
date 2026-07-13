using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// First-run banner shown once at app startup when the PawnIO driver is missing, with an
/// install hint. Distinct from <see cref="SensorsViewModel"/>'s per-tick degradation banner:
/// this one is a cheap up-front check (<see cref="Services.PawnIoDetector"/>) so the user gets
/// pointed at the fix immediately, before any sampling happens.
/// </summary>
public sealed partial class PawnIoBannerViewModel : ObservableObject
{
    public const string InstallHintUrl = "https://github.com/namazso/PawnIO";

    public PawnIoBannerViewModel(bool isPawnIoInstalled)
    {
        IsPawnIoInstalled = isPawnIoInstalled;
    }

    public bool IsPawnIoInstalled { get; }

    /// <summary>True when the banner should be shown (PawnIO missing).</summary>
    public bool ShowBanner => !IsPawnIoInstalled;

    /// <summary>Install hint text; empty when PawnIO is present.</summary>
    public string BannerText => ShowBanner
        ? $"PawnIO is not installed. Hardware temperature, power and fan sensors need it — install it from {InstallHintUrl}."
        : "";
}
