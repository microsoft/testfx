// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Extensions.VSTestBridge.Capabilities;

/// <summary>
/// The VSTest bridged test framework capabilities.
/// </summary>
// NOTE: MSTest no longer uses this, as we don't want to use the vstestProvider.
// Only NUnit and Expecto use this.
// https://github.com/nunit/nunit3-vs-adapter/blob/3d0f824243aaaeb85621d3c7dddc92e7a7c45097/src/NUnitTestAdapter/TestingPlatformAdapter/TestApplicationBuilderExtensions.cs#L20
// https://github.com/YoloDev/YoloDev.Expecto.TestSdk/blob/0d1a3eadd65b605f61bb01d302f28382be76b8ac/src/YoloDev.Expecto.TestSdk/TestApplicationHelpers.fs#L16
public sealed class VSTestBridgeExtensionBaseCapabilities : IInternalVSTestBridgeTrxReportCapability, INamedFeatureCapability
{
    private const string VSTestProviderSupport = "vstestProvider";

    /// <inheritdoc />
    bool ITrxReportCapability.IsSupported => true;

    /// <summary>
    /// Gets a value indicating whether a flag indicating whether the trx report capability is enabled.
    /// </summary>
    public bool IsTrxEnabled { get; private set; }

    /// <inheritdoc />
    void ITrxReportCapability.Enable() => IsTrxEnabled = true;

    /// <inheritdoc />
    bool INamedFeatureCapability.IsSupported(string featureName) => featureName is VSTestProviderSupport;
}
