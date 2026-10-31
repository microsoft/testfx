// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class SuspendCodeCoverageTests : TestContainer
{
    public void NestedScopesRestoreOriginalValueAndRepeatedDisposeDoesNotOverwriteLaterChanges()
    {
        const string variable = "__VANGUARD_SUSPEND_INSTRUMENT__";
        string? original = Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process);
        try
        {
            foreach (string? previous in new[] { null, "previous collector value" })
            {
                Environment.SetEnvironmentVariable(variable, previous, EnvironmentVariableTarget.Process);
                using SuspendCodeCoverage outer = new();
                Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process).Should().Be("TRUE");
                using (SuspendCodeCoverage inner = new())
                {
                    Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process).Should().Be("TRUE");
                }

                Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process).Should().Be("TRUE");
                outer.Dispose();
                Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process).Should().Be(previous);
                Environment.SetEnvironmentVariable(variable, "later legitimate value", EnvironmentVariableTarget.Process);
                outer.Dispose();
                Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process).Should().Be("later legitimate value");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original, EnvironmentVariableTarget.Process);
        }
    }
}
#endif
