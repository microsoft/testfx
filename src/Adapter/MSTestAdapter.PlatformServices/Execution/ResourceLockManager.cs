// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// A per-run registry of named <see cref="AsyncReaderWriterLock"/> instances keyed by resource string (ordinal,
/// case-sensitive). It lets the parallel scheduler serialize only the chunks that declare conflicting
/// <c>[ResourceLock]</c> keys while unrelated chunks continue to run concurrently. Scope is the test host process.
/// </summary>
internal sealed class ResourceLockManager
{
    private readonly ConcurrentDictionary<string, AsyncReaderWriterLock> _locks = new(StringComparer.Ordinal);

    /// <summary>
    /// Acquires every lock in <paramref name="locks"/> (which the caller MUST have sorted ordinally by resource
    /// to guarantee a global, deadlock-free acquisition order), runs <paramref name="action"/>, then releases the
    /// locks in reverse order. If cancellation occurs while acquiring, any already-acquired locks are released.
    /// </summary>
    /// <param name="locks">The distinct, ordinally-sorted locks to hold for the duration of <paramref name="action"/>.</param>
    /// <param name="action">The work to perform while holding the locks.</param>
    /// <param name="cancellationToken">Token used to abandon a blocked acquisition.</param>
    public async Task ExecuteWithLocksAsync(IReadOnlyList<ResourceLockInfo> locks, Func<Task> action, CancellationToken cancellationToken)
    {
        var releasers = new List<IDisposable>(locks.Count);
        try
        {
            foreach (ResourceLockInfo resourceLock in locks)
            {
                AsyncReaderWriterLock namedLock = _locks.GetOrAdd(resourceLock.Resource, static _ => new AsyncReaderWriterLock());
                IDisposable releaser = resourceLock.Mode == ResourceAccessMode.Read
                    ? await namedLock.AcquireReaderAsync(cancellationToken).ConfigureAwait(false)
                    : await namedLock.AcquireWriterAsync(cancellationToken).ConfigureAwait(false);
                releasers.Add(releaser);
            }

            await action().ConfigureAwait(false);
        }
        finally
        {
            for (int i = releasers.Count - 1; i >= 0; i--)
            {
                releasers[i].Dispose();
            }
        }
    }

    /// <summary>
    /// Merges the resource locks declared across all elements of a scheduling chunk into a distinct set, keeping
    /// the strongest mode per key (<see cref="ResourceAccessMode.ReadWrite"/> wins over
    /// <see cref="ResourceAccessMode.Read"/>), sorted ordinally by resource so every chunk acquires locks in the
    /// same order.
    /// </summary>
    /// <param name="testSet">The elements that make up the chunk.</param>
    /// <returns>The distinct, ordinally-sorted locks for the chunk, or an empty list when none are declared.</returns>
    public static IReadOnlyList<ResourceLockInfo> GetChunkLocks(IEnumerable<UnitTestElement> testSet)
    {
        Dictionary<string, ResourceAccessMode>? map = null;
        foreach (UnitTestElement element in testSet)
        {
            if (element.ResourceLocks is not { Length: > 0 } elementLocks)
            {
                continue;
            }

            // StringComparer.Ordinal keeps key comparison consistent with the rest of the lock machinery.
#pragma warning disable IDE0028 // Collection initialization can be simplified - a comparer argument is required here.
            map ??= new Dictionary<string, ResourceAccessMode>(StringComparer.Ordinal);
#pragma warning restore IDE0028
            foreach (ResourceLockInfo resourceLock in elementLocks)
            {
                if (map.TryGetValue(resourceLock.Resource, out ResourceAccessMode existingMode))
                {
                    if (existingMode != ResourceAccessMode.ReadWrite && resourceLock.Mode == ResourceAccessMode.ReadWrite)
                    {
                        map[resourceLock.Resource] = ResourceAccessMode.ReadWrite;
                    }
                }
                else
                {
                    map[resourceLock.Resource] = resourceLock.Mode;
                }
            }
        }

        if (map is null)
        {
            return [];
        }

        var result = new List<ResourceLockInfo>(map.Count);
        foreach (KeyValuePair<string, ResourceAccessMode> entry in map)
        {
            result.Add(new ResourceLockInfo(entry.Key, entry.Value));
        }

        result.Sort(static (left, right) => string.CompareOrdinal(left.Resource, right.Resource));
        return result;
    }
}
