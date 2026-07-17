// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class DotnetTestConnectionTests
{
    [DataRow(null, "1")]
    [DataRow("", "1")]
    [DataRow("invalid", "1")]
    [DataRow("0", "1")]
    [DataRow("-1", "1")]
    [DataRow("2147483648", "1")]
    [DataRow("1", "1")]
    [DataRow("2", "2")]
    [DataRow("002", "2")]
    [TestMethod]
    public void GetAttemptNumber_NormalizesEnvironmentValue(string? environmentValue, string expected)
    {
        var environment = new Mock<IEnvironment>();
        environment
            .Setup(e => e.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER))
            .Returns(environmentValue);

        Assert.AreEqual(expected, DotnetTestConnection.GetAttemptNumber(environment.Object));
    }
}
