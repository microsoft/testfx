// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestDependencyGraphTests : TestContainer
{
    private const string ClassA = "Ns.ClassA";
    private const string ClassB = "Ns.ClassB";

    private static UnitTestElement CreateElement(string className, string methodName, params TestDependencyInfo[] dependencies)
        => new(new TestMethod(methodName, className, "DummyAssembly", displayName: null))
        {
            Dependencies = dependencies.Length == 0 ? null : dependencies,
        };

    private static TestDependencyInfo DependsOnMethod(string methodName, bool proceedOnFailure = false)
        => new(null, methodName, proceedOnFailure);

    private static TestDependencyInfo DependsOnClass(string className)
        => new(className, null, proceedOnFailure: false);

    private static string[] NamesOf(IEnumerable<UnitTestElement> elements)
        => [.. elements.Select(e => e.TestMethod.Name)];

    public void Build_WhenNoTestDeclaresDependencies_ReturnsNull()
    {
        // The null fast path is what keeps every run that does not use the feature on the code it uses today.
        var graph = TestDependencyGraph.Build(
            [CreateElement(ClassA, "A"), CreateElement(ClassA, "B")],
            ExecutionScope.MethodLevel,
            parallelizationEnabled: true);

        graph.Should().BeNull();
    }

    public void Build_OrdersDependentAfterItsPrerequisiteWithinAChunk()
    {
        // Declared in reverse order on purpose: only the dependency, not the declaration order, may decide.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "Second", DependsOnMethod("First")),
            CreateElement(ClassA, "First"),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.ClassLevel, parallelizationEnabled: true)!;

        graph.ParallelChunks.Should().ContainSingle();
        NamesOf(graph.ParallelChunks[0]).Should().Equal("First", "Second");
        graph.Errors.Should().BeEmpty();
    }

    public void Build_UnderClassLevelScope_MakesTheDependentClassWaitForThePrerequisiteClass()
    {
        UnitTestElement[] tests =
        [
            CreateElement(ClassB, "NeedsA", new TestDependencyInfo(ClassA, "Setup", false)),
            CreateElement(ClassA, "Setup"),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.ClassLevel, parallelizationEnabled: true)!;

        graph.ParallelChunks.Length.Should().Be(2);
        int classBChunk = Array.FindIndex(graph.ParallelChunks, c => c[0].TestMethod.FullClassName == ClassB);
        int classAChunk = Array.FindIndex(graph.ParallelChunks, c => c[0].TestMethod.FullClassName == ClassA);

        graph.ParallelChunkPrerequisites[classBChunk].Should().Equal(classAChunk);
        graph.ParallelChunkPrerequisites[classAChunk].Should().BeEmpty();
    }

    public void Build_WhenTwoTestsShareAPrerequisite_LeavesThemIndependentSoTheyCanRunInParallel()
    {
        // Fan-out is the whole point of a graph over a flat order: neither branch may wait for the other.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "Root"),
            CreateElement(ClassA, "BranchOne", DependsOnMethod("Root")),
            CreateElement(ClassA, "BranchTwo", DependsOnMethod("Root")),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        int root = Array.FindIndex(graph.ParallelChunks, c => c[0].TestMethod.Name == "Root");
        int one = Array.FindIndex(graph.ParallelChunks, c => c[0].TestMethod.Name == "BranchOne");
        int two = Array.FindIndex(graph.ParallelChunks, c => c[0].TestMethod.Name == "BranchTwo");

        graph.ParallelChunkPrerequisites[one].Should().Equal(root);
        graph.ParallelChunkPrerequisites[two].Should().Equal(root);
        graph.ParallelChunkPrerequisites[one].Should().NotContain(two);
        graph.ParallelChunkPrerequisites[two].Should().NotContain(one);
    }

    public void Build_WhenDependencyTargetsAWholeClass_WaitsForEveryTestOfThatClass()
    {
        UnitTestElement[] tests =
        [
            CreateElement(ClassB, "Dependent", DependsOnClass(ClassA)),
            CreateElement(ClassA, "One"),
            CreateElement(ClassA, "Two"),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.TestPrerequisites[0].Should().BeEquivalentTo([1, 2]);
    }

    public void Build_WhenAClassDependsOnItself_DoesNotCreateASelfEdge()
    {
        // A whole-class dependency naturally covers the declaring test too; treating that as a cycle would
        // make the common "this class runs after that class" declaration unusable from a base class.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "One", DependsOnClass(ClassA)),
            CreateElement(ClassA, "Two", DependsOnClass(ClassA)),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.TestPrerequisites[0].Should().NotContain(0);
        graph.TestPrerequisites[1].Should().NotContain(1);
    }

    public void Build_WhenDependenciesFormACycle_ReportsItAndMarksTheTestsBroken()
    {
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "One", DependsOnMethod("Two")),
            CreateElement(ClassA, "Two", DependsOnMethod("One")),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().ContainSingle();
        graph.Errors[0].Should().Contain("One").And.Contain("Two");
        NamesOf(graph.BrokenTests).Should().BeEquivalentTo(["One", "Two"]);

        // Broken tests are reported as failed instead of being scheduled, so they must not appear anywhere.
        graph.ParallelChunks.Should().BeEmpty();
        graph.SequentialTests.Should().BeEmpty();
    }

    public void Build_WhenATestDependsOnItselfByName_IsReportedAsACycle()
    {
        UnitTestElement[] tests = [CreateElement(ClassA, "Loop", DependsOnMethod("Loop"))];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().ContainSingle();
        NamesOf(graph.BrokenTests).Should().Equal("Loop");
    }

    public void Build_WhenACycleExistsOnlyInTheClassLevelProjection_RunsTheAffectedTestsSequentiallyInOrder()
    {
        // No test depends on itself, but class A must precede B *and* B must precede A, which cannot hold
        // when the class is the scheduling unit. Dropping the ordering would be worse than losing the
        // parallelism: the run-time gate cannot tell "has not run yet" from "did not pass", so unordered
        // dependents would be skipped nondeterministically while the run still reported success.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "A1"),
            CreateElement(ClassA, "A2", new TestDependencyInfo(ClassB, "B1", false)),
            CreateElement(ClassB, "B1", new TestDependencyInfo(ClassA, "A1", false)),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.ClassLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().ContainSingle();
        graph.Errors[0].Should().Contain("MethodLevel");

        // The test-level graph is sound, so nothing is broken - the tests still run.
        graph.BrokenTests.Should().BeEmpty();

        // They are moved to the sequential phase, where the topological order is honoured exactly.
        graph.ParallelChunks.Should().BeEmpty();
        NamesOf(graph.SequentialTests).Should().Equal("A1", "B1", "A2");
    }

    public void Build_WhenAProjectionCycleIsDemoted_LeavesUnrelatedTestsInTheParallelPhase()
    {
        // Only the classes caught in the projection cycle lose their parallelism; everything else keeps it.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "A1"),
            CreateElement(ClassA, "A2", new TestDependencyInfo(ClassB, "B1", false)),
            CreateElement(ClassB, "B1", new TestDependencyInfo(ClassA, "A1", false)),
            CreateElement("Ns.ClassC", "C1"),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.ClassLevel, parallelizationEnabled: true)!;

        graph.ParallelChunks.Should().ContainSingle();
        NamesOf(graph.ParallelChunks[0]).Should().Equal("C1");
        NamesOf(graph.SequentialTests).Should().Equal("A1", "B1", "A2");
    }

    public void Build_WhenTheSameGraphUsesMethodLevelScope_HasNoProjectionCycle()
    {
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "A1"),
            CreateElement(ClassA, "A2", new TestDependencyInfo(ClassB, "B1", false)),
            CreateElement(ClassB, "B1", new TestDependencyInfo(ClassA, "A1", false)),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.Errors.Should().BeEmpty();
    }

    public void Build_WhenAPrerequisiteIsNotParallelizable_DemotesItsDependentsToTheSequentialPhase()
    {
        // The sequential phase runs after the parallel one, so a parallel test waiting on a sequential
        // prerequisite could never observe it complete. The dependent moves instead.
        UnitTestElement prerequisite = CreateElement(ClassA, "Serial");
        prerequisite.DoNotParallelize = true;

        UnitTestElement[] tests =
        [
            prerequisite,
            CreateElement(ClassA, "Direct", DependsOnMethod("Serial")),
            CreateElement(ClassB, "Transitive", new TestDependencyInfo(ClassA, "Direct", false)),
            CreateElement(ClassB, "Unrelated"),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        NamesOf(graph.SequentialTests).Should().Equal("Serial", "Direct", "Transitive");
        graph.ParallelChunks.Should().ContainSingle();
        graph.ParallelChunks[0][0].TestMethod.Name.Should().Be("Unrelated");
    }

    public void Build_WhenParallelizationIsDisabled_PutsEveryTestInTheSequentialPhaseInDependencyOrder()
    {
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "Last", DependsOnMethod("Middle")),
            CreateElement(ClassA, "Middle", DependsOnMethod("First")),
            CreateElement(ClassA, "First"),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.ClassLevel, parallelizationEnabled: false)!;

        graph.ParallelChunks.Should().BeEmpty();
        NamesOf(graph.SequentialTests).Should().Equal("First", "Middle", "Last");
    }

    public void Build_WhenADependencyMatchesNoTestInTheRun_WarnsAndIgnoresTheEdge()
    {
        // Running a subset (a filter, or a single test from an IDE) must stay possible, so an unmatched
        // reference cannot block the dependent.
        UnitTestElement[] tests = [CreateElement(ClassA, "Only", DependsOnMethod("FilteredOut"))];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.Warnings.Should().ContainSingle();
        graph.Warnings[0].Should().Contain("FilteredOut");
        graph.TestPrerequisites[0].Should().BeEmpty();
        graph.Errors.Should().BeEmpty();
    }

    public void Build_MergesProceedOnFailureAcrossEdges_HoldingBackWhenAnyEdgeAsksToSkip()
    {
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "First"),
            CreateElement(ClassA, "Second"),
            CreateElement(ClassA, "Mixed", DependsOnMethod("First", proceedOnFailure: true), DependsOnMethod("Second")),
            CreateElement(ClassA, "AllProceed", DependsOnMethod("First", proceedOnFailure: true), DependsOnMethod("Second", proceedOnFailure: true)),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)!;

        graph.ProceedOnFailure[2].Should().BeFalse();
        graph.ProceedOnFailure[3].Should().BeTrue();
    }

    public void Build_TieBreaksReadyTestsByDeclarationOrder()
    {
        // Determinism matters: two runs of the same suite must schedule the same way.
        UnitTestElement[] tests =
        [
            CreateElement(ClassA, "Alpha"),
            CreateElement(ClassA, "Beta"),
            CreateElement(ClassA, "Gamma", DependsOnMethod("Beta")),
        ];

        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.ClassLevel, parallelizationEnabled: true)!;

        NamesOf(graph.ParallelChunks[0]).Should().Equal("Alpha", "Beta", "Gamma");
    }

    public void EncodeDecode_RoundTripsEveryTargetShape()
    {
        AssertRoundTrip(new TestDependencyInfo(ClassA, "Method", proceedOnFailure: false));
        AssertRoundTrip(new TestDependencyInfo(ClassA, null, proceedOnFailure: true));
        AssertRoundTrip(new TestDependencyInfo(null, "Method", proceedOnFailure: true));

        static void AssertRoundTrip(TestDependencyInfo original)
        {
            var decoded = TestDependencyInfo.Decode(TestDependencyInfo.Encode(original));
            decoded.TargetClassFullName.Should().Be(original.TargetClassFullName);
            decoded.TargetMethodName.Should().Be(original.TargetMethodName);
            decoded.ProceedOnFailure.Should().Be(original.ProceedOnFailure);
        }
    }

    public void Decode_WhenFlagIsUnrecognized_FailsClosedToSkipAndKeepsTheWholePayload()
    {
        // Proceeding past a failed prerequisite must never be the result of a decoding glitch, and the
        // payload must still name the test it was meant to name.
        var decoded = TestDependencyInfo.Decode("Ns.ClassA\nMethod");
        decoded.ProceedOnFailure.Should().BeFalse();
        decoded.TargetClassFullName.Should().Be("Ns.ClassA");
        decoded.TargetMethodName.Should().Be("Method");
    }

    public void DescribeTarget_RendersEachShapeReadably()
    {
        new TestDependencyInfo(ClassA, "Method", false).DescribeTarget().Should().Be("Ns.ClassA.Method");
        new TestDependencyInfo(ClassA, null, false).DescribeTarget().Should().Be("Ns.ClassA.*");
        new TestDependencyInfo(null, "Method", false).DescribeTarget().Should().Be("Method");
    }
}
