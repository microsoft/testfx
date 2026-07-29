// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class TestDependencyGraph
{
    /// <summary>
    /// Groups the parallel-phase tests into scheduling chunks and projects the test-level edges onto them.
    /// A cycle that exists only in the projection (class A waits for class B and B waits for A, although no
    /// single test waits for itself) is reported through <paramref name="projectionCycleTests"/> so the caller
    /// can move those tests into the sequential phase, where the test-level order is honoured exactly.
    /// </summary>
    private static (UnitTestElement[][] Chunks, int[][] ChunkPrerequisites) BuildChunks(
        UnitTestElement[] tests,
        int[][] prerequisites,
        List<int> parallelIndices,
        ExecutionScope scope,
        List<string> warnings,
        out List<int>? projectionCycleTests)
    {
        projectionCycleTests = null;

        // Tests not scheduled in the parallel phase keep -1: the edge projection below relies on that to skip them.
        int[] chunkOfTest = new int[tests.Length];
        chunkOfTest.AsSpan().Fill(-1);

        var chunkMembers = new List<List<int>>();
        if (scope == ExecutionScope.ClassLevel)
        {
            var chunkByClass = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (int index in parallelIndices)
            {
                string className = tests[index].TestMethod.FullClassName;
                if (!chunkByClass.TryGetValue(className, out int chunk))
                {
                    chunkByClass[className] = chunk = chunkMembers.Count;
                    chunkMembers.Add([]);
                }

                chunkOfTest[index] = chunk;
                chunkMembers[chunk].Add(index);
            }
        }
        else
        {
            foreach (int index in parallelIndices)
            {
                chunkOfTest[index] = chunkMembers.Count;
                chunkMembers.Add([index]);
            }
        }

        var chunkPrerequisites = new HashSet<int>[chunkMembers.Count];
        for (int i = 0; i < chunkPrerequisites.Length; i++)
        {
            chunkPrerequisites[i] = [];
        }

        foreach (int index in parallelIndices)
        {
            foreach (int prerequisite in prerequisites[index])
            {
                int prerequisiteChunk = chunkOfTest[prerequisite];

                // Edges to a test that is not scheduled in the parallel phase (it is sequential, or broken)
                // do not gate a chunk: the sequential phase runs later, and a broken test never runs. The
                // run-time outcome check still applies to the dependent.
                if (prerequisiteChunk >= 0 && prerequisiteChunk != chunkOfTest[index])
                {
                    chunkPrerequisites[chunkOfTest[index]].Add(prerequisiteChunk);
                }
            }
        }

        int[][] chunkPrerequisiteArrays = new int[chunkMembers.Count][];
        for (int i = 0; i < chunkMembers.Count; i++)
        {
            chunkPrerequisiteArrays[i] = [.. chunkPrerequisites[i]];
        }

        if (HasChunkCycle(chunkPrerequisiteArrays, out int[]? blockedChunks, out int[]? cycleChunks))
        {
            // Everything blocked has to leave the parallel phase - a chunk merely downstream of the cycle
            // cannot be scheduled there either - but only the chunks genuinely in the cycle may be named as
            // depending on each other, which is all the message claims.
            projectionCycleTests = [];
            foreach (int chunk in blockedChunks)
            {
                projectionCycleTests.AddRange(chunkMembers[chunk]);
            }

            var classNames = new List<string>();
            foreach (int chunk in cycleChunks)
            {
                classNames.Add(tests[chunkMembers[chunk][0]].TestMethod.FullClassName);
            }

            warnings.Add(string.Format(CultureInfo.CurrentCulture, Resource.DependsOnClassLevelCycle, string.Join(", ", classNames)));
        }

        // When a projection cycle was found, the caller re-chunks without the tests reported through
        // 'projectionCycleTests', so what is built here is discarded anyway. Returning it unchanged rather
        // than something partially adjusted keeps this method free of a half-valid state.
        return (BuildChunkArrays(chunkMembers, prerequisites, tests), chunkPrerequisiteArrays);
    }

    private static UnitTestElement[][] BuildChunkArrays(List<List<int>> chunkMembers, int[][] prerequisites, UnitTestElement[] tests)
    {
        var chunks = new UnitTestElement[chunkMembers.Count][];
        for (int i = 0; i < chunkMembers.Count; i++)
        {
            chunks[i] = OrderTopologically(chunkMembers[i], prerequisites, tests);
        }

        return chunks;
    }
}
