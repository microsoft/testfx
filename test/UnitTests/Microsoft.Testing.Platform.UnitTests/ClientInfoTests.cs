// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ClientInfoTests
{
    [TestMethod]
    public void Constructor_ExposesIdAndVersion()
    {
        ClientInfo clientInfo = new("vs-client", "17.12.3");

        Assert.AreEqual("vs-client", clientInfo.Id);
        Assert.AreEqual("17.12.3", clientInfo.Version);
    }
}
