// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class TrxModeHelpersTests
{
    [TestMethod]
    [DataRow(null, false, true, false)]
    [DataRow(null, true, true, true)]
    [DataRow("in-process", false, false, false)]
    [DataRow("in-process", true, false, false)]
    [DataRow("IN-PROCESS", true, false, false)]
    [DataRow("out-of-process", false, true, false)]
    [DataRow("out-of-process", true, true, true)]
    [DataRow("OUT-OF-PROCESS", true, true, true)]
    public void Mode_ControlsControllerRequestAndPipeUse(string? mode, bool hasController, bool requestController, bool usePipe)
    {
        var options = new Dictionary<string, string[]>
        {
            [TrxReportGeneratorCommandLine.TrxReportOptionName] = [],
        };
        if (mode is not null)
        {
            options.Add(TrxReportGeneratorCommandLine.TrxModeOptionName, [mode]);
        }

        if (hasController)
        {
            options.Add(PlatformCommandLineProvider.TestHostControllerPIDOptionKey, ["42"]);
        }

        var commandLineOptions = new TestCommandLineOptions(options);

        Assert.AreEqual(requestController, TrxModeHelpers.ShouldUseControllerBackedTrxGeneration(commandLineOptions));
        Assert.AreEqual(usePipe, TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(commandLineOptions));
    }
}
