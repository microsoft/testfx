// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DotnetTestConnectionTests
{
    [TestMethod]
    [DataRow("1.0.0", "1.0.0", true, DisplayName = "SingleEntryExactMatch")]
    [DataRow("1.1.0", "1.0.0;1.1.0;1.2.0", true, DisplayName = "MultipleEntriesMiddleMatch")]
    [DataRow("1.2.0", "1.0.0;1.1.0;1.2.0", true, DisplayName = "MultipleEntriesLastMatch")]
    [DataRow("2.0.0", "1.0.0;1.1.0;1.2.0", false, DisplayName = "VersionNotInList")]
    [DataRow("1.2", "1.2.0", false, DisplayName = "PartialVersionIsNotAMatch")]
    [DataRow("1.2.0", "1.0.0;1.1.0;1.20.0", false, DisplayName = "SubstringOfAnotherEntryIsNotAMatch")]
    public void IsVersionCompatible_ReturnsExpectedResult(string protocolVersion, string supportedProtocolVersions, bool expected)
    {
        bool actual = DotnetTestConnection.IsVersionCompatible(protocolVersion, supportedProtocolVersions);

        Assert.AreEqual(expected, actual);
    }
}
