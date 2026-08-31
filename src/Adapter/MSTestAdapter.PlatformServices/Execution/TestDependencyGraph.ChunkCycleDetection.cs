// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestDependencyGraph
{
    /// <summary>
    /// Reports whether the chunk graph contains a cycle. Kahn's algorithm cannot settle a node that is on a
    /// cycle <em>or</em> merely downstream of one, so the two are separated: <paramref name="blockedChunks"/>
    /// is everything that cannot be scheduled (all of it has to leave the parallel phase), while
    /// <paramref name="cycleChunks"/> is the subset genuinely in a cycle, which is what the diagnostic may
    /// claim depends on each other.
    /// </summary>
    private static bool HasChunkCycle(int[][] chunkPrerequisites, [NotNullWhen(true)] out int[]? blockedChunks, [NotNullWhen(true)] out int[]? cycleChunks)
    {
        int[] remaining = new int[chunkPrerequisites.Length];
        var dependents = new List<int>[chunkPrerequisites.Length];
        for (int i = 0; i < chunkPrerequisites.Length; i++)
        {
            remaining[i] = chunkPrerequisites[i].Length;
            foreach (int prerequisite in chunkPrerequisites[i])
            {
                (dependents[prerequisite] ??= []).Add(i);
            }
        }

        var queue = new Queue<int>();
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] == 0)
            {
                queue.Enqueue(i);
            }
        }

        int settled = 0;
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            settled++;
            if (dependents[current] is not { } currentDependents)
            {
                continue;
            }

            foreach (int dependent in currentDependents)
            {
                if (--remaining[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        if (settled == chunkPrerequisites.Length)
        {
            blockedChunks = null;
            cycleChunks = null;
            return false;
        }

        var blocked = new List<int>();
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] > 0)
            {
                blocked.Add(i);
            }
        }

        blockedChunks = [.. blocked];
        cycleChunks = FindChunksOnCycle(chunkPrerequisites, blocked);
        return true;
    }

    /// <summary>
    /// Narrows the blocked chunks down to those actually on a cycle, using the same
    /// <see cref="ForEachCyclicComponent"/> pass as <see cref="FindCycles"/>. Peeling chunks that no other
    /// blocked chunk depends on is not sufficient - a chunk sitting on a one-way path from one cycle to
    /// another has a blocked dependent, so it would survive the peel and be named as mutually dependent when
    /// it is only downstream of one cycle and upstream of the other.
    /// </summary>
    private static int[] FindChunksOnCycle(int[][] chunkPrerequisites, List<int> blocked)
    {
        bool[] isOnCycle = new bool[chunkPrerequisites.Length];

        ForEachCyclicComponent(chunkPrerequisites, (component, _) =>
        {
            foreach (int node in component)
            {
                isOnCycle[node] = true;
            }
        });

        // Walking 'blocked' rather than the components keeps the reported order stable and guarantees the
        // result is a subset of the blocked set, which is what the caller's warning claims.
        var onCycle = new List<int>();
        foreach (int chunk in blocked)
        {
            if (isOnCycle[chunk])
            {
                onCycle.Add(chunk);
            }
        }

        return [.. onCycle];
    }
}
