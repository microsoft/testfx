// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// The resolved <c>[DependsOn]</c> dependency graph for one test source: which test must run before which,
/// how the tests are grouped into scheduling chunks, and in what order a chunk's tests run.
/// </summary>
/// <remarks>
/// <para>
/// The graph is built once per source, before anything runs, from the dependencies carried on the
/// <see cref="UnitTestElement"/>s (see <see cref="UnitTestElement.Dependencies"/>) plus any edges declared
/// in a dependency chain file. It is a pure data structure: it neither runs tests nor observes outcomes -
/// <see cref="TestDependencyCoordinator"/> does that at run time.
/// </para>
/// <para>
/// Edges are resolved between <em>tests</em>, but MSTest schedules <em>chunks</em> (a whole class under
/// <see cref="ExecutionScope.ClassLevel"/>, a single test under <see cref="ExecutionScope.MethodLevel"/>).
/// The test-level graph is therefore projected onto chunks: an edge inside a chunk only orders the tests
/// within it, while an edge between chunks gates when the dependent chunk may start.
/// </para>
/// </remarks>
internal sealed class TestDependencyGraph
{
    private TestDependencyGraph(
        UnitTestElement[] tests,
        int[][] testPrerequisites,
        bool[] proceedOnFailure,
        UnitTestElement[][] parallelChunks,
        int[][] parallelChunkPrerequisites,
        UnitTestElement[] sequentialTests,
        IReadOnlyList<UnitTestElement> brokenTests,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings)
    {
        Tests = tests;
        TestPrerequisites = testPrerequisites;
        ProceedOnFailure = proceedOnFailure;
        ParallelChunks = parallelChunks;
        ParallelChunkPrerequisites = parallelChunkPrerequisites;
        SequentialTests = sequentialTests;
        BrokenTests = brokenTests;
        Errors = errors;
        Warnings = warnings;
    }

    /// <summary>Gets every test of the source, in the order the graph indexes them.</summary>
    public UnitTestElement[] Tests { get; }

    /// <summary>
    /// Gets, for each test, the indices into <see cref="Tests"/> of the tests that must run first.
    /// </summary>
    public int[][] TestPrerequisites { get; }

    /// <summary>
    /// Gets, for each test, whether it runs even when a prerequisite did not pass. A test with several
    /// prerequisites has a single value because the flag is merged per dependent when the edges are
    /// resolved: it is set only when every declared edge asked to proceed.
    /// </summary>
    public bool[] ProceedOnFailure { get; }

    /// <summary>
    /// Gets the scheduling chunks of the parallel phase. The tests inside a chunk are already ordered so
    /// that intra-chunk prerequisites come first.
    /// </summary>
    public UnitTestElement[][] ParallelChunks { get; }

    /// <summary>
    /// Gets, for each parallel chunk, the indices of the chunks that must complete before it may start.
    /// </summary>
    public int[][] ParallelChunkPrerequisites { get; }

    /// <summary>
    /// Gets the tests of the sequential phase (those marked <c>[DoNotParallelize]</c>, plus those pulled in
    /// by the demotion rule), ordered so that prerequisites come first.
    /// </summary>
    public UnitTestElement[] SequentialTests { get; }

    /// <summary>
    /// Gets the tests that take part in a dependency cycle. They cannot be ordered, so they are reported as
    /// failed instead of being run; their dependents are then skipped by the ordinary "a prerequisite did
    /// not pass" rule.
    /// </summary>
    public IReadOnlyList<UnitTestElement> BrokenTests { get; }

    /// <summary>Gets the configuration errors to report (currently: the dependency cycles that were found).</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>Gets the non-fatal problems to report, such as dependencies that match no test in this run.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Builds the graph for <paramref name="tests"/>, or returns <see langword="null"/> when no test declares
    /// a dependency, in which case the caller keeps its ordinary scheduling path. The null fast path matters:
    /// it keeps every run that does not use the feature on exactly the code it uses today.
    /// </summary>
    /// <param name="tests">The tests of one source, after filtering.</param>
    /// <param name="scope">The effective parallelization scope, which decides what a chunk is.</param>
    /// <param name="parallelizationEnabled">
    /// Whether the parallel phase exists at all. When it does not, every test is placed in the sequential
    /// phase, so a declared order is still honored when parallelization is turned off.
    /// </param>
    public static TestDependencyGraph? Build(UnitTestElement[] tests, ExecutionScope scope, bool parallelizationEnabled)
    {
        bool anyDependency = false;
        foreach (UnitTestElement test in tests)
        {
            if (test.Dependencies is { Length: > 0 })
            {
                anyDependency = true;
                break;
            }
        }

        if (!anyDependency)
        {
            return null;
        }

        var warnings = new List<string>();
        var errors = new List<string>();

        (int[][] testPrerequisites, bool[] proceedOnFailure) = ResolveEdges(tests, warnings);

        bool[] isBroken = FindCycles(testPrerequisites, tests, errors);

        // A test in a cycle cannot be ordered, so it is dropped from scheduling and reported as failed. Its
        // dependents keep the edge: at run time the prerequisite is recorded as "did not pass", so they are
        // skipped with the ordinary message rather than silently running out of order.
        var brokenTests = new List<UnitTestElement>();
        for (int i = 0; i < tests.Length; i++)
        {
            if (isBroken[i])
            {
                brokenTests.Add(tests[i]);
            }
        }

        bool[] isSequential = ComputeSequentialSet(tests, testPrerequisites, isBroken, parallelizationEnabled);

        UnitTestElement[] sequentialTests = OrderTopologically(
            SelectIndices(tests.Length, i => isSequential[i] && !isBroken[i]),
            testPrerequisites,
            tests);

        (UnitTestElement[][] parallelChunks, int[][] parallelChunkPrerequisites) = BuildChunks(
            tests,
            testPrerequisites,
            SelectIndices(tests.Length, i => !isSequential[i] && !isBroken[i]),
            scope,
            errors);

        return new TestDependencyGraph(
            tests,
            testPrerequisites,
            proceedOnFailure,
            parallelChunks,
            parallelChunkPrerequisites,
            sequentialTests,
            brokenTests,
            errors,
            warnings);
    }

