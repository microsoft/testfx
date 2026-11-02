// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsConstantsTests
{
    [TestMethod]
    [DataRow("true")]
    [DataRow("True")]
    [DataRow("TRUE")]
    [DataRow("tRuE")]
    public void IsRunningInAzureDevOps_ReturnsTrue_WhenTfBuildIsTrue_RegardlessOfCasing(string value)
    {
        Mock<IEnvironment> environmentMock = new();
        _ = environmentMock
            .Setup(e => e.GetEnvironmentVariable(AzureDevOpsConstants.TfBuildEnvironmentVariableName))
            .Returns(value);

        Assert.IsTrue(AzureDevOpsConstants.IsRunningInAzureDevOps(environmentMock.Object));
    }

    [TestMethod]
    [DataRow("false")]
    [DataRow("False")]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("yes")]
    [DataRow(" true")]
    [DataRow("true ")]
    [DataRow("")]
    public void IsRunningInAzureDevOps_ReturnsFalse_WhenTfBuildIsNotTrue(string value)
    {
        Mock<IEnvironment> environmentMock = new();
        _ = environmentMock
            .Setup(e => e.GetEnvironmentVariable(AzureDevOpsConstants.TfBuildEnvironmentVariableName))
            .Returns(value);

        Assert.IsFalse(AzureDevOpsConstants.IsRunningInAzureDevOps(environmentMock.Object));
    }

    [TestMethod]
    public void IsRunningInAzureDevOps_ReturnsFalse_WhenTfBuildIsNull()
    {
        Mock<IEnvironment> environmentMock = new();
        _ = environmentMock
            .Setup(e => e.GetEnvironmentVariable(AzureDevOpsConstants.TfBuildEnvironmentVariableName))
            .Returns((string?)null);

        Assert.IsFalse(AzureDevOpsConstants.IsRunningInAzureDevOps(environmentMock.Object));
    }

    [TestMethod]
    public void IsRunningInAzureDevOps_QueriesTheCorrectEnvironmentVariable()
    {
        Mock<IEnvironment> environmentMock = new(MockBehavior.Strict);
        _ = environmentMock
            .Setup(e => e.GetEnvironmentVariable("TF_BUILD"))
            .Returns("true");

        Assert.IsTrue(AzureDevOpsConstants.IsRunningInAzureDevOps(environmentMock.Object));
        environmentMock.Verify(e => e.GetEnvironmentVariable("TF_BUILD"), Times.Once);
    }

    [TestMethod]
    public void IsFeatureKnobEnabled_ReturnsTrue_WhenKnobNotSet()
    {
        TestCommandLineOptions options = new([]);
        Assert.IsTrue(AzureDevOpsConstants.IsFeatureKnobEnabled(options, AzureDevOpsCommandLineOptions.AzureDevOpsGroups));
    }

    [TestMethod]
    [DataRow("on")]
    [DataRow("On")]
    [DataRow("anything")]
    public void IsFeatureKnobEnabled_ReturnsTrue_WhenKnobIsNotOff(string value)
    {
        TestCommandLineOptions options = new(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsGroups] = [value],
        });
        Assert.IsTrue(AzureDevOpsConstants.IsFeatureKnobEnabled(options, AzureDevOpsCommandLineOptions.AzureDevOpsGroups));
    }

    [TestMethod]
    [DataRow("off")]
    [DataRow("OFF")]
    [DataRow("Off")]
    public void IsFeatureKnobEnabled_ReturnsFalse_WhenKnobIsOff(string value)
    {
        TestCommandLineOptions options = new(new Dictionary<string, string[]>
        {
            [AzureDevOpsCommandLineOptions.AzureDevOpsAnnotations] = [value],
        });
        Assert.IsFalse(AzureDevOpsConstants.IsFeatureKnobEnabled(options, AzureDevOpsCommandLineOptions.AzureDevOpsAnnotations));
    }
}
