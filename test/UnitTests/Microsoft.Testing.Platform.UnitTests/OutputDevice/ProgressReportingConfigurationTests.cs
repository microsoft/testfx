// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.OutputDevice;

[TestClass]
public sealed class ProgressReportingConfigurationTests
{
    [TestMethod]
    [DataRow("1", true)]
    [DataRow("true", true)]
    [DataRow(" TRUE ", true)]
    [DataRow("on", true)]
    [DataRow("enable", true)]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("   ", false)]
    [DataRow("0", false)]
    [DataRow("false", false)]
    [DataRow("off", false)]
    [DataRow("yes", false)]
    public void IsScrollRegionEnabled_AcceptsOnlyOnValues(string? value, bool expected)
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(e => e.GetEnvironmentVariable(ProgressReportingConfiguration.MTP_PROGRESS_SCROLL_REGION)).Returns(value);

        Assert.AreEqual(expected, ProgressReportingConfiguration.IsScrollRegionEnabled(environment.Object));
    }
}
