// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class WellKnownClientsTests
{
    // These identities are exchanged with external clients (Visual Studio sends its name in the JSON-RPC
    // initialize request, and the console host reports its own). Consumers compare against the public
    // constants, so a comparison that reads the constant on both sides cannot detect a changed value.
    [TestMethod]
    [DataRow(nameof(WellKnownClients.TestingPlatformConsole), "testingplatform-console")]
    [DataRow(nameof(WellKnownClients.VisualStudio), "Microsoft Visual Studio Testing Platform Client")]
    public void WellKnownClients_ExposeTheIdentitiesExternalClientsRecognize(string client, string expectedIdentity)
    {
        string actual = client switch
        {
            nameof(WellKnownClients.TestingPlatformConsole) => WellKnownClients.TestingPlatformConsole,
            nameof(WellKnownClients.VisualStudio) => WellKnownClients.VisualStudio,
            _ => throw new InvalidOperationException($"Unexpected client '{client}'."),
        };

        Assert.AreEqual(expectedIdentity, actual);
    }
}
