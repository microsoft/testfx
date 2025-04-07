// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Extensions.VSTestBridge.Capabilities;

/// <summary>
/// The VSTest bridged test framework capabilities.
/// </summary>
public sealed class VSTestBridgeExtensionBaseCapabilities : ITrxReportCapability, IVSTestFlattenedTestNodesReportCapability, INamedFeatureCapability
{
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

    /// <inheritdoc />
    bool INamedFeatureCapability.IsSupported(string featureName) => false;
}
