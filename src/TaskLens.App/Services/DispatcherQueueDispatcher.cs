using Microsoft.UI.Dispatching;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

/// <summary>Marshals engine callbacks onto the UI thread via <see cref="DispatcherQueue"/>.</summary>
public sealed class DispatcherQueueDispatcher(DispatcherQueue queue) : IDispatcher
{
    private readonly DispatcherQueue queue = queue ?? throw new ArgumentNullException(nameof(queue));

    public void Post(Action action) => queue.TryEnqueue(() => action());
}
