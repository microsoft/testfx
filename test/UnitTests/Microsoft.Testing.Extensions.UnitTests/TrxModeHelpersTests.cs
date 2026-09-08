// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxModeHelpersTests
{
    [TestMethod]
    public void ShouldUseOutOfProcessTrxGeneration_IsFalse_WhenTestHostControllerPidOptionIsNotSet()
    {
        // The child test host only recovers via the controller when it can see that it was actually
        // launched by one (the platform sets internal-testhostcontroller-pid on that child process).
        // Without that option, ShouldUseOutOfProcessTrxGeneration must not assume controller-backed mode
        // even if the current platform generally supports it.
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TrxReport.Abstractions.TrxReportGeneratorCommandLine.TrxReportOptionName] = [],
        });

        Assert.IsFalse(TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(commandLineOptions));
    }

    [TestMethod]
    public void ShouldUseOutOfProcessTrxGeneration_ReflectsControllerSupport_WhenTestHostControllerPidOptionIsSet()
    {
        // Once the platform has actually placed this process under a controller (the PID option is
        // present), whether TRX recovers out-of-process should track platform support alone: the child
        // no longer needs to know which extension (TRX, HangDump, --timeout, ...) caused the isolation.
        // This test always runs on a platform that supports test-host controllers (Windows/Linux/macOS),
        // so assert the fixed expected value rather than comparing against IsTestHostControllerSupported
        // itself, which would make the assertion self-referential and unable to catch a regression that
        // breaks both sides identically.
        var commandLineOptions = new TestCommandLineOptions(new Dictionary<string, string[]>
        {
            [TrxReport.Abstractions.TrxReportGeneratorCommandLine.TrxReportOptionName] = [],
            [PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["42"],
        });

        Assert.IsTrue(TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(commandLineOptions));
    }
}
