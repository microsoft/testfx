// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Extensions.VSTestBridge.Capabilities;

public sealed class VSTestBridgeExtensionBaseCapabilities : ITrxReportCapability, IVSTestFlattenedTestNodesReportCapability, INamedFeatureCapability
{
    private const string MultiRequestSupport = "experimental_multiRequestSupport";
    private const string VSTestProviderSupport = "vstestProvider";

    /// <inheritdoc />
    bool IVSTestFlattenedTestNodesReportCapability.IsSupported { get; } = true;

    /// <inheritdoc />
    bool ITrxReportCapability.IsSupported { get; } = true;

    /// <summary>
    /// Gets a value indicating whether a flag indicating whether the trx report capability is enabled.
    /// </summary>
    public bool IsTrxEnabled { get; private set; }

    /// <inheritdoc />
    void ITrxReportCapability.Enable() => IsTrxEnabled = true;

    bool INamedFeatureCapability.IsSupported(string featureName) => featureName is MultiRequestSupport or VSTestProviderSupport;
}
