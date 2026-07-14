using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>
/// Enables/disables autostart entries (plan-tm3 tm3-06). Implemented next to the
/// <see cref="IStartupItemSource"/> that produced the item — the item's
/// <see cref="StartupItem.ToggleId"/> is the contract between the two.
/// </summary>
public interface IStartupManager
{
    /// <summary>Sets the entry's StartupApproved state; entries without a ToggleId fail as data.</summary>
    public ActionResult SetEnabled(StartupItem item, bool enabled);
}
