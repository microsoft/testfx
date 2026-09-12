// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestDependencyCoordinatorTests : TestContainer
{
    private const string ClassA = "Ns.ClassA";

    private static UnitTestElement CreateElement(string methodName, params TestDependencyInfo[] dependencies)
        => new(new TestMethod(methodName, ClassA, "DummyAssembly", displayName: null))
        {
            Dependencies = dependencies.Length == 0 ? null : dependencies,
        };

    private static TestDependencyInfo DependsOnMethod(string methodName, bool proceedOnFailure = false)
        => new(null, methodName, proceedOnFailure);

    private static TestDependencyCoordinator CreateCoordinator(params UnitTestElement[] tests)
    {
        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)
            ?? throw new InvalidOperationException("The test dependency graph could not be built.");
        return new TestDependencyCoordinator(graph);
    }

    public void ShouldSkip_WhenTestHasNoPrerequisites_ReturnsFalse()
    {
        UnitTestElement prerequisite = CreateElement("Prerequisite");
        UnitTestElement dependent = CreateElement("Dependent", DependsOnMethod("Prerequisite"));
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);

        coordinator.ShouldSkip(prerequisite, out string? reason).Should().BeFalse();
        reason.Should().BeNull();
    }

    public void RecordOutcome_ControlsWhetherDependentIsSkipped()
    {
        UnitTestElement prerequisite = CreateElement("Prerequisite");
        UnitTestElement dependent = CreateElement("Dependent", DependsOnMethod("Prerequisite"));
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);

        coordinator.RecordOutcome(prerequisite, passed: true);

        coordinator.ShouldSkip(dependent, out string? reason).Should().BeFalse();
        reason.Should().BeNull();

        coordinator.RecordOutcome(prerequisite, passed: false);

        coordinator.ShouldSkip(dependent, out reason).Should().BeTrue();
        reason.Should().Be($"Test skipped because it depends on '{prerequisite.TestMethod.FullyQualifiedName}', which did not pass.");
    }

    public void ShouldSkip_WhenProceedOnFailureIsSet_ReturnsFalse()
    {
        UnitTestElement prerequisite = CreateElement("Prerequisite");
        UnitTestElement dependent = CreateElement("Dependent", DependsOnMethod("Prerequisite", proceedOnFailure: true));
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);
        coordinator.RecordOutcome(prerequisite, passed: false);

        coordinator.ShouldSkip(dependent, out string? reason).Should().BeFalse();
        reason.Should().BeNull();
    }

    public void ShouldSkip_WhenAnyPrerequisiteDidNotPass_ReturnsTrue()
    {
        UnitTestElement passedPrerequisite = CreateElement("PassedPrerequisite");
        UnitTestElement failedPrerequisite = CreateElement("FailedPrerequisite");
        UnitTestElement dependent = CreateElement(
            "Dependent",
            DependsOnMethod("PassedPrerequisite"),
            DependsOnMethod("FailedPrerequisite"));
        TestDependencyCoordinator coordinator = CreateCoordinator(passedPrerequisite, failedPrerequisite, dependent);
        coordinator.RecordOutcome(passedPrerequisite, passed: true);
        coordinator.RecordOutcome(failedPrerequisite, passed: false);

        coordinator.ShouldSkip(dependent, out string? reason).Should().BeTrue();
        reason.Should().Be($"Test skipped because it depends on '{failedPrerequisite.TestMethod.FullyQualifiedName}', which did not pass.");
    }

    public void ShouldSkip_WhenPrerequisiteHasNotCompleted_ReturnsTrue()
    {
        UnitTestElement prerequisite = CreateElement("Prerequisite");
        UnitTestElement dependent = CreateElement("Dependent", DependsOnMethod("Prerequisite"));
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);

        coordinator.ShouldSkip(dependent, out string? reason).Should().BeTrue();
        reason.Should().Contain(prerequisite.TestMethod.FullyQualifiedName);
    }

    public void RecordOutcome_TracksEqualTestsByReferenceIdentity()
    {
        UnitTestElement firstDataRow = CreateElement("DataTest");
        UnitTestElement secondDataRow = CreateElement("DataTest");
        UnitTestElement dependent = CreateElement("Dependent", DependsOnMethod("DataTest"));
        TestDependencyCoordinator coordinator = CreateCoordinator(firstDataRow, secondDataRow, dependent);

        coordinator.RecordOutcome(firstDataRow, passed: true);

        coordinator.ShouldSkip(dependent, out _).Should().BeTrue("the second data row has not completed");

        coordinator.RecordOutcome(secondDataRow, passed: true);

        coordinator.ShouldSkip(dependent, out string? reason).Should().BeFalse();
        reason.Should().BeNull();
    }

    public void RecordNotRun_CausesDependentToBeSkipped()
    {
        UnitTestElement prerequisite = CreateElement("Prerequisite");
        UnitTestElement dependent = CreateElement("Dependent", DependsOnMethod("Prerequisite"));
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);

        coordinator.RecordNotRun(prerequisite);

        coordinator.ShouldSkip(dependent, out string? reason).Should().BeTrue();
        reason.Should().Contain(prerequisite.TestMethod.FullyQualifiedName);
    }

    public void UnknownTest_IsIgnored()
    {
        UnitTestElement prerequisite = CreateElement("Prerequisite");
        UnitTestElement dependent = CreateElement("Dependent", DependsOnMethod("Prerequisite"));
        UnitTestElement unknown = CreateElement("Unknown");
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);

        coordinator.RecordOutcome(unknown, passed: true);
        coordinator.RecordNotRun(unknown);

        coordinator.ShouldSkip(unknown, out string? unknownReason).Should().BeFalse();
        unknownReason.Should().BeNull();
        coordinator.ShouldSkip(dependent, out _).Should().BeTrue("recording an unknown test must not change tracked state");
    }

    public async Task ConcurrentAccess_RecordsOutcomesAndReadsFinalState()
    {
        const int PairCount = 16;
        var prerequisites = new UnitTestElement[PairCount];
        var dependents = new UnitTestElement[PairCount];
        var tests = new UnitTestElement[PairCount * 2];
        for (int i = 0; i < PairCount; i++)
        {
            prerequisites[i] = CreateElement($"Prerequisite{i}");
            dependents[i] = CreateElement($"Dependent{i}", DependsOnMethod($"Prerequisite{i}"));
            tests[i * 2] = prerequisites[i];
            tests[(i * 2) + 1] = dependents[i];
        }

        TestDependencyCoordinator coordinator = CreateCoordinator(tests);
        var allReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int readyCount = 0;

        Task<bool>[] tasks = Enumerable.Range(0, PairCount).Select(i => Task.Run(async () =>
        {
            if (Interlocked.Increment(ref readyCount) == PairCount)
            {
                allReady.SetResult(true);
            }

            await start.Task;

            coordinator.RecordOutcome(prerequisites[i], passed: true);
            return coordinator.ShouldSkip(dependents[i], out _);
        })).ToArray();

        await allReady.Task;
        start.SetResult(true);

        bool[] skipped = await Task.WhenAll(tasks);

        skipped.Should().AllSatisfy(wasSkipped => wasSkipped.Should().BeFalse());
        dependents.Should().AllSatisfy(dependent => coordinator.ShouldSkip(dependent, out _).Should().BeFalse());
    }
}
