namespace TaskLens.Core.Services;

/// <summary>Wall clock abstraction so delta math is deterministic in tests (ManualClock).</summary>
public interface IClock
{
    public DateTime UtcNow { get; }
}

/// <summary>Production clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
