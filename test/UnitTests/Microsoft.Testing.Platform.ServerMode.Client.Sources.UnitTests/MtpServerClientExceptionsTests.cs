// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode.Client;

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <summary>
/// Tests for the exception types raised by the MTP server client, in particular the default message carried by
/// <see cref="MtpServerConnectionClosedException"/> when constructed without an explicit message.
/// </summary>
[TestClass]
public sealed class MtpServerClientExceptionsTests
{
    [TestMethod]
    public void MtpServerConnectionClosedException_ParameterlessConstructor_UsesDescriptiveDefaultMessage()
    {
        MtpServerConnectionClosedException exception = new();

        Assert.AreEqual("The connection to the test host process was closed unexpectedly.", exception.Message);
    }
}
