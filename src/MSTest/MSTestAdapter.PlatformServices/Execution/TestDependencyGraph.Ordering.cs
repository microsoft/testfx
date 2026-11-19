// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestDependencyGraph
{
    /// <summary>
    /// Orders <paramref name="indices"/> so that a test comes after the prerequisites that are themselves in
    /// the set, breaking ties by the original position so the result stays deterministic and as close to the
    /// declared order as the constraints allow. Any node left over by a cycle is appended in its original
    /// order rather than dropped, so a test can never disappear from the run.
    /// </summary>
    private static UnitTestElement[] OrderTopologically(List<int> indices, int[][] prerequisites, UnitTestElement[] tests)
    {
        if (indices.Count <= 1)
        {
            return indices.Count == 0 ? [] : [tests[indices[0]]];
        }

        var position = new Dictionary<int, int>(indices.Count);
        for (int i = 0; i < indices.Count; i++)
        {
            position[indices[i]] = i;
        }

        int[] remaining = new int[indices.Count];
        var dependents = new List<int>[indices.Count];
        for (int i = 0; i < indices.Count; i++)
        {
            foreach (int prerequisite in prerequisites[indices[i]])
            {
                if (position.TryGetValue(prerequisite, out int prerequisitePosition))
                {
                    remaining[i]++;
                    (dependents[prerequisitePosition] ??= []).Add(i);
                }
            }
        }

        // A sorted set keyed by original position makes the tie-break deterministic: among the tests that
        // are ready, the one declared first runs first.
        var ready = new SortedSet<int>();
        for (int i = 0; i < indices.Count; i++)
        {
            if (remaining[i] == 0)
            {
                ready.Add(i);
            }
        }

        var ordered = new List<UnitTestElement>(indices.Count);
        bool[] emitted = new bool[indices.Count];
        while (ready.Count > 0)
        {
            int current = ready.Min;
            ready.Remove(current);
            ordered.Add(tests[indices[current]]);
            emitted[current] = true;

            if (dependents[current] is not { } currentDependents)
            {
                continue;
            }

            foreach (int dependent in currentDependents)
            {
                if (--remaining[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        for (int i = 0; i < indices.Count; i++)
        {
            if (!emitted[i])
            {
                ordered.Add(tests[indices[i]]);
            }
        }

        return [.. ordered];
    }

    private static List<int> SelectIndices(int count, Func<int, bool> predicate)
    {
        var result = new List<int>();
        for (int i = 0; i < count; i++)
        {
            if (predicate(i))
            {
                result.Add(i);
            }
        }

        return result;
    }
}
