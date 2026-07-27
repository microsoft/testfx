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
/// <see cref="UnitTestElement"/>s (see <see cref="UnitTestElement.Dependencies"/>), which come from
/// <c>[DependsOn]</c> attributes and/or <c>testconfig.json</c>. It is a pure data structure: it neither
/// runs tests nor observes outcomes - <see cref="TestDependencyCoordinator"/> does that at run time.
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
        IReadOnlyList<BrokenTest> brokenTests,
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
    /// Gets the tests that take part in a dependency cycle, each paired with the description of the cycle
    /// <em>it</em> is in. They cannot be ordered, so they are reported as failed instead of being run; their
    /// dependents are then skipped by the ordinary "a prerequisite did not pass" rule.
    /// </summary>
    public IReadOnlyList<BrokenTest> BrokenTests { get; }

    /// <summary>
    /// A test that cannot be scheduled because it takes part in a dependency cycle, together with the
    /// description of that cycle. The message is per test rather than per run so that a suite containing two
    /// unrelated cycles reports each failure against its own cycle instead of every cycle in the assembly.
    /// </summary>
    internal sealed class BrokenTest
    {
        public BrokenTest(UnitTestElement element, string cycleMessage)
        {
            Element = element;
            CycleMessage = cycleMessage;
        }

        public UnitTestElement Element { get; }

        public string CycleMessage { get; }
    }

    /// <summary>
    /// Gets the fatal configuration errors: the dependency cycles that make tests unschedulable. These are
    /// reported as failures against the tests in the cycle, so nothing that merely degrades - such as a cycle
    /// in the class-level projection, which is recovered from by running those tests sequentially - belongs
    /// here; that goes to <see cref="Warnings"/>.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets the non-fatal problems to report: dependencies that match no test in this run, and cycles in the
    /// class-level projection that were recovered from by demoting the affected tests to the sequential phase.
    /// </summary>
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

        string?[] cycleMessageByTest = FindCycles(testPrerequisites, tests, errors);
        bool[] isBroken = new bool[tests.Length];
        for (int i = 0; i < tests.Length; i++)
        {
            isBroken[i] = cycleMessageByTest[i] is not null;
        }

        // A test in a cycle cannot be ordered, so it is dropped from scheduling and reported as failed, with
        // the description of the cycle *it* is in. Its dependents keep the edge: at run time the prerequisite
        // is recorded as "did not pass", so they are skipped with the ordinary message rather than silently
        // running out of order.
        var brokenTests = new List<BrokenTest>();
        for (int i = 0; i < tests.Length; i++)
        {
            if (cycleMessageByTest[i] is { } cycleMessage)
            {
                brokenTests.Add(new BrokenTest(tests[i], cycleMessage));
            }
        }

        bool[] isSequential = ComputeSequentialSet(tests, testPrerequisites, isBroken, parallelizationEnabled);

        (UnitTestElement[][] parallelChunks, int[][] parallelChunkPrerequisites) = BuildChunks(
            tests,
            testPrerequisites,
            SelectIndices(tests.Length, i => !isSequential[i] && !isBroken[i]),
            scope,
            warnings,
            out List<int>? projectionCycleTests);

        // A cycle that exists only in the class-level projection means the declared order is satisfiable
        // between tests but not between whole classes. Rather than leave those tests unordered - which would
        // make the run-time gate skip them nondeterministically, because a prerequisite that has not run yet
        // is indistinguishable from one that failed - the affected tests are moved into the sequential phase,
        // where the topological order can be honoured exactly. The cost is parallelism for those tests only,
        // and the reported error tells the user how to get it back.
        if (projectionCycleTests is not null)
        {
            foreach (int index in projectionCycleTests)
            {
                isSequential[index] = true;
            }

            // Anything that waited on a demoted test has to move too, for the same reason [DoNotParallelize]
            // dependents do: the sequential phase runs after the parallel one.
            PropagateSequential(tests.Length, testPrerequisites, isBroken, isSequential);

            (parallelChunks, parallelChunkPrerequisites) = BuildChunks(
                tests,
                testPrerequisites,
                SelectIndices(tests.Length, i => !isSequential[i] && !isBroken[i]),
                scope,
                warnings,
                out _);
        }

        UnitTestElement[] sequentialTests = OrderTopologically(
            SelectIndices(tests.Length, i => isSequential[i] && !isBroken[i]),
            testPrerequisites,
            tests);

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
    /// Finds every test that takes part in a dependency cycle, and describes each cycle as a path
    /// (<c>A &gt; B &gt; A</c>). Cycle membership is computed with Tarjan's strongly-connected-components
    /// algorithm rather than by collecting back-edge paths from a plain depth-first search: a DFS reports the
    /// path it happened to close, so with overlapping cycles (A depends on B and C, B on A, C on B) it finds
    /// <c>A &gt; B &gt; A</c>, marks B finished, and never notices that C is in a cycle too. Every member of a
    /// component with a cycle has to be found, because each one is unschedulable.
    /// </summary>
    private static string?[] FindCycles(int[][] prerequisites, UnitTestElement[] tests, List<string> errors)
    {
        string?[] cycleMessageByTest = new string?[prerequisites.Length];
        int[] index = new int[prerequisites.Length];
        int[] lowLink = new int[prerequisites.Length];
        bool[] onStack = new bool[prerequisites.Length];
        int[] edgeCursor = new int[prerequisites.Length];
        for (int i = 0; i < index.Length; i++)
        {
            index[i] = -1;
        }

        var stack = new List<int>();
        var callStack = new List<int>();
        int nextIndex = 0;

        for (int start = 0; start < prerequisites.Length; start++)
        {
            if (index[start] != -1)
            {
                continue;
            }

            // Iterative Tarjan: the recursive form would stack-overflow on a long dependency chain.
            callStack.Add(start);
            index[start] = lowLink[start] = nextIndex++;
            edgeCursor[start] = 0;
            stack.Add(start);
            onStack[start] = true;

            while (callStack.Count > 0)
            {
                int current = callStack[callStack.Count - 1];
                if (edgeCursor[current] < prerequisites[current].Length)
                {
                    int next = prerequisites[current][edgeCursor[current]++];
                    if (index[next] == -1)
                    {
                        index[next] = lowLink[next] = nextIndex++;
                        edgeCursor[next] = 0;
                        stack.Add(next);
                        onStack[next] = true;
                        callStack.Add(next);
                    }
                    else if (onStack[next])
                    {
                        lowLink[current] = Math.Min(lowLink[current], index[next]);
                    }

                    continue;
                }

                callStack.RemoveAt(callStack.Count - 1);
                if (callStack.Count > 0)
                {
                    int parent = callStack[callStack.Count - 1];
                    lowLink[parent] = Math.Min(lowLink[parent], lowLink[current]);
                }

                if (lowLink[current] != index[current])
                {
                    continue;
                }

                // 'current' roots a strongly connected component: pop it off the stack.
                var component = new List<int>();
                int member;
                do
                {
                    member = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    onStack[member] = false;
                    component.Add(member);
                }
                while (member != current);

                // A component of one is only a cycle when the test names itself; anything larger always is.
                bool isCycle = component.Count > 1 || Array.IndexOf(prerequisites[current], current) >= 0;
                if (!isCycle)
                {
                    continue;
                }

                // A component is a *set*, not a path: rendering its members in any order would claim edges
                // that were never declared (with A depending on B and C, B on A and C on B, "A > B > C > A"
                // asserts a B -> C edge that does not exist, and tells the user to remove it). Walk real
                // edges inside the component instead, so every arrow in the message is one the user wrote.
                int[] cycleNodes = FindCyclePathWithin(prerequisites, component, current);
                var cycle = new List<string>(cycleNodes.Length + 1);
                foreach (int node in cycleNodes)
                {
                    cycle.Add(tests[node].TestMethod.FullyQualifiedName);
                }

                cycle.Add(tests[cycleNodes[0]].TestMethod.FullyQualifiedName);

                string message = string.Format(CultureInfo.CurrentCulture, Resource.DependsOnCycle, string.Join(" > ", cycle));
                errors.Add(message);
                foreach (int node in component)
                {
                    cycleMessageByTest[node] = message;
                }
            }
        }

        return cycleMessageByTest;
    }

    /// <summary>
    /// Returns a genuine closed walk through <paramref name="component"/> starting at <paramref name="root"/>,
    /// following only declared prerequisite edges that stay inside the component. A breadth-first search finds
    /// the shortest such walk, which keeps the reported path as small as the graph allows. The component is
    /// strongly connected, so a walk back to the root always exists.
    /// </summary>
    private static int[] FindCyclePathWithin(int[][] prerequisites, List<int> component, int root)
    {
        var inComponent = new HashSet<int>(component);

        // Self-reference: the whole cycle is the one node.
        if (Array.IndexOf(prerequisites[root], root) >= 0)
        {
            return [root];
        }

        var parent = new Dictionary<int, int>();
        var queue = new Queue<int>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int prerequisite in prerequisites[current])
            {
                if (!inComponent.Contains(prerequisite))
                {
                    continue;
                }

                if (prerequisite == root)
                {
                    // Walk the parents back to the root, then reverse so the path reads root-first.
                    var path = new List<int> { current };
                    int node = current;
                    while (parent.TryGetValue(node, out int previous))
                    {
                        path.Add(previous);
                        node = previous;
                    }

                    path.Reverse();
                    return [.. path];
                }

                if (!parent.ContainsKey(prerequisite))
                {
                    parent[prerequisite] = current;
                    queue.Enqueue(prerequisite);
                }
            }
        }

        // Unreachable for a strongly connected component, but never render a path we cannot justify.
        return [root];
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

        if (HasChunkCycle(chunkPrerequisiteArrays, out int[]? blockedChunks, out int[]? cycleChunks))
        {
            // Everything blocked has to leave the parallel phase - a chunk merely downstream of the cycle
            // cannot be scheduled there either - but only the chunks genuinely in the cycle may be named as
            // depending on each other, which is all the message claims.
            projectionCycleTests = [];
            foreach (int chunk in blockedChunks!)
            {
                projectionCycleTests.AddRange(chunkMembers[chunk]);
            }

            var classNames = new List<string>();
            foreach (int chunk in cycleChunks!)
            {
                classNames.Add(tests[chunkMembers[chunk][0]].TestMethod.FullClassName);
            }

            warnings.Add(string.Format(CultureInfo.CurrentCulture, Resource.DependsOnClassLevelCycle, string.Join(", ", classNames)));

            // The caller re-chunks without these tests, so the arrays built here are discarded. Returning them
            // unchanged keeps this method free of a partially-valid state.
            return (BuildChunkArrays(chunkMembers, prerequisites, tests), chunkPrerequisiteArrays);
        }

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

    /// <summary>
    /// Reports whether the chunk graph contains a cycle, and if so which chunks take part in it.
    /// </summary>
    /// <summary>
    /// Reports whether the chunk graph contains a cycle. Kahn's algorithm cannot settle a node that is on a
    /// cycle <em>or</em> merely downstream of one, so the two are separated: <paramref name="blockedChunks"/>
    /// is everything that cannot be scheduled (all of it has to leave the parallel phase), while
    /// <paramref name="cycleChunks"/> is the subset genuinely in a cycle, which is what the diagnostic may
    /// claim depends on each other.
    /// </summary>
    private static bool HasChunkCycle(int[][] chunkPrerequisites, out int[]? blockedChunks, out int[]? cycleChunks)
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

        bool[] isBlocked = new bool[chunkPrerequisites.Length];
        var blocked = new List<int>();
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] > 0)
            {
                isBlocked[i] = true;
                blocked.Add(i);
            }
        }

        blockedChunks = [.. blocked];
        cycleChunks = FindChunksOnCycle(chunkPrerequisites, isBlocked, blocked);
        return true;
    }

    /// <summary>
    /// Narrows the blocked chunks down to those actually on a cycle, by repeatedly discarding any chunk that
    /// no other blocked chunk depends on. Such a chunk can reach the cycle but nothing returns to it, so it is
    /// downstream of the cycle rather than part of it; what survives is exactly the chunks that can reach
    /// themselves.
    /// </summary>
    private static int[] FindChunksOnCycle(int[][] chunkPrerequisites, bool[] isBlocked, List<int> blocked)
    {
        int[] blockedDependentCount = new int[chunkPrerequisites.Length];
        foreach (int chunk in blocked)
        {
            foreach (int prerequisite in chunkPrerequisites[chunk])
            {
                if (isBlocked[prerequisite])
                {
                    blockedDependentCount[prerequisite]++;
                }
            }
        }

        bool[] isOnCycle = new bool[chunkPrerequisites.Length];
        foreach (int chunk in blocked)
        {
            isOnCycle[chunk] = true;
        }

        var queue = new Queue<int>();
        foreach (int chunk in blocked)
        {
            if (blockedDependentCount[chunk] == 0)
            {
                queue.Enqueue(chunk);
            }
        }

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            isOnCycle[current] = false;
            foreach (int prerequisite in chunkPrerequisites[current])
            {
                if (isOnCycle[prerequisite] && --blockedDependentCount[prerequisite] == 0)
                {
                    queue.Enqueue(prerequisite);
                }
            }
        }

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
