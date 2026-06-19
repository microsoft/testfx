// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.TerminalReporterContract.UnitTests;

/// <summary>
/// Verifies the shared terminal-reporter source, compiled into this independent assembly via
/// <c>TerminalReporterContract.props</c>.
/// </summary>
/// <remarks>
/// This project does not reference Microsoft.Testing.Platform's terminal reporter types; it compiles the same
/// source files (reporter + rendering + state + the small platform abstractions) and the <c>TerminalResources</c>
/// resx into its own assembly. The fact that this assembly compiles at all is the proof that the reporter is
/// self-contained and consumable with a single source of truth (the same way dotnet/sdk's 'dotnet test' will
/// consume it), instead of being hard-forked.
/// </remarks>
[TestClass]
public sealed class TerminalReporterContractTests
{
    [TestMethod]
    public void TerminalResources_AreCompiledIntoThisAssembly()
    {
        // Resolves from TerminalResources.resx GenerateSource'd into THIS assembly, not from
        // Microsoft.Testing.Platform. A missing/renamed string would fail the build, not just this test.
        Assert.IsFalse(string.IsNullOrEmpty(TerminalResources.TestRunSummary));
        Assert.IsFalse(string.IsNullOrEmpty(TerminalResources.Passed));
    }

    [TestMethod]
    public void SharedTerminalTypes_AreUsable()
    {
        Assert.IsNotEmpty(Enum.GetValues<TerminalColor>());

        string rendered = HumanReadableDurationFormatter.Render(TimeSpan.FromSeconds(1));
        Assert.Contains("1s", rendered);
    }
}
