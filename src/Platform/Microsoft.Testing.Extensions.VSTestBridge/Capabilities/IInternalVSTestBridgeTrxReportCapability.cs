// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.Testing.Extensions.VSTestBridge.Capabilities;

internal interface IInternalVSTestBridgeTrxReportCapability : ITrxReportCapability
{
    bool IsTrxEnabled { get; }
}
