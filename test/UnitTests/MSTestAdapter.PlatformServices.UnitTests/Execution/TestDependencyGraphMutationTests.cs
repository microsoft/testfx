// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestDependencyGraphMutationTests : TestContainer
{
    private const string ClassA = "Ns.ClassA";
    private const string ClassB = "Ns.ClassB";

    private static UnitTestElement CreateElement(string className, string methodName, params TestDependencyInfo[] dependencies)
        => new(new TestMethod(methodName, className, "DummyAssembly", displayName: null))
        {
            Dependencies = dependencies.Length == 0 ? null : dependencies,
        };

    private static TestDependencyInfo DependsOnMethod(string methodName)
        => new(null, methodName, proceedOnFailure: false);

    private static string[] NamesOfBroken(IEnumerable<TestDependencyGraph.BrokenTest> broken)
        => [.. broken.Select(b => b.Element.TestMethod.Name)];

    private static string CycleMessage(string path)
        => string.Format(CultureInfo.CurrentCulture, Resource.DependsOnCycle, path);

    private static TestDependencyCoordinator CreateCoordinator(params UnitTestElement[] tests)
    {
        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)
            ?? throw new InvalidOperationException("The test dependency graph could not be built.");
        return new TestDependencyCoordinator(graph);
    }

    public void Build_WhenTestsOnlyCarryEmptyDependencyLists_ReturnsNull()
    {
        UnitTestElement first = CreateElement(ClassA, "First");
        first.Dependencies = [];
        UnitTestElement second = CreateElement(ClassA, "Second");
        second.Dependencies = [];

        var graph = TestDependencyGraph.Build([first, second], ExecutionScope.MethodLevel, parallelizationEnabled: true);

        graph.Should().BeNull("an empty list declares nothing, so the run must stay on the ordinary scheduling path");
    }

    public void Build_WhenTwoTestsDependOnEachOther_DescribesTheCycleFromTheTestThatRootsIt()
    {
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "One", DependsOnMethod("Two")),
            CreateElement(ClassA, "Two", DependsOnMethod("One")),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().Equal(CycleMessage("Ns.ClassA.One > Ns.ClassA.Two > Ns.ClassA.One"));
        foreach (TestDependencyGraph.BrokenTest broken in graph.BrokenTests)
        {
            broken.CycleMessage.Should().Be(graph.Errors[0]);
        }
    }

    public void Build_WhenThreeTestsFormACycle_DescribesItInTheDirectionOfTheDeclaredEdges()
    {
        // A waits for B, B for C and C for A, so the path has to read A > B > C > A. Reading it backwards would
        // claim that C waits for B, which nobody declared.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "A", DependsOnMethod("B")),
            CreateElement(ClassA, "B", DependsOnMethod("C")),
            CreateElement(ClassA, "C", DependsOnMethod("A")),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().Equal(CycleMessage("Ns.ClassA.A > Ns.ClassA.B > Ns.ClassA.C > Ns.ClassA.A"));
        NamesOfBroken(graph.BrokenTests).Should().BeEquivalentTo(["A", "B", "C"]);
    }

    public void Build_WhenASelfDependentTestIsAlsoReachedFromAnEarlierTest_ReportsItsCycleOnce()
    {
        // 'Dependent' is visited first and walks into 'Loop', so 'Loop' has already been settled when the
        // outer loop reaches it again. Visiting it a second time would report the same cycle twice.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "Dependent", DependsOnMethod("Loop")),
            CreateElement(ClassA, "Loop", DependsOnMethod("Loop")),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().Equal(CycleMessage("Ns.ClassA.Loop > Ns.ClassA.Loop"));
        NamesOfBroken(graph.BrokenTests).Should().Equal("Loop");
    }

    public void Build_WhenACycleDependsOnACycleThatWasAlreadySettled_ReportsBothCycles()
    {
        // C waits for the A/B cycle and for itself. Once A and B are settled they are no longer on the stack
        // of the search, so the edge from C into them cannot drag C into their component: C is a cycle of
        // its own, and has to be reported as one.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "A", DependsOnMethod("B")),
            CreateElement(ClassA, "B", DependsOnMethod("A")),
            CreateElement(ClassA, "C", DependsOnMethod("A"), DependsOnMethod("C")),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().Equal(
            CycleMessage("Ns.ClassA.A > Ns.ClassA.B > Ns.ClassA.A"),
            CycleMessage("Ns.ClassA.C > Ns.ClassA.C"));
        NamesOfBroken(graph.BrokenTests).Should().BeEquivalentTo(["A", "B", "C"]);
    }

    public void Build_WhenAPrerequisiteIsBroken_DoesNotMakeItsParallelDependentsWaitForIt()
    {
        // CycleOne and CycleTwo are unschedulable and are reported as failed, so they never run in the
        // parallel phase. Its two dependents keep the declared edge (the run-time gate skips them), but no
        // chunk may wait for a chunk that does not exist: the chunk of a test that is not scheduled in the
        // parallel phase must not be mistaken for chunk 0 or chunk 1.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "CycleOne", DependsOnMethod("CycleTwo")),
            CreateElement(ClassA, "CycleTwo", DependsOnMethod("CycleOne")),
            CreateElement(ClassA, "DependentOne", DependsOnMethod("CycleOne")),
            CreateElement(ClassA, "DependentTwo", DependsOnMethod("CycleOne")),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        NamesOfBroken(graph.BrokenTests).Should().BeEquivalentTo(["CycleOne", "CycleTwo"]);
        graph.TestPrerequisites[2].Should().Equal(0);
        graph.TestPrerequisites[3].Should().Equal(0);
        graph.ParallelChunks.Length.Should().Be(2);
        for (int chunk = 0; chunk < graph.ParallelChunkPrerequisites.Length; chunk++)
        {
            graph.ParallelChunkPrerequisites[chunk].Should().BeEmpty($"chunk {chunk} only waits for a test that never runs in the parallel phase");
        }
    }

    public void Build_WhenTwoClassesWaitForEachOther_NamesBothClassesInTheWarning()
    {
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "A1"),
            CreateElement(ClassA, "A2", new TestDependencyInfo(ClassB, "B1", false)),
            CreateElement(ClassB, "B1", new TestDependencyInfo(ClassA, "A1", false)),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.ClassLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().BeEmpty();
        graph.Warnings.Should().Equal(string.Format(CultureInfo.CurrentCulture, Resource.DependsOnClassLevelCycle, "Ns.ClassA, Ns.ClassB"));
    }

    public void RecordNotRun_AfterThePrerequisiteWasRecordedAsPassed_SkipsTheDependent()
    {
        UnitTestElement prerequisite = CreateElement(ClassA, "Prerequisite");
        UnitTestElement dependent = CreateElement(ClassA, "Dependent", DependsOnMethod("Prerequisite"));
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);
        coordinator.RecordOutcome(prerequisite, passed: true);

        coordinator.RecordNotRun(prerequisite);

        coordinator.ShouldSkip(dependent, out string? reason).Should().BeTrue();
        reason.Should().Be(string.Format(CultureInfo.CurrentCulture, Resource.DependsOnPrerequisiteNotPassed, prerequisite.TestMethod.FullyQualifiedName));
    }

    public void RecordNotRun_ForATestThatIsNotInTheGraph_LeavesTheRecordedOutcomesAlone()
    {
        UnitTestElement prerequisite = CreateElement(ClassA, "Prerequisite");
        UnitTestElement dependent = CreateElement(ClassA, "Dependent", DependsOnMethod("Prerequisite"));
        UnitTestElement stranger = CreateElement(ClassA, "Stranger");
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);
        coordinator.RecordOutcome(prerequisite, passed: true);

        coordinator.RecordNotRun(stranger);

        coordinator.ShouldSkip(dependent, out string? reason).Should().BeFalse();
        reason.Should().BeNull();
    }
}
