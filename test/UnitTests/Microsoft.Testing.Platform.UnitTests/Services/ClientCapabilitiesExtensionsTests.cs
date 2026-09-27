// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

#pragma warning disable TPEXP // IClientCapabilities and ClientCapabilitiesExtensions are experimental.
[TestClass]
public sealed class ClientCapabilitiesExtensionsTests
{
    [TestMethod]
    public void IsStateful_UndeclaredCapability_DefaultsToFalse()
    {
        IClientCapabilities capabilities = new ClientCapabilitiesService(DeclaredIsStateful: null);

        Assert.IsFalse(capabilities.IsStateful);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void GetIsStateful_ForwardsCustomImplementation(bool isStateful)
    {
        IClientCapabilities capabilities = new CustomClientCapabilities(isStateful);

        Assert.AreEqual(isStateful, capabilities.GetIsStateful());
    }

    private sealed class CustomClientCapabilities(bool isStateful) : IClientCapabilities
    {
        public bool IsStateful { get; } = isStateful;
    }
}
#pragma warning restore TPEXP
