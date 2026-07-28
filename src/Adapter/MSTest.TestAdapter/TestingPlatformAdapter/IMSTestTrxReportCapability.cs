// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// MSTest-native TRX report capability that also exposes whether TRX reporting has been enabled for the run.
/// This mirrors the VSTest bridge's internal capability so the native framework can determine TRX enablement
/// without depending on the bridge.
/// </summary>
internal interface IMSTestTrxReportCapability : ITrxReportCapability
{
    bool IsTrxEnabled { get; }
}
#endif
