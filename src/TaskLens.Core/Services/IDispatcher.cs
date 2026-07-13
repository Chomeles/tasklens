namespace TaskLens.Core.Services;

/// <summary>
/// Marshals an action onto the UI thread. The WinUI implementation wraps
/// <c>DispatcherQueue.TryEnqueue</c>; tests use a synchronous implementation.
/// </summary>
public interface IDispatcher
{
    void Post(Action action);
}
