using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>Produces the raw per-process rows for one sampling tick.</summary>
public interface IProcessEnumerator
{
    public IReadOnlyList<ProcessSample> Enumerate();
}
