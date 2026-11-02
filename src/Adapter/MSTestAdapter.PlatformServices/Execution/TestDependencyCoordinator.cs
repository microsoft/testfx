// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Tracks, for one source, the outcome of every test that takes part in the dependency graph, and answers
/// the only question the executor asks of it: whether a test may run, or must be skipped because a
/// prerequisite did not pass.
/// </summary>
/// <remarks>
/// Instances are shared by the parallel workers, so every access to the outcome state is synchronized. The
/// state is tiny (two bits per test) and touched twice per test, so a single lock is cheaper and easier to
/// reason about than finer-grained synchronization.
/// </remarks>
internal sealed class TestDependencyCoordinator
{
    private readonly TestDependencyGraph _graph;
    private readonly Dictionary<UnitTestElement, int> _indexByTest;
    private readonly bool[] _completed;
    private readonly bool[] _passed;
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    public TestDependencyCoordinator(TestDependencyGraph graph)
    {
        _graph = graph;
        _completed = new bool[graph.Tests.Length];
        _passed = new bool[graph.Tests.Length];

        // The scheduler hands back the very instances the graph was built from, so reference identity is the
        // correct - and cheapest - way to find a test's node. Value equality is not an option: two data rows
        // of the same test method are distinct nodes but compare equal on their identifying fields.
        _indexByTest = new Dictionary<UnitTestElement, int>(graph.Tests.Length, ReferenceComparer.Instance);
        for (int i = 0; i < graph.Tests.Length; i++)
        {
            _indexByTest[graph.Tests[i]] = i;
        }
    }

    /// <summary>
    /// Determines whether <paramref name="test"/> must be skipped because one of its prerequisites did not
    /// pass, and if so produces the message to report. A test that declares no dependency, or that opted
    /// into <c>ProceedOnFailure</c>, is never skipped by this method.
    /// </summary>
    /// <remarks>
    /// A prerequisite that has not completed counts as not passed. That happens for a test dropped from the
    /// run because it takes part in a cycle, and it is the conservative reading: the dependent's precondition
    /// demonstrably did not hold.
    /// </remarks>
    public bool ShouldSkip(UnitTestElement test, [NotNullWhen(true)] out string? reason)
    {
        reason = null;
        if (!_indexByTest.TryGetValue(test, out int index))
        {
            return false;
        }

        int[] prerequisites = _graph.TestPrerequisites[index];
        if (prerequisites.Length == 0 || _graph.ProceedOnFailure[index])
        {
            return false;
        }

        lock (_lock)
        {
            foreach (int prerequisite in prerequisites)
            {
                if (!_completed[prerequisite] || !_passed[prerequisite])
                {
                    reason = string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.DependsOnPrerequisiteNotPassed,
                        _graph.Tests[prerequisite].TestMethod.FullyQualifiedName);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Records the outcome of a test that the scheduler ran, so that its dependents can be gated on it.
    /// </summary>
    public void RecordOutcome(UnitTestElement test, bool passed)
    {
        if (!_indexByTest.TryGetValue(test, out int index))
        {
            return;
        }

        lock (_lock)
        {
            _completed[index] = true;
            _passed[index] = passed;
        }
    }

    /// <summary>
    /// Records that a test will never run - because it takes part in a cycle, or because it was skipped -
    /// so that everything downstream of it is skipped in turn.
    /// </summary>
    public void RecordNotRun(UnitTestElement test)
    {
        if (!_indexByTest.TryGetValue(test, out int index))
        {
            return;
        }

        lock (_lock)
        {
            _completed[index] = true;
            _passed[index] = false;
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<UnitTestElement>
    {
        public static readonly ReferenceComparer Instance = new();

        public bool Equals(UnitTestElement? x, UnitTestElement? y) => ReferenceEquals(x, y);

        public int GetHashCode(UnitTestElement obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
