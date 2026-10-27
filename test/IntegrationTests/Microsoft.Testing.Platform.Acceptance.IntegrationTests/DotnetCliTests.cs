// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class DotnetCliTests
{
    private const string CopyTaskInitializationFailure = """
        The "Copy" task failed unexpectedly.
        System.ArgumentNullException: Value cannot be null. (Parameter 'message')
           at Microsoft.Build.Utilities.TaskLoggingHelper.LogMessage(MessageImportance importance, String message, Object[] messageArgs)
           at Microsoft.Build.Tasks.Copy.CopyFileWithLogging(FileState sourceFileState, FileState destinationFileState)
        """;

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_CopyTaskInitializationFailure_Retries(bool failIfReturnValueIsNotZero)
    {
        using TempDirectory directory = new();
        string projectPath = await CreateProjectAsync(directory, "MSB4018", CopyTaskInitializationFailure, failOnce: true);

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{projectPath}\" --no-restore",
            failIfReturnValueIsNotZero: failIfReturnValueIsNotZero,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(ExitCode.Success);
        Assert.HasCount(2, File.ReadAllLines(Path.Combine(directory.Path, "attempts.txt")));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_PersistentCopyTaskInitializationFailure_StopsAfterRetryLimit(bool failIfReturnValueIsNotZero)
    {
        using TempDirectory directory = new();
        string projectPath = await CreateProjectAsync(directory, "MSB4018", CopyTaskInitializationFailure);

        if (failIfReturnValueIsNotZero)
        {
            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                async () => await DotnetCli.RunAsync(
                    $"build \"{projectPath}\" --no-restore",
                    cancellationToken: TestContext.CancellationToken));

            Assert.Contains("Microsoft.Build.Tasks.Copy.CopyFileWithLogging", exception.Message);
        }
        else
        {
            DotnetMuxerResult result = await DotnetCli.RunAsync(
                $"build \"{projectPath}\" --no-restore",
                failIfReturnValueIsNotZero: false,
                cancellationToken: TestContext.CancellationToken);

            result.AssertExitCodeIs(1);
            Assert.Contains("Microsoft.Build.Tasks.Copy.CopyFileWithLogging", result.StandardOutput);
        }

        Assert.HasCount(4, File.ReadAllLines(Path.Combine(directory.Path, "attempts.txt")));
    }

    [TestMethod]
    [DataRow("MSB3021", "Unable to copy the file because access is denied.", DisplayName = "Copy file error")]
    [DataRow("MSB4018", "The \"Copy\" task failed unexpectedly. System.IO.IOException: The file is in use.", DisplayName = "Copy IO exception")]
    [DataRow("MSB4018", "The \"Delete\" task failed unexpectedly. System.ArgumentNullException: Value cannot be null. (Parameter 'message')", DisplayName = "Other task null message")]
    [DataRow("MSB4018", "The \"Copy\" task failed unexpectedly. System.ArgumentNullException: Value cannot be null. (Parameter 'source') at Microsoft.Build.Tasks.Copy.CopyFileWithLogging(FileState sourceFileState, FileState destinationFileState)", DisplayName = "Copy other null parameter")]
    public async Task RunAsync_UnrelatedBuildFailure_DoesNotRetry(string errorCode, string errorMessage)
    {
        using TempDirectory directory = new();
        string projectPath = await CreateProjectAsync(directory, errorCode, errorMessage);

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"build \"{projectPath}\" --no-restore",
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        result.AssertExitCodeIs(1);
        Assert.Contains(errorCode, result.StandardOutput);
        Assert.HasCount(1, File.ReadAllLines(Path.Combine(directory.Path, "attempts.txt")));
    }

    private static async Task<string> CreateProjectAsync(TempDirectory directory, string errorCode, string errorMessage, bool failOnce = false)
    {
        string failureCondition = failOnce ? "'@(PreviousAttempts)' == ''" : "true";
        string project = $$"""
            <Project DefaultTargets="Build">
              <Target Name="Build">
                <ReadLinesFromFile File="$(MSBuildThisFileDirectory)attempts.txt" Condition="Exists('$(MSBuildThisFileDirectory)attempts.txt')">
                  <Output TaskParameter="Lines" ItemName="PreviousAttempts" />
                </ReadLinesFromFile>
                <WriteLinesToFile File="$(MSBuildThisFileDirectory)attempts.txt" Lines="attempt" Overwrite="false" />
                <Error Code="{{errorCode}}" Text="{{SecurityElement.Escape(errorMessage)}}" Condition="{{failureCondition}}" />
              </Target>
            </Project>
            """;

        await TempDirectory.WriteFileAsync(directory.Path, "Retry.proj", project);
        return Path.Combine(directory.Path, "Retry.proj");
    }

    public TestContext TestContext { get; set; }
}
