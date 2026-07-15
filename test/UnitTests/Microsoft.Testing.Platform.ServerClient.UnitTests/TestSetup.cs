// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.ServerClient.UnitTests;

/// <summary>
/// Assembly-level setup for the ServerClient unit tests.
/// </summary>
[TestClass]
public static class TestSetup
{
    /// <summary>
    /// Registers the client-side serializers exactly once, before any formatter is created.
    /// </summary>
    /// <remarks>
    /// <see cref="FormatterUtilities.CreateFormatter"/> snapshots the registered serializer/deserializer set
    /// when it is constructed (this matters for the System.Text.Json formatter on .NET). Both the client under
    /// test and <see cref="FakeMtpServer"/> create formatters, so the client serializers must be installed into
    /// the shared <see cref="SerializerUtilities"/> tables before the first <c>CreateFormatter</c> call. Running
    /// this in <see cref="AssemblyInitializeAttribute"/> guarantees that ordering for every test.
    /// </remarks>
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext testContext)
    {
        _ = testContext;
        SerializerUtilities.RegisterClientSerializers();
    }
}
