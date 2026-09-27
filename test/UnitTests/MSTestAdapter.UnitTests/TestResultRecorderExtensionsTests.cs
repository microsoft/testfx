// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;
using VSTestTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class TestResultRecorderExtensionsTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _platformServiceProvider = new();

    public TestResultRecorderExtensionsTests()
    {
        PlatformServiceProvider.Instance = _platformServiceProvider;
        _platformServiceProvider.MockFileOperations
            .Setup(fileOperations => fileOperations.GetFullFilePath(It.IsAny<string>()))
            .Returns((string path) => path);
    }

    public async Task VSTestRecorderShouldMergeSupersededRetryAttachmentsIntoFinalResult()
    {
        var hostRecorder = new Mock<ITestExecutionRecorder>();
        var reportedResults = new List<VSTestTestResult>();
        hostRecorder
            .Setup(recorder => recorder.RecordResult(It.IsAny<VSTestTestResult>()))
            .Callback((VSTestTestResult result) => reportedResults.Add(result));

        ITestResultRecorder recorder = hostRecorder.Object.ToTestResultRecorder(
            Environment.MachineName,
            new MSTestSettings());
        UnitTestElement element = CreateElement();
        string rowAFirstPath = Path.GetFullPath("row-a-first-attempt.json");
        string rowBFirstPath = Path.GetFullPath("row-b-first-attempt.json");
        string rowASecondPath = Path.GetFullPath("row-a-second-attempt.json");
        string rowAFinalPath = Path.GetFullPath("row-a-final-attempt.json");
        string rowBFinalPath = Path.GetFullPath("row-b-final-attempt.json");
        FrameworkTestResult[] results =
        [
            new()
            {
                DisplayName = "Custom name",
                Outcome = UnitTestOutcome.Failed,
                IsSupersededRetryAttempt = true,
                RetryAttemptNumber = 1,
                ResultFiles = [rowAFirstPath],
            },
            new()
            {
                DisplayName = "Custom name",
                Outcome = UnitTestOutcome.Failed,
                IsSupersededRetryAttempt = true,
                RetryAttemptNumber = 1,
                ResultFiles = [rowBFirstPath],
            },
            new()
            {
                DisplayName = "Custom name",
                Outcome = UnitTestOutcome.Failed,
                IsSupersededRetryAttempt = true,
                RetryAttemptNumber = 2,
                ResultFiles = [rowASecondPath],
            },
            new()
            {
                DisplayName = "Custom name",
                Outcome = UnitTestOutcome.Passed,
                IsSupersededRetryAttempt = true,
                RetryAttemptNumber = 2,
            },
            new()
            {
                DisplayName = "Custom name",
                Outcome = UnitTestOutcome.Passed,
                RetryAttemptNumber = 3,
                ResultFiles = [rowAFinalPath],
            },
            new()
            {
                DisplayName = "Custom name",
                Outcome = UnitTestOutcome.Passed,
                RetryAttemptNumber = 3,
                ResultFiles = [rowBFinalPath],
            },
        ];

        recorder.PrepareResults(element, results);
        foreach (FrameworkTestResult result in results)
        {
            await recorder.RecordResultAsync(element, result, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }

        reportedResults.Should().HaveCount(2);
        reportedResults[0].Attachments.Should().ContainSingle();
        reportedResults[0].Attachments[0].Attachments
            .Select(attachment => attachment.Uri.LocalPath)
            .Should().BeEquivalentTo(rowAFirstPath, rowASecondPath, rowAFinalPath);
        reportedResults[1].Attachments.Should().ContainSingle();
        reportedResults[1].Attachments[0].Attachments
            .Select(attachment => attachment.Uri.LocalPath)
            .Should().BeEquivalentTo(rowBFirstPath, rowBFinalPath);
        hostRecorder.Verify(recorder => recorder.RecordResult(It.IsAny<VSTestTestResult>()), Times.Exactly(2));
    }

    public void VSTestRecorderShouldMergeUnmatchedRetryAttachmentsIntoLastFinalResult()
    {
        var hostRecorder = new Mock<ITestExecutionRecorder>();
        ITestResultRecorder recorder = hostRecorder.Object.ToTestResultRecorder(
            Environment.MachineName,
            new MSTestSettings());
        UnitTestElement element = CreateElement();
        string removedRowPath = Path.GetFullPath("removed-row.json");
        var survivingResult = new FrameworkTestResult
        {
            DisplayName = "Row A",
            Outcome = UnitTestOutcome.Passed,
            RetryAttemptNumber = 2,
        };
        FrameworkTestResult[] results =
        [
            new()
            {
                DisplayName = "Row A",
                Outcome = UnitTestOutcome.Failed,
                IsSupersededRetryAttempt = true,
                RetryAttemptNumber = 1,
            },
            new()
            {
                DisplayName = "Row B",
                Outcome = UnitTestOutcome.Failed,
                IsSupersededRetryAttempt = true,
                RetryAttemptNumber = 1,
                ResultFiles = [removedRowPath],
            },
            survivingResult,
        ];

        recorder.PrepareResults(element, results);

        survivingResult.ResultFiles.Should().ContainSingle()
            .Which.Should().Be(removedRowPath);
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            PlatformServiceProvider.Instance = null;
            base.Dispose(disposing);
        }
    }

    private static UnitTestElement CreateElement()
        => new(new TestMethod(
            "TestMethod",
            hierarchyValues: null,
            "TestMethod",
            "Tests.TestClass",
            "Tests.dll",
            displayName: null,
            parameterTypes: null));
}
