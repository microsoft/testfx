// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

internal static class AcceptanceAssert
{
    public static void HasExitCode(int exitCode, TestHostResult testHostResult)
        => Assert.That(exitCode == testHostResult.ExitCode, GenerateFailedAssertionMessage(testHostResult));

    public static void OutputMatchesRegex(string pattern, TestHostResult testHostResult)
        => Assert.That(Regex.IsMatch(testHostResult.StandardOutput, pattern), GenerateFailedAssertionMessage(testHostResult));

    public static void OutputDoesNotMatchRegex(string pattern, TestHostResult testHostResult)
        => Assert.That(!Regex.IsMatch(testHostResult.StandardOutput, pattern), GenerateFailedAssertionMessage(testHostResult));

    public static void OutputContains(string pattern, TestHostResult testHostResult)
        => Assert.That(testHostResult.StandardOutput.Contains(pattern, StringComparison.Ordinal), GenerateFailedAssertionMessage(testHostResult));

    private static string GenerateFailedAssertionMessage(TestHostResult testHostResult)
        => $"Output of the test host is:\n{testHostResult}";
}
