#pragma warning disable IDE0073 // The file header does not match the required text
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.
#pragma warning restore IDE0073 // The file header does not match the required text

using Microsoft.Testing.Extensions.Policy;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class RetryArgumentsBuilderTests
{
    // Mirrors RetryArgumentsBuilder.CommandLineLengthLimit and RetryArgumentsBuilder.PerArgumentOverhead: the
    // builder falls back to a response file once the predicted command line exceeds the limit, and each failed
    // UID contributes its own length plus the per-argument overhead to that prediction.
    private const int CommandLineLengthLimit = 30_000;
    private const int PerArgumentOverhead = 3;

    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName, "50")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName, "5")]
    [DataRow(RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName, "1s")]
    [DataRow(PlatformCommandLineProvider.ResultDirectoryOptionKey, "results")]
    [TestMethod]
    public void ComputeIndicesToCleanup_WithOptionalOption_ReturnsOptionAndValueIndices(string optionalOptionName, string optionalOptionValue)
    {
        string[] executableArguments =
        [
            "test.dll",
            $"-{RetryCommandLineOptionsProvider.RetryFailedTestsOptionName}",
            "3",
            $"--{optionalOptionName}",
            optionalOptionValue,
            "--keep",
            "value",
        ];

        List<int> actual = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);

        int[] expected = [1, 2, 3, 4];
        Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void ComputeIndicesToCleanup_WithoutOptionalOptions_ReturnsOnlyRetryOptionIndices()
    {
        string[] executableArguments =
        [
            "test.dll",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsOptionName}",
            "3",
            "--keep",
            "value",
        ];

        List<int> actual = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);

        int[] expected = [1, 2];
        Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void ComputeIndicesToCleanup_WithAllOptionalOptions_ReturnsEveryOptionAndValueIndex()
    {
        string[] executableArguments =
        [
            "test.dll",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsOptionName}",
            "3",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsMaxPercentageOptionName}",
            "50",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName}",
            "5",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName}",
            "1s",
            $"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}",
            "results",
            "--keep",
            "value",
        ];

        List<int> actual = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);

        int[] expected = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void ComputeIndicesToCleanup_WithDelayOptionAsLastArgument_DoesNotReturnIndexBeyondArguments()
    {
        string[] executableArguments =
        [
            "test.dll",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsOptionName}",
            "3",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsDelayOptionName}",
        ];

        List<int> actual = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);

        int[] expected = [1, 2, 3];
        Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public void ComputeIndicesToCleanup_WithInlineOptionValues_ReturnsOnlyOptionIndices()
    {
        string[] executableArguments =
        [
            "test.dll",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsOptionName}=3",
            $"-{RetryCommandLineOptionsProvider.RetryFailedTestsMaxTestsOptionName}:5",
            $"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}=results",
            "--keep",
            "value",
        ];

        List<int> actual = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);

        Assert.AreSequenceEqual([1, 2, 3], actual, SequenceOrder.InAnyOrder);
    }

    [TestMethod]
    public async Task BuildAttemptArgumentsAsync_WithFailedIds_ReplacesFiltersAndMinimumExpectedTests()
    {
        string[] executableArguments =
        [
            "test.dll",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsOptionName}",
            "3",
            "--keep",
            "value",
            $"--{PlatformCommandLineProvider.FilterUidOptionKey}",
            "old-1",
            "old-2",
            $"--{TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter}",
            "/old-filter",
            $"--{PlatformCommandLineProvider.MinimumExpectedTestsOptionKey}",
            "10",
        ];
        List<int> indicesToCleanup = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        List<string> actual = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
            fileSystem.Object,
            executableArguments,
            indicesToCleanup,
            "retry-root-2",
            "retry-root",
            "pipe-name",
            ["failed uid", "failed-2"],
            attemptCount: 2).ConfigureAwait(false);

        AssertArguments(
            [
                "test.dll",
                "--keep",
                "value",
                $"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}",
                "retry-root-2",
                $"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}",
                "pipe-name",
                $"--{PlatformCommandLineProvider.FilterUidOptionKey}",
                "failed uid",
                "failed-2",
            ],
            actual);
        fileSystem.Verify(
            fs => fs.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>()),
            Times.Never);
    }

    [TestMethod]
    public async Task BuildAttemptArgumentsAsync_WithOriginalResponseFile_WritesCleanedArgumentsToResponseFile()
    {
        string retryRoot = Path.Combine("results", "Retries", "run");
        string responseFilePath = Path.Combine(retryRoot, "retry-arguments-1.rsp");
        string[] executableArguments =
        [
            "exec",
            "test.dll",
            "--keep",
            "value with spaces",
            $"--{TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter}",
            "/old-filter",
            $"--{PlatformCommandLineProvider.MinimumExpectedTestsOptionKey}",
            "10",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsOptionName}=1",
            $"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}",
            "old-results",
        ];
        List<int> indicesToCleanup = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);
        using var memoryStream = new MemoryStream();
        var fileStream = new Mock<IFileStream>(MockBehavior.Strict);
        fileStream.SetupGet(stream => stream.Stream).Returns(memoryStream);
        fileStream.Setup(stream => stream.Dispose());
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem
            .Setup(fs => fs.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write))
            .Returns(fileStream.Object);

        List<string> actual = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
            fileSystem.Object,
            executableArguments,
            ["exec", "test.dll", "@original.rsp"],
            indicesToCleanup,
            Path.Combine(retryRoot, "1"),
            retryRoot,
            "pipe-name",
            lastListOfFailedId: ["failed-uid"],
            attemptCount: 1).ConfigureAwait(false);

        AssertArguments(
            [
                "exec",
                "test.dll",
                $"@{responseFilePath}",
                $"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}",
                Path.Combine(retryRoot, "1"),
                $"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}",
                "pipe-name",
                $"--{PlatformCommandLineProvider.FilterUidOptionKey}",
                "failed-uid",
            ],
            actual);
        Assert.AreEqual(
            $"\"--keep\"{Environment.NewLine}\"value with spaces\"{Environment.NewLine}",
            Encoding.UTF8.GetString(memoryStream.ToArray()));
    }

    [TestMethod]
    public async Task BuildAttemptArgumentsAsync_WithQuotedDirectPrefix_WritesSuffixToResponseFile()
    {
        string retryRoot = Path.Combine("results", "Retries", "run");
        string responseFilePath = Path.Combine(retryRoot, "retry-arguments-1.rsp");
        string[] executableArguments = ["exec", "quoted\"prefix", "--keep", "value"];
        using var memoryStream = new MemoryStream();
        var fileStream = new Mock<IFileStream>(MockBehavior.Strict);
        fileStream.SetupGet(stream => stream.Stream).Returns(memoryStream);
        fileStream.Setup(stream => stream.Dispose());
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem
            .Setup(fs => fs.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write))
            .Returns(fileStream.Object);

        List<string> actual = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
            fileSystem.Object,
            executableArguments,
            ["exec", "quoted\"prefix", "@original.rsp"],
            [],
            Path.Combine(retryRoot, "1"),
            retryRoot,
            "pipe-name",
            lastListOfFailedId: null,
            attemptCount: 1).ConfigureAwait(false);

        Assert.AreSequenceEqual(
            [
                "exec",
                "quoted\"prefix",
                $"@{responseFilePath}",
                $"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}",
                Path.Combine(retryRoot, "1"),
                $"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}",
                "pipe-name",
            ],
            actual);
        Assert.AreEqual(
            $"\"--keep\"{Environment.NewLine}\"value\"{Environment.NewLine}",
            Encoding.UTF8.GetString(memoryStream.ToArray()));
    }

    [TestMethod]
    public async Task BuildAttemptArgumentsAsync_WithMultipleOriginalResponseFiles_WritesEntireExpandedSuffix()
    {
        string retryRoot = Path.Combine("results", "Retries", "run");
        string responseFilePath = Path.Combine(retryRoot, "retry-arguments-1.rsp");
        string[] executableArguments = ["exec", "--first", "a", "--between", "b", "--second", "c", "--after", "d"];
        using var memoryStream = new MemoryStream();
        var fileStream = new Mock<IFileStream>(MockBehavior.Strict);
        fileStream.SetupGet(stream => stream.Stream).Returns(memoryStream);
        fileStream.Setup(stream => stream.Dispose());
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem
            .Setup(fs => fs.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write))
            .Returns(fileStream.Object);

        List<string> actual = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
            fileSystem.Object,
            executableArguments,
            ["exec", "@first.rsp", "--between", "b", "@second.rsp", "--after", "d"],
            [],
            Path.Combine(retryRoot, "1"),
            retryRoot,
            "pipe-name",
            lastListOfFailedId: null,
            attemptCount: 1).ConfigureAwait(false);

        Assert.AreEqual("exec", actual[0]);
        Assert.AreEqual($"@{responseFilePath}", actual[1]);
        Assert.AreEqual(
            string.Join(Environment.NewLine, executableArguments.Skip(1).Select(argument => $"\"{argument}\"")) + Environment.NewLine,
            Encoding.UTF8.GetString(memoryStream.ToArray()));
    }

    [TestMethod]
    public Task BuildAttemptArgumentsAsync_WithNullFailedIds_KeepsOriginalFiltersAndMinimumExpectedTests()
        => AssertFirstAttemptKeepsOriginalFiltersAsync(lastListOfFailedId: null);

    [TestMethod]
    public Task BuildAttemptArgumentsAsync_WithEmptyFailedIds_KeepsOriginalFiltersAndMinimumExpectedTests()
        => AssertFirstAttemptKeepsOriginalFiltersAsync(lastListOfFailedId: []);

    [TestMethod]
    public async Task BuildAttemptArgumentsAsync_OverLengthFailedIds_WritesFailedIdsToResponseFile()
    {
        string retryRoot = Path.Combine("results", "Retries", "run");
        string responseFilePath = Path.Combine(retryRoot, "retry-filter-uids-2.rsp");
        string[] executableArguments = ["test.dll"];
        string[] failedIds = CreateOverLengthFailedIds("uid with whitespace", "#comment-like-uid");
        AssertOnlyFailedIdsExceedLengthLimit(executableArguments, failedIds);
        using var memoryStream = new MemoryStream();
        var fileStream = new Mock<IFileStream>(MockBehavior.Strict);
        fileStream.SetupGet(stream => stream.Stream).Returns(memoryStream);
        fileStream.Setup(stream => stream.Dispose());
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem
            .Setup(fs => fs.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write))
            .Returns(fileStream.Object);

        List<string> actual = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
            fileSystem.Object,
            executableArguments,
            [],
            Path.Combine(retryRoot, "2"),
            retryRoot,
            "pipe-name",
            failedIds,
            attemptCount: 2).ConfigureAwait(false);

        Assert.Contains($"@{responseFilePath}", actual);
        Assert.DoesNotContain($"--{PlatformCommandLineProvider.FilterUidOptionKey}", actual);

        var expectedResponseFileContent = new StringBuilder($"--{PlatformCommandLineProvider.FilterUidOptionKey}");
        foreach (string failedId in failedIds)
        {
            expectedResponseFileContent.Append(" \"").Append(failedId).Append('"');
        }

        expectedResponseFileContent.Append(Environment.NewLine);
        Assert.AreEqual(expectedResponseFileContent.ToString(), Encoding.UTF8.GetString(memoryStream.ToArray()));
        fileSystem.Verify(
            fs => fs.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write),
            Times.Once);
        fileStream.Verify(stream => stream.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAttemptArgumentsAsync_OverLengthFailedIdsWithQuotedUid_KeepsFailedIdsInline()
    {
        // ResponseFileHelper.SplitCommandLine strips '"' from tokens, so a UID containing a literal quote
        // cannot round-trip through a response file and must stay inline even when the payload is over length.
        const string QuotedUid = "uid with a \"quoted\" value";
        string[] executableArguments = ["test.dll"];
        string[] failedIds = CreateOverLengthFailedIds(QuotedUid);
        AssertOnlyFailedIdsExceedLengthLimit(executableArguments, failedIds);
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        List<string> actual = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
            fileSystem.Object,
            executableArguments,
            [],
            "retry-root-2",
            "retry-root",
            "pipe-name",
            failedIds,
            attemptCount: 2).ConfigureAwait(false);

        int filterUidIndex = actual.IndexOf($"--{PlatformCommandLineProvider.FilterUidOptionKey}");
        Assert.IsGreaterThanOrEqualTo(0, filterUidIndex);
        Assert.AreSequenceEqual(failedIds, actual.Skip(filterUidIndex + 1));
        Assert.DoesNotContain(
            argument => argument.StartsWith("@", StringComparison.Ordinal),
            actual);
        fileSystem.Verify(
            fs => fs.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>()),
            Times.Never);
    }

    private static async Task AssertFirstAttemptKeepsOriginalFiltersAsync(string[]? lastListOfFailedId)
    {
        string[] executableArguments =
        [
            "test.dll",
            $"--{RetryCommandLineOptionsProvider.RetryFailedTestsOptionName}",
            "3",
            "--keep",
            "value",
            $"--{PlatformCommandLineProvider.FilterUidOptionKey}",
            "old-1",
            "old-2",
            $"--{TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter}",
            "/old-filter",
            $"--{PlatformCommandLineProvider.MinimumExpectedTestsOptionKey}",
            "10",
        ];
        List<int> indicesToCleanup = RetryArgumentsBuilder.ComputeIndicesToCleanup(executableArguments);
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        List<string> actual = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
            fileSystem.Object,
            executableArguments,
            indicesToCleanup,
            "retry-root-1",
            "retry-root",
            "pipe-name",
            lastListOfFailedId,
            attemptCount: 1).ConfigureAwait(false);

        AssertArguments(
            [
                "test.dll",
                "--keep",
                "value",
                $"--{PlatformCommandLineProvider.FilterUidOptionKey}",
                "old-1",
                "old-2",
                $"--{TreeNodeFilterCommandLineOptionsProvider.TreenodeFilter}",
                "/old-filter",
                $"--{PlatformCommandLineProvider.MinimumExpectedTestsOptionKey}",
                "10",
                $"--{PlatformCommandLineProvider.ResultDirectoryOptionKey}",
                "retry-root-1",
                $"--{RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName}",
                "pipe-name",
            ],
            actual);
        fileSystem.Verify(
            fs => fs.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>()),
            Times.Never);
    }

    /// <summary>
    /// Builds a realistic failed-test payload whose UIDs alone push the predicted command line past the limit,
    /// so the response-file fallback is driven by the failed UIDs rather than by an over-long original argument.
    /// </summary>
    private static string[] CreateOverLengthFailedIds(params string[] leadingUids)
    {
        List<string> uids = [.. leadingUids];
        int predictedLength = uids.Sum(uid => uid.Length + PerArgumentOverhead);
        while (predictedLength <= CommandLineLengthLimit)
        {
            string uid = $"Contoso.Widgets.Tests.CalculatorTests.Add(first: {uids.Count}, second: {uids.Count})";
            uids.Add(uid);
            predictedLength += uid.Length + PerArgumentOverhead;
        }

        return [.. uids];
    }

    /// <summary>
    /// Guards the fixture itself: the original command line must stay under the limit and the failed-UID payload
    /// must cross it on its own. Otherwise the over-length tests would keep passing even if the builder stopped
    /// counting UID lengths when predicting the command-line length.
    /// </summary>
    private static void AssertOnlyFailedIdsExceedLengthLimit(string[] executableArguments, string[] failedIds)
    {
        Assert.IsLessThan(
            CommandLineLengthLimit,
            executableArguments.Sum(argument => argument.Length + PerArgumentOverhead),
            "The original arguments must stay under the limit so they cannot trigger the response file on their own.");
        Assert.IsGreaterThan(
            CommandLineLengthLimit,
            failedIds.Sum(uid => uid.Length + PerArgumentOverhead),
            "The failed-UID payload must exceed the limit on its own.");
    }

    private static void AssertArguments(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        Assert.HasCount(expected.Count, actual);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i], actual[i], $"Argument at index {i} differs.");
        }
    }
}
