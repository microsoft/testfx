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
        Assert.HasCount(expected.Length, actual);
        foreach (int expectedIndex in expected)
        {
            Assert.Contains(expectedIndex, actual);
        }
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
    public async Task BuildAttemptArgumentsAsync_OverLengthArguments_WritesFailedIdsToResponseFile()
    {
        string retryRoot = Path.Combine("results", "Retries", "run");
        string responseFilePath = Path.Combine(retryRoot, "retry-filter-uids-2.rsp");
        string[] failedIds = ["uid with whitespace", "#comment-like-uid"];
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
            [CreateOverLengthArgument()],
            [],
            Path.Combine(retryRoot, "2"),
            retryRoot,
            "pipe-name",
            failedIds,
            attemptCount: 2).ConfigureAwait(false);

        Assert.Contains($"@{responseFilePath}", actual);
        Assert.DoesNotContain($"--{PlatformCommandLineProvider.FilterUidOptionKey}", actual);
        Assert.AreEqual(
            $"--{PlatformCommandLineProvider.FilterUidOptionKey} \"{failedIds[0]}\" \"{failedIds[1]}\"{Environment.NewLine}",
            Encoding.UTF8.GetString(memoryStream.ToArray()));
        fileSystem.Verify(
            fs => fs.NewFileStream(responseFilePath, FileMode.Create, FileAccess.Write),
            Times.Once);
        fileStream.Verify(stream => stream.Dispose(), Times.Once);
    }

    [TestMethod]
    public async Task BuildAttemptArgumentsAsync_OverLengthArgumentsWithQuotedUid_KeepsFailedIdsInline()
    {
        const string QuotedUid = "uid with a \"quoted\" value";
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        List<string> actual = await RetryArgumentsBuilder.BuildAttemptArgumentsAsync(
            fileSystem.Object,
            [CreateOverLengthArgument()],
            [],
            "retry-root-2",
            "retry-root",
            "pipe-name",
            [QuotedUid],
            attemptCount: 2).ConfigureAwait(false);

        Assert.Contains($"--{PlatformCommandLineProvider.FilterUidOptionKey}", actual);
        Assert.Contains(QuotedUid, actual);
        Assert.DoesNotContain(
            argument => argument.StartsWith("@", StringComparison.Ordinal),
            actual);
        fileSystem.Verify(
            fs => fs.NewFileStream(It.IsAny<string>(), It.IsAny<FileMode>(), It.IsAny<FileAccess>()),
            Times.Never);
    }

    private static string CreateOverLengthArgument()
        => new('x', 40_000);

    private static void AssertArguments(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        Assert.HasCount(expected.Count, actual);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i], actual[i], $"Argument at index {i} differs.");
        }
    }
}
