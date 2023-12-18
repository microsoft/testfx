// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

internal static class AcceptanceAssert
{
    public static void AssertExitCodeIs(this TestHostResult testHostResult, int exitCode)
        => Assert.That(exitCode == testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult));

    public static void AssertExitCodeIsNot(this TestHostResult testHostResult, int exitCode)
        => Assert.That(exitCode != testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult));

    public static void AssertOutputMatchesRegex(this TestHostResult testHostResult, string pattern)
        => Assert.That(Regex.IsMatch(testHostResult.StandardOutput, pattern), GenerateFailedAssertionMessage(testHostResult));

    public static void AssertOutputDoesNotMatchRegex(this TestHostResult testHostResult, string pattern)
        => Assert.That(!Regex.IsMatch(testHostResult.StandardOutput, pattern), GenerateFailedAssertionMessage(testHostResult));

    public static void AssertOutputContains(this TestHostResult testHostResult, string value)
        => Assert.That(testHostResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult));

    public static void AssertOutputContains(this DotnetMuxerResult dotnetMuxerResult, string value)
        => Assert.That(dotnetMuxerResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(dotnetMuxerResult));

    public static void AssertOutputNotContains(this DotnetMuxerResult dotnetMuxerResult, string value)
        => Assert.That(!dotnetMuxerResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(dotnetMuxerResult));

    public static void AssertOutputRegEx(this DotnetMuxerResult dotnetMuxerResult, string pattern)
        => Assert.That(Regex.IsMatch(dotnetMuxerResult.StandardOutput, pattern), GenerateFailedAssertionMessage(dotnetMuxerResult));

    public static void AssertOutputDoesNotContain(this TestHostResult testHostResult, string value)
        => Assert.That(!testHostResult.StandardOutput.Contains(value, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult));

    private static string GenerateFailedAssertionMessage(TestHostResult testHostResult)
        => $"Output of the test host is:\n{testHostResult}";

    private static string GenerateFailedAssertionMessage(DotnetMuxerResult dotnetMuxerResult)
        => $"Output of the dotnet muxer is:\n{dotnetMuxerResult}";
}
