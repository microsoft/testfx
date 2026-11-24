// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests;

[TestClass]
public sealed class VSTestBridgeCapabilitiesTests
{
    [TestMethod]
    public void Enable_ActivatesTrxWithoutChangingNamedFeatureSupport()
    {
        var capabilities = new VSTestBridgeExtensionBaseCapabilities();
        var trx = (ITrxReportCapability)capabilities;
        var features = (INamedFeatureCapability)capabilities;
        Assert.IsFalse(capabilities.IsTrxEnabled);
        Assert.IsTrue(trx.IsSupported);

        trx.Enable();
        trx.Enable();

        Assert.IsTrue(capabilities.IsTrxEnabled);
        Assert.IsTrue(features.IsSupported("vstestProvider"));
        Assert.IsFalse(features.IsSupported("VSTestProvider"));
        Assert.IsFalse(features.IsSupported("unrelated"));
    }
}
