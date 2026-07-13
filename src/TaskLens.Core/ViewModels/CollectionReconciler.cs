using System.Collections.ObjectModel;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// Shared list-reconcile helper: mutates an <see cref="ObservableCollection{T}"/> into a target
/// order with minimal remove/insert/move events (no reset), so surviving rows keep their
/// containers and per-row state in the view. Identity is reference equality.
/// </summary>
internal static class CollectionReconciler
{
    // ponytail: IndexOf makes this O(n²) worst case; fine for hundreds of rows,
    // switch to an index map if profiling ever says otherwise.
    internal static void Reconcile<T>(ObservableCollection<T> rows, List<T> target)
        where T : class
    {
        var wanted = new HashSet<T>(target);
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (!wanted.Contains(rows[i]))
            {
                rows.RemoveAt(i);
            }
        }

        for (var i = 0; i < target.Count; i++)
        {
            if (i < rows.Count && ReferenceEquals(rows[i], target[i]))
            {
                continue;
            }

            var j = rows.IndexOf(target[i]);
            if (j < 0)
            {
                rows.Insert(i, target[i]);
            }
            else
            {
                rows.Move(j, i);
            }
        }
    }
}
