// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

internal static class AcceptanceAssert
{
    public static void AssertExitCodeIs(this TestHostResult testHostResult, int exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(exitCode == testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertExitCodeIsNot(this TestHostResult testHostResult, int exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(exitCode != testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertOutputMatches(this TestHostResult testHostResult, string wildcardPattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        string[] wildcardLines = wildcardPattern.Split(Environment.NewLine);
        for (int i = 0; i < testHostResult.StandardOutputLines.Count; i++)
        {
            string outputLine = testHostResult.StandardOutputLines[i];

            if (wildcardLines[i].Contains('*'))
            {
                string matchingPatternLine =
                    "^"
                    + Regex.Escape(wildcardLines[i]).Replace("\\*", ".*")
                    + "$";

                Assert.That(
                    Regex.IsMatch(outputLine, matchingPatternLine, RegexOptions.Singleline),
                    $"Output on line {i + 1}{Environment.NewLine}{outputLine}{Environment.NewLine}doesn't match pattern{Environment.NewLine}{matchingPatternLine}",
                    callerMemberName: callerMemberName,
                    callerFilePath: callerFilePath,
                    callerLineNumber: callerLineNumber);
            }
            else
            {
                string expectedLine = wildcardLines[i];
                Assert.That(
                    string.Equals(outputLine, expectedLine, StringComparison.Ordinal),
                    $"Output on line {i + 1} (length: {outputLine.Length}){Environment.NewLine}{outputLine}{Environment.NewLine}doesn't match line (length: {expectedLine.Length}){Environment.NewLine}{expectedLine}",
                    callerMemberName: callerMemberName,
                    callerFilePath: callerFilePath,
                    callerLineNumber: callerLineNumber);
            }
        }
    }

    public static void AssertOutputMatchesRegex(this TestHostResult testHostResult, string pattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(Regex.IsMatch(testHostResult.StandardOutput, pattern), GenerateFailedAssertionMessage(testHostResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertOutputDoesNotMatchRegex(this TestHostResult testHostResult, string pattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(!Regex.IsMatch(testHostResult.StandardOutput, pattern), GenerateFailedAssertionMessage(testHostResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertOutputContains(this TestHostResult testHostResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(testHostResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertOutputContains(this DotnetMuxerResult dotnetMuxerResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(dotnetMuxerResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(dotnetMuxerResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertOutputNotContains(this DotnetMuxerResult dotnetMuxerResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(!dotnetMuxerResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(dotnetMuxerResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertOutputRegEx(this DotnetMuxerResult dotnetMuxerResult, string pattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(Regex.IsMatch(dotnetMuxerResult.StandardOutput, pattern), GenerateFailedAssertionMessage(dotnetMuxerResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertOutputDoesNotContain(this TestHostResult testHostResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.That(!testHostResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);

    public static void AssertOutputContainsSummary(this TestHostResult testHostResult, int failed, int passed, int skipped, bool? aborted = false, int? minimumNumberOfTests = null, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        int totalTests = failed + passed + skipped;
        string result = minimumNumberOfTests != null && totalTests < minimumNumberOfTests
            ? $"Minimum expected tests policy violation, tests ran {totalTests}, minimum expected {minimumNumberOfTests}"
            : aborted is true
                ? "Aborted"
                : failed > 0
                    ? "Failed!"
                    : totalTests == 0 || totalTests == skipped
                        ? "Zero tests ran"
                        : "Passed!";

        string summaryResult = $"Test run summary: {result}";
        string summaryCounts = $"""
          total: {totalTests}
          failed: {failed}
          succeeded: {passed}
          skipped: {skipped}
        """;
        Assert.That(testHostResult.StandardOutput.Contains(summaryResult, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
        Assert.That(testHostResult.StandardOutput.Contains(summaryCounts, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult), callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber);
    }

    private static string GenerateFailedAssertionMessage(TestHostResult testHostResult)
        => $"Output of the test host is:\n{testHostResult}";

    private static string GenerateFailedAssertionMessage(DotnetMuxerResult dotnetMuxerResult)
        => $"Output of the dotnet muxer is:\n{dotnetMuxerResult}";
}
