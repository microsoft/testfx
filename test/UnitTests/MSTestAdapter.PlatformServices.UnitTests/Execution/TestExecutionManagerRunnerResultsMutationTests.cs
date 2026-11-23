// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestExecutionManagerRunnerResultsMutationTests : TestContainer
{
    public async Task SendTestResultsAsyncOnlyRecordsAnEmptyResultWhenThereIsNoResult()
    {
        var recorder = new RecordingRecorder();
        var manager = new TestExecutionManager();

        await manager.SendTestResultsAsync(CreateElement(), [], DateTimeOffset.Now, DateTimeOffset.Now, recorder);

        recorder.Events.Should().Equal("RecordEmptyResult");
    }

    public async Task SendTestResultsAsyncPreparesTheResultsBeforeRecordingEachOfThem()
    {
        var recorder = new RecordingRecorder();
        var manager = new TestExecutionManager();
        UnitTestElement test = CreateElement();
        TestResult[] results =
        [
            new TestResult { Outcome = UnitTestOutcome.Passed },
            new TestResult { Outcome = UnitTestOutcome.Failed },
        ];

        await manager.SendTestResultsAsync(test, results, DateTimeOffset.Now, DateTimeOffset.Now, recorder);

        recorder.Events.Should().Equal("PrepareResults", "RecordResult:Passed", "RecordResult:Failed");
        recorder.PreparedTest.Should().BeSameAs(test);
        recorder.PreparedResults.Should().BeSameAs(results);
    }

    public async Task SendTestResultsAsyncDoesNotPrepareOrRecordAnythingOnceTheRunIsCanceled()
    {
        var recorder = new RecordingRecorder();
        var manager = new TestExecutionManager();
        var token = new TestRunCancellationToken();
        token.Cancel();
        typeof(TestExecutionManager)
            .GetField("_testRunCancellationToken", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(manager, token);

        Func<Task> act = () => manager.SendTestResultsAsync(
            CreateElement(),
            [new TestResult { Outcome = UnitTestOutcome.Passed }],
            DateTimeOffset.Now,
            DateTimeOffset.Now,
            recorder);

        await act.Should().ThrowAsync<OperationCanceledException>();
        recorder.Events.Should().BeEmpty();
    }

    public void AllPassedIsFalseWhenThereIsNoResult()
        => AllPassed().Should().BeFalse();

    public void AllPassedIsTrueOnlyWhenEveryFinalResultPassed()
    {
        AllPassed(Passed()).Should().BeTrue();
        AllPassed(Passed(), Passed()).Should().BeTrue();
        AllPassed(Failed()).Should().BeFalse();
        AllPassed(Passed(), Failed()).Should().BeFalse();
        AllPassed(Failed(), Passed()).Should().BeFalse();
    }

    public void AllPassedIgnoresTheSupersededRetryAttempts()
    {
        AllPassed(Failed(isSupersededRetryAttempt: true), Passed()).Should().BeTrue();
        AllPassed(Passed(), Failed(isSupersededRetryAttempt: true)).Should().BeTrue();
        AllPassed(Failed(isSupersededRetryAttempt: true), Failed()).Should().BeFalse();
    }

    private static TestResult Passed() => new() { Outcome = UnitTestOutcome.Passed };

    private static TestResult Failed(bool isSupersededRetryAttempt = false)
        => new() { Outcome = UnitTestOutcome.Failed, IsSupersededRetryAttempt = isSupersededRetryAttempt };

    private static UnitTestElement CreateElement()
        => new(new TestMethod("TestMethod", "Ns.TestClass", "TestAssembly", displayName: null));

    private static bool AllPassed(params TestResult[] results)
        => (bool)typeof(TestExecutionManager)
            .GetMethod("AllPassed", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [results])!;

    private sealed class RecordingRecorder : ITestResultRecorder
    {
        public List<string> Events { get; } = [];

        public UnitTestElement? PreparedTest { get; private set; }

        public TestResult[]? PreparedResults { get; private set; }

        public void PrepareResults(UnitTestElement test, TestResult[] results)
        {
            Events.Add("PrepareResults");
            PreparedTest = test;
            PreparedResults = results;
        }

        public Task RecordStartAsync(UnitTestElement test)
        {
            Events.Add("RecordStart");
            return Task.CompletedTask;
        }

        public Task RecordEmptyResultAsync(UnitTestElement test)
        {
            Events.Add("RecordEmptyResult");
            return Task.CompletedTask;
        }

        public Task<bool> RecordResultAsync(UnitTestElement test, TestResult result, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            Events.Add("RecordResult:" + result.Outcome);
            return Task.FromResult(false);
        }
    }
}
