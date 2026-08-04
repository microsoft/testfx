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
internal sealed partial class TestDependencyGraph
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
        bool[] isBroken = Array.ConvertAll(cycleMessageByTest, static cycleMessage => cycleMessage is not null);

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
        // and the reported warning tells the user how to get it back.
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
}
