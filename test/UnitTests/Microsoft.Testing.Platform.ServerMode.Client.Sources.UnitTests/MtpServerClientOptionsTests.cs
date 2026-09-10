// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode.Client;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <summary>
/// Tests for the default values exposed by <see cref="MtpServerClientOptions"/>. These defaults are sent to the
/// server during the initialize handshake, so a silently empty default would still "work" but would misreport
/// the client's identity/version to the server.
/// </summary>
[TestClass]
public sealed class MtpServerClientOptionsTests
{
    [TestMethod]
    public void ClientName_DefaultsToNonEmptyPackageIdentity()
    {
        MtpServerClientOptions options = new();

        Assert.AreEqual("Microsoft.Testing.Platform.ServerMode.Client", options.ClientName);
    }

    [TestMethod]
    public void ClientVersion_DefaultsToNonEmptyVersion()
    {
        MtpServerClientOptions options = new();

        Assert.AreEqual("1.0.0", options.ClientVersion);
    }
}
