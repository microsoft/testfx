// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

internal static class AcceptanceAssert
{
    /// <summary>
    /// Regex fragment (including the surrounding parentheses) that matches a test duration as rendered in the
    /// platform output, e.g. <c>(040ms)</c>, <c>(1s 040ms)</c>, <c>(2m 03s 040ms)</c> or <c>(1h 02m 03s 040ms)</c>.
    /// </summary>
    /// <remarks>
    /// The duration format (see <c>HumanReadableDurationFormatter</c> in Microsoft.Testing.Platform) grows additional
    /// leading parts (seconds, minutes, hours, days) as the elapsed time increases. A naive <c>\(\d+ms\)</c> pattern
    /// only matches sub-second durations and is a frequent source of timing flakiness on slower machines (often macOS,
    /// sometimes Windows) where a test that usually runs in a few hundred milliseconds occasionally takes over a second
    /// and is rendered as <c>(1s 040ms)</c>. Always use this constant when asserting on output that contains a duration
    /// instead of hard-coding <c>\(\d+ms\)</c>.
    /// </remarks>
    public const string DurationPattern = @"\((?:\d+d )?(?:\d+h )?(?:\d+m )?(?:\d+s )?\d+ms\)";

    public static void AssertExitCodeIs(this TestHostResult testHostResult, int exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.AreEqual(exitCode, testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertExitCodeIs(this TestHostResult testHostResult, ExitCode exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => AssertExitCodeIs(testHostResult, (int)exitCode, callerMemberName, callerFilePath, callerLineNumber);

    public static void AssertExitCodeIsNot(this TestHostResult testHostResult, int exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.AreNotEqual(exitCode, testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertExitCodeIsNot(this TestHostResult testHostResult, ExitCode exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => AssertExitCodeIsNot(testHostResult, (int)exitCode, callerMemberName, callerFilePath, callerLineNumber);

    /// <summary>
    /// Ensure that the output matches the given pattern. The pattern can use `*` to mean any character, it is internally replaced by `.*` and matched as regex.
    /// If you have lines in the pattern that are optional then you can output `###SKIP###` and the line in pattern will be skipped. This allows matching lines that are present only when some condition is met.
    /// The output is matched from the first line. We do not ensure that no more lines are output after the pattern ends.
    /// </summary>
    public static void AssertOutputMatchesLines(this TestHostResult testHostResult, string linesWildcardPattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        string[] wildcardLines = linesWildcardPattern.Split(Environment.NewLine);
        int j = 0;
        for (int i = 0; i < wildcardLines.Length; i++)
        {
            string outputLine = testHostResult.StandardOutputLines[j];

            if (wildcardLines[i].Contains("###SKIP###"))
            {
                continue;
            }

            if (wildcardLines[i].Contains('*'))
            {
                string matchingPatternLine =
                    "^"
                    + Regex.Escape(wildcardLines[i]).Replace("\\*", ".*")
                    + "$";

                Assert.IsTrue(
                    Regex.IsMatch(outputLine, matchingPatternLine, RegexOptions.Singleline),
                    $"Output on line {j + 1}{Environment.NewLine}{outputLine}{Environment.NewLine}doesn't match pattern{Environment.NewLine}{matchingPatternLine}{Environment.NewLine}Standard output:{Environment.NewLine}{testHostResult.StandardOutput}{Environment.NewLine}for member '{callerMemberName}' at line {callerLineNumber} of file '{callerFilePath}'.");
            }
            else
            {
                string expectedLine = wildcardLines[i];
                Assert.AreEqual(expectedLine, outputLine, StringComparer.Ordinal,
                    $"Output on line {j + 1} (length: {outputLine.Length}){Environment.NewLine}{outputLine}{Environment.NewLine}doesn't match line (length: {expectedLine.Length}){Environment.NewLine}{expectedLine}{Environment.NewLine}Standard output:{Environment.NewLine}{testHostResult.StandardOutput}{Environment.NewLine}for member '{callerMemberName}' at line {callerLineNumber} of file '{callerFilePath}'.");
            }

            j++;
        }
    }

    /// <summary>
    /// Ensure that the output matches the given pattern. The pattern can use `*` to mean any character, it is internally replaced by `.*` and matched as regex.
    /// If you have lines in the pattern that are optional then you can output `###SKIP###` and the line in pattern will be skipped. This allows matching lines that are present only when some condition is met.
    /// The output is matched from the first line. We do not ensure that no more lines are output after the pattern ends.
    /// </summary>
    public static void AssertOutputMatchesRegexLines(this TestHostResult testHostResult, string linesRegexPattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        string[] patternLines = linesRegexPattern.Split(Environment.NewLine);
        int j = 0;
        for (int i = 0; i < patternLines.Length; i++)
        {
            if (patternLines[i].Contains("###SKIP###"))
            {
                continue;
            }

            string outputLine = testHostResult.StandardOutputLines[j];
            Assert.IsTrue(
                Regex.IsMatch(outputLine, patternLines[i], RegexOptions.Singleline),
                $"Output on line {j + 1}{Environment.NewLine}{outputLine}{Environment.NewLine}doesn't match pattern{Environment.NewLine}{patternLines[i]}{Environment.NewLine}Standard output:{Environment.NewLine}{testHostResult.StandardOutput}{Environment.NewLine}for member '{callerMemberName}' at line {callerLineNumber} of file '{callerFilePath}'.");
            j++;
        }
    }

    public static void AssertOutputMatchesRegex(this TestHostResult testHostResult, [StringSyntax("Regex")] string pattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.IsTrue(Regex.IsMatch(testHostResult.StandardOutput, pattern), GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertOutputMatchesRegex(this TestHostResult testHostResult, [StringSyntax("Regex")] string pattern, RegexOptions regexOptions, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.IsTrue(Regex.IsMatch(testHostResult.StandardOutput, pattern, regexOptions), GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertOutputDoesNotMatchRegex(this TestHostResult testHostResult, [StringSyntax("Regex")] string pattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.IsFalse(Regex.IsMatch(testHostResult.StandardOutput, pattern), GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertOutputContains(this TestHostResult testHostResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.Contains(value, testHostResult.StandardOutput, StringComparison.Ordinal, GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertExitCodeIs(this DotnetMuxerResult testHostResult, int exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.AreEqual(exitCode, testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertExitCodeIs(this DotnetMuxerResult testHostResult, ExitCode exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => AssertExitCodeIs(testHostResult, (int)exitCode, callerMemberName, callerFilePath, callerLineNumber);

    public static void AssertExitCodeIsNot(this DotnetMuxerResult testHostResult, int exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.AreNotEqual(exitCode, testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertExitCodeIsNot(this DotnetMuxerResult testHostResult, ExitCode exitCode, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => AssertExitCodeIsNot(testHostResult, (int)exitCode, callerMemberName, callerFilePath, callerLineNumber);

    public static void AssertOutputContains(this DotnetMuxerResult dotnetMuxerResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.Contains(value, dotnetMuxerResult.StandardOutput, StringComparison.Ordinal, GenerateFailedAssertionMessage(dotnetMuxerResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertOutputDoesNotContain(this DotnetMuxerResult dotnetMuxerResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.IsFalse(dotnetMuxerResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(dotnetMuxerResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertOutputMatchesRegex(this DotnetMuxerResult dotnetMuxerResult, [StringSyntax("Regex")] string pattern, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.IsTrue(Regex.IsMatch(dotnetMuxerResult.StandardOutput, pattern), GenerateFailedAssertionMessage(dotnetMuxerResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    public static void AssertOutputDoesNotContain(this TestHostResult testHostResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
        => Assert.IsFalse(testHostResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

    /// <summary>
    /// Asserts that the test host output contains the given value after Unicode normalization (FormC) and
    /// non-breaking space (U+00A0) replacement. Use this instead of <see cref="AssertOutputContains"/> when
    /// comparing localized strings that may use different normalization forms or typographic spacing conventions.
    /// </summary>
    public static void AssertOutputContainsNormalized(this TestHostResult testHostResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        string normalizedOutput = NormalizeForLocalization(testHostResult.StandardOutput);
        string normalizedValue = NormalizeForLocalization(value);
        Assert.IsTrue(
            normalizedOutput.Contains(normalizedValue, StringComparison.Ordinal),
            $"Output does not contain '{value}'.{Environment.NewLine}Output:{Environment.NewLine}{testHostResult.StandardOutput}");
    }

    /// <summary>
    /// Asserts that the test host output does not contain the given value after Unicode normalization (FormC) and
    /// non-breaking space (U+00A0) replacement. Use this instead of <see cref="AssertOutputDoesNotContain"/> when
    /// comparing localized strings that may use different normalization forms or typographic spacing conventions.
    /// </summary>
    public static void AssertOutputDoesNotContainNormalized(this TestHostResult testHostResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
    {
        string normalizedOutput = NormalizeForLocalization(testHostResult.StandardOutput);
        string normalizedValue = NormalizeForLocalization(value);
        Assert.IsFalse(
            normalizedOutput.Contains(normalizedValue, StringComparison.Ordinal),
            $"Output should not contain '{value}'.{Environment.NewLine}Output:{Environment.NewLine}{testHostResult.StandardOutput}");
    }

    public static void AssertStandardErrorContains(this TestHostResult testHostResult, string value, [CallerMemberName] string? callerMemberName = null, [CallerFilePath] string? callerFilePath = null, [CallerLineNumber] int callerLineNumber = 0)
       => Assert.Contains(value, testHostResult.StandardError, StringComparison.Ordinal, GenerateFailedAssertionMessage(testHostResult, callerMemberName: callerMemberName, callerFilePath: callerFilePath, callerLineNumber: callerLineNumber));

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
        Assert.IsTrue(testHostResult.StandardOutput.Contains(summaryResult, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult, callerMemberName, callerFilePath, callerLineNumber));
        Assert.IsTrue(testHostResult.StandardOutput.Contains(summaryCounts, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult, callerMemberName, callerFilePath, callerLineNumber));
    }

    private static string GenerateFailedAssertionMessage(TestHostResult testHostResult, string? callerMemberName, string? callerFilePath, int callerLineNumber, [CallerMemberName] string? assertCallerMemberName = null)
        => $"Expression '{assertCallerMemberName}' failed for member '{callerMemberName}' at line {callerLineNumber} of file '{callerFilePath}'. Output of the test host is:{Environment.NewLine}{testHostResult}";

    private static string GenerateFailedAssertionMessage(DotnetMuxerResult dotnetMuxerResult, string? callerMemberName, string? callerFilePath, int callerLineNumber, [CallerMemberName] string? assertCallerMemberName = null)
        => $"Expression '{assertCallerMemberName}' failed for member '{callerMemberName}' at line {callerLineNumber} of file '{callerFilePath}'. Output of the dotnet muxer is:{Environment.NewLine}{dotnetMuxerResult}";

    // Localized resource strings may use different Unicode normalization forms (NFC vs NFD) than C# string literals.
    // French locale also uses non-breaking space (U+00A0) before colons per typographic convention.
    private static string NormalizeForLocalization(string text)
        => text.Normalize(NormalizationForm.FormC).Replace('\u00A0', ' ');
}
