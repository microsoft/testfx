// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

internal static class LegacyAcceptanceAssert
{
    public static void Passed(TestHostResult result, params string[] displayNames)
        => Outcomes(result, "passed", displayNames);

    public static void Failed(TestHostResult result, params string[] displayNames)
        => Outcomes(result, "failed", displayNames);

    public static void OutputContains(TestHostResult result, params string[] expectedValues)
    {
        foreach (string expectedValue in expectedValues)
        {
            result.AssertOutputContains(expectedValue);
        }
    }

    public static void OutcomeCount(TestHostResult result, string outcome, string displayName, int expectedCount)
    {
        int actualCount = result.StandardOutputLines.Count(
            line => line.Contains($"{outcome} {displayName}", StringComparison.Ordinal));

        Assert.AreEqual(
            expectedCount,
            actualCount,
            $"Expected '{outcome} {displayName}' to occur {expectedCount} time(s).{Environment.NewLine}{result}");
    }

    private static void Outcomes(TestHostResult result, string outcome, params string[] displayNames)
    {
        foreach (string displayName in displayNames)
        {
            result.AssertOutputContains($"{outcome} {displayName}");
        }
    }
}
