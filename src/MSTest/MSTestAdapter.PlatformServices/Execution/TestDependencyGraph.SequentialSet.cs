// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestDependencyGraph
{
    /// <summary>
    /// Decides which tests run in the sequential phase. That is every <c>[DoNotParallelize]</c> test, plus -
    /// transitively - everything that depends on one: because the sequential phase runs after the parallel
    /// phase, a parallel test that waited for a sequential prerequisite would never see it complete. Moving
    /// the dependent instead of moving the prerequisite keeps <c>[DoNotParallelize]</c>'s own guarantee
    /// (that such tests never run alongside anything) intact.
    /// </summary>
    private static bool[] ComputeSequentialSet(UnitTestElement[] tests, int[][] prerequisites, bool[] isBroken, bool parallelizationEnabled)
    {
        bool[] isSequential = new bool[tests.Length];
        if (!parallelizationEnabled)
        {
            isSequential.AsSpan().Fill(true);
            return isSequential;
        }

        for (int i = 0; i < tests.Length; i++)
        {
            if (tests[i].DoNotParallelize && !isBroken[i])
            {
                isSequential[i] = true;
            }
        }

        PropagateSequential(tests.Length, prerequisites, isBroken, isSequential);
        return isSequential;
    }

    /// <summary>
    /// Extends <paramref name="isSequential"/> to everything that (transitively) waits on a test already in
    /// it, because the sequential phase runs after the parallel one: a parallel test waiting on a sequential
    /// prerequisite would never observe it complete.
    /// </summary>
    private static void PropagateSequential(int testCount, int[][] prerequisites, bool[] isBroken, bool[] isSequential)
    {
        // dependents[p] lists the tests that wait for p, so the demotion can be pushed forward along edges.
        var dependents = new List<int>[testCount];
        for (int i = 0; i < testCount; i++)
        {
            foreach (int prerequisite in prerequisites[i])
            {
                (dependents[prerequisite] ??= []).Add(i);
            }
        }

        var queue = new Queue<int>();
        for (int i = 0; i < testCount; i++)
        {
            if (isSequential[i])
            {
                queue.Enqueue(i);
            }
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            if (dependents[current] is not { } currentDependents)
            {
                continue;
            }

            foreach (int dependent in currentDependents)
            {
                if (!isSequential[dependent] && !isBroken[dependent])
                {
                    isSequential[dependent] = true;
                    queue.Enqueue(dependent);
                }
            }
        }
    }
}
