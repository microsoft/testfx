// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

using Moq;

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
        Assert.AreEqual(usePipe, TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(commandLineOptions, Mock.Of<IEnvironment>()));
    }

    [TestMethod]
    [DataRow("in-process", null, false, false, false)]
    [DataRow("in-process", null, true, false, false)]
    [DataRow("out-of-process", null, false, true, false)]
    [DataRow("out-of-process", null, true, true, true)]
    [DataRow("in-process", "out-of-process", true, true, true)]
    [DataRow("out-of-process", "in-process", true, false, false)]
    public void Mode_UsesConfiguredDefaultUnlessExplicitValueIsSet(string defaultMode, string? explicitMode, bool hasController, bool requestController, bool usePipe)
    {
        var options = new Dictionary<string, string[]>
        {
            [TrxReportGeneratorCommandLine.TrxReportOptionName] = [],
        };
        if (explicitMode is not null)
        {
            options.Add(TrxReportGeneratorCommandLine.TrxModeOptionName, [explicitMode]);
        }

        if (hasController)
        {
            options.Add(PlatformCommandLineProvider.TestHostControllerPIDOptionKey, ["42"]);
        }

        var commandLineOptions = new TestCommandLineOptions(
            options,
            new() { [TrxReportGeneratorCommandLine.TrxModeOptionName] = [defaultMode] });

        Assert.AreEqual(requestController, TrxModeHelpers.ShouldUseControllerBackedTrxGeneration(commandLineOptions));
        Assert.AreEqual(usePipe, TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(commandLineOptions, Mock.Of<IEnvironment>()));
    }

    [TestMethod]
    [DataRow("in-process", "out-of-process", false)]
    [DataRow("out-of-process", "in-process", true)]
    public void Mode_ControllerChoiceOverridesChildDefault(string controllerMode, string childDefault, bool usePipe)
    {
        var options = new TestCommandLineOptions(
            new()
            {
                [TrxReportGeneratorCommandLine.TrxReportOptionName] = [],
                [PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["42"],
            },
            new() { [TrxReportGeneratorCommandLine.TrxModeOptionName] = [childDefault] });
        var environment = new Mock<IEnvironment>();
        environment.Setup(value => value.GetEnvironmentVariable(TrxModeHelpers.GetControllerModeEnvironmentVariableName("42")))
            .Returns(controllerMode);

        Assert.AreEqual(controllerMode, TrxModeHelpers.GetEffectiveMode(options, environment.Object));
        Assert.AreEqual(usePipe, TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(options, environment.Object));
    }

    [TestMethod]
    public void Mode_DoesNotReuseAnAncestorControllersChoice()
    {
        var options = new TestCommandLineOptions(new()
        {
            [PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["42"],
            [TrxReportGeneratorCommandLine.TrxModeOptionName] = ["out-of-process"],
        });
        var environment = new Mock<IEnvironment>();
        environment.Setup(value => value.GetEnvironmentVariable(TrxModeHelpers.GetControllerModeEnvironmentVariableName("99")))
            .Returns("in-process");

        Assert.AreEqual("out-of-process", TrxModeHelpers.GetEffectiveMode(options, environment.Object));
        Assert.IsTrue(TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(options, environment.Object));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("invalid")]
    public void Mode_InvalidControllerChoiceIsRejected(string controllerMode)
    {
        var options = new TestCommandLineOptions(new()
        {
            [PlatformCommandLineProvider.TestHostControllerPIDOptionKey] = ["42"],
        });
        var environment = new Mock<IEnvironment>();
        environment.Setup(value => value.GetEnvironmentVariable(TrxModeHelpers.GetControllerModeEnvironmentVariableName("42")))
            .Returns(controllerMode);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => TrxModeHelpers.GetEffectiveMode(options, environment.Object));

        Assert.AreEqual(TrxReport.Resources.ExtensionResources.TrxModeInvalidValue, exception.Message);
    }
}