    /// <summary>
    /// Resolves every declared dependency into edges between test indices. A dependency that names a class
    /// but no method matches every test of that class; one that names no class is looked up in the
    /// dependent's own class. A dependency that matches no test in this run is reported as a warning and
    /// dropped, so that running a subset of the suite (for example with a filter, or a single test from an
    /// IDE) stays possible.
    /// </summary>
    private static (int[][] Prerequisites, bool[] ProceedOnFailure) ResolveEdges(UnitTestElement[] tests, List<string> warnings)
    {
        var byClass = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var byClassAndMethod = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < tests.Length; i++)
        {
            TestMethod testMethod = tests[i].TestMethod;
            AddToIndex(byClass, testMethod.FullClassName, i);
            AddToIndex(byClassAndMethod, MakeKey(testMethod.FullClassName, testMethod.Name), i);
        }

        int[][] prerequisites = new int[tests.Length][];
        bool[] proceedOnFailure = new bool[tests.Length];
        var resolved = new List<int>();
        var seen = new HashSet<int>();

        for (int i = 0; i < tests.Length; i++)
        {
            if (tests[i].Dependencies is not { Length: > 0 } dependencies)
            {
                prerequisites[i] = [];
                continue;
            }

            resolved.Clear();
            seen.Clear();

            // A dependent proceeds past a failed prerequisite only when every edge it declares says so:
            // one edge that wants the ordinary skip is enough to hold it back.
            bool allProceed = true;

            foreach (TestDependencyInfo dependency in dependencies)
            {
                string targetClass = dependency.TargetClassFullName ?? tests[i].TestMethod.FullClassName;
                List<int>? matches;
                if (dependency.TargetMethodName is null)
                {
                    byClass.TryGetValue(targetClass, out matches);
                }
                else
                {
                    byClassAndMethod.TryGetValue(MakeKey(targetClass, dependency.TargetMethodName), out matches);
                }

                int added = 0;
                if (matches is not null)
                {
                    foreach (int match in matches)
                    {
                        // A whole-class dependency naturally includes the dependent itself when it lives in
                        // that class; that is not a cycle the user wrote, so drop it. An explicit self
                        // reference by name is kept, so it surfaces as a cycle.
                        if (match == i && dependency.TargetMethodName is null)
                        {
                            continue;
                        }

                        added++;
                        if (seen.Add(match))
                        {
                            resolved.Add(match);
                        }
                    }
                }

                if (added == 0)
                {
                    warnings.Add(string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.DependsOnTargetNotFound,
                        tests[i].TestMethod.FullyQualifiedName,
                        dependency.DescribeTarget()));
                    continue;
                }

                allProceed &= dependency.ProceedOnFailure;
            }

