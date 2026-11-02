// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.OpenTelemetry;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class OpenTelemetryEnvironmentVariablesTests
{
    [TestMethod]
    [DataRow(null, true)]
    [DataRow("", true)]
    [DataRow(" ", true)]
    [DataRow("\t\r\n", true)]
    [DataRow("\u00A0", true)]
    [DataRow("a", false)]
    [DataRow(" a ", false)]
    public void IsNullOrWhiteSpace_ReturnsExpectedResult(string? value, bool expected)
        => Assert.AreEqual(expected, OpenTelemetryEnvironmentVariables.IsNullOrWhiteSpace(value));
}
