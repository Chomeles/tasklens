using System.Collections;

namespace TaskLens.Core.Models;

/// <summary>
/// Fixed-capacity ring buffer for per-tick history (sparklines, detail graphs).
/// Once full, <see cref="Add"/> overwrites the oldest item. The indexer and
/// enumeration are oldest-first. Not thread-safe — callers synchronize.
/// </summary>
public sealed class HistoryBuffer<T> : IReadOnlyList<T>
{
    private readonly T[] items;
    private int next; // slot the next Add writes to

    public HistoryBuffer(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be >= 1.");
        }

        items = new T[capacity];
    }

    public int Capacity => items.Length;

    public int Count { get; private set; }

    /// <summary>Index 0 is the oldest retained item.</summary>
    public T this[int index] =>
        index >= 0 && index < Count
            ? items[(next - Count + index + items.Length) % items.Length]
            : throw new ArgumentOutOfRangeException(nameof(index), index, "index must be within [0, Count).");

    public void Add(T item)
    {
        items[next] = item;
        next = (next + 1) % items.Length;
        if (Count < items.Length)
        {
            Count++;
        }
    }

    /// <summary>Copies the retained items, oldest first.</summary>
    public T[] ToArray()
    {
        var result = new T[Count];
        for (var i = 0; i < Count; i++)
        {
            result[i] = this[i];
        }

        return result;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return this[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