            prerequisites[i] = [.. resolved];
            proceedOnFailure[i] = resolved.Count > 0 && allProceed;
        }

        return (prerequisites, proceedOnFailure);

        static void AddToIndex(Dictionary<string, List<int>> index, string key, int value)
        {
            if (!index.TryGetValue(key, out List<int>? list))
            {
                index[key] = list = [];
            }

            list.Add(value);
        }

        static string MakeKey(string className, string methodName) => className + "." + methodName;
    }

    /// <summary>
    /// Finds every dependency cycle with an iterative three-colour depth-first search and describes each one
    /// as a path (<c>A &gt; B &gt; A</c>). Nodes already known to be on a cycle are treated as finished so a
    /// second cycle through them cannot restart the search indefinitely.
    /// </summary>
    private static bool[] FindCycles(int[][] prerequisites, UnitTestElement[] tests, List<string> errors)
    {
        const byte White = 0, Grey = 1, Black = 2;
        byte[] colour = new byte[prerequisites.Length];
        bool[] isBroken = new bool[prerequisites.Length];
        var path = new List<int>();
        int[] edgeCursor = new int[prerequisites.Length];

        for (int start = 0; start < prerequisites.Length; start++)
        {
            if (colour[start] != White)
            {
                continue;
            }

            path.Clear();
            path.Add(start);
            colour[start] = Grey;
            edgeCursor[start] = 0;

            while (path.Count > 0)
            {
                int current = path[path.Count - 1];
                int[] currentPrerequisites = prerequisites[current];

                if (edgeCursor[current] < currentPrerequisites.Length)
                {
                    int next = currentPrerequisites[edgeCursor[current]++];
                    if (colour[next] == Grey)
                    {
                        // 'next' is on the current path, so the path from it to 'current' is a cycle.
                        int cycleStart = path.LastIndexOf(next);
                        var cycle = new List<string>();
                        for (int i = cycleStart; i < path.Count; i++)
                        {
                            isBroken[path[i]] = true;
                            cycle.Add(tests[path[i]].TestMethod.FullyQualifiedName);
                        }

                        cycle.Add(tests[next].TestMethod.FullyQualifiedName);
                        errors.Add(string.Format(CultureInfo.CurrentCulture, Resource.DependsOnCycle, string.Join(" > ", cycle)));
                    }
                    else if (colour[next] == White)
                    {
                        colour[next] = Grey;
                        edgeCursor[next] = 0;
                        path.Add(next);
                    }

                    continue;
                }

                colour[current] = Black;
                path.RemoveAt(path.Count - 1);
            }
        }

        return isBroken;
    }

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
            for (int i = 0; i < isSequential.Length; i++)
            {
                isSequential[i] = true;
            }

            return isSequential;
        }

        // dependents[p] lists the tests that wait for p, so the demotion can be pushed forward along edges.
        var dependents = new List<int>[tests.Length];
        for (int i = 0; i < tests.Length; i++)
        {
            foreach (int prerequisite in prerequisites[i])
            {
                (dependents[prerequisite] ??= []).Add(i);
            }
        }

        var queue = new Queue<int>();
        for (int i = 0; i < tests.Length; i++)
        {
            if (tests[i].DoNotParallelize && !isBroken[i])
            {
                isSequential[i] = true;
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

        return isSequential;
    }

    /// <summary>
    /// Groups the parallel-phase tests into scheduling chunks and projects the test-level edges onto them.
    /// A cycle that exists only in the projection (class A waits for class B and B waits for A, although no
    /// single test waits for itself) is reported with the advice to switch to
    /// <see cref="ExecutionScope.MethodLevel"/>, where each test is its own chunk and the projection cannot
    /// introduce a cycle. The chunk order then falls back to the declaration order, and the test-level
    /// prerequisites still hold every dependent back at run time, so nothing runs out of order.
    /// </summary>
    private static (UnitTestElement[][] Chunks, int[][] ChunkPrerequisites) BuildChunks(
        UnitTestElement[] tests,
        int[][] prerequisites,
        List<int> parallelIndices,
        ExecutionScope scope,
        List<string> errors)
    {
        int[] chunkOfTest = new int[tests.Length];
        for (int i = 0; i < chunkOfTest.Length; i++)
        {
            chunkOfTest[i] = -1;
        }

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

        if (HasChunkCycle(chunkPrerequisiteArrays, out int[]? cycleChunks))
        {
            var classNames = new List<string>();
            foreach (int chunk in cycleChunks!)
            {
                classNames.Add(tests[chunkMembers[chunk][0]].TestMethod.FullClassName);
            }

            errors.Add(string.Format(CultureInfo.CurrentCulture, Resource.DependsOnClassLevelCycle, string.Join(", ", classNames)));

            // Drop the projected edges rather than the tests: the test-level graph is sound, so the run can
            // still honour it through the run-time gate reported above.
            for (int i = 0; i < chunkPrerequisiteArrays.Length; i++)
            {
                chunkPrerequisiteArrays[i] = [];
            }
        }

        var chunks = new UnitTestElement[chunkMembers.Count][];
        for (int i = 0; i < chunkMembers.Count; i++)
        {
            chunks[i] = OrderTopologically(chunkMembers[i], prerequisites, tests);
        }

        return (chunks, chunkPrerequisiteArrays);
    }

    /// <summary>
    /// Reports whether the chunk graph contains a cycle, and if so which chunks take part in it.
    /// </summary>
    private static bool HasChunkCycle(int[][] chunkPrerequisites, out int[]? cycleChunks)
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
            cycleChunks = null;
            return false;
        }

        // Whatever Kahn's algorithm could not settle is exactly the part of the graph held up by a cycle.
        var unsettled = new List<int>();
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] > 0)
            {
                unsettled.Add(i);
            }
        }

        cycleChunks = [.. unsettled];
        return true;
    }

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
