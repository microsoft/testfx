// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace TestFramework.ForTestingMSTest;

internal sealed class TestFrameworkCapabilities : ITestFrameworkCapabilities
{
    private readonly ITrxReportCapability _trxReportCapability;
    private readonly ITestFrameworkCapability _treeNodeFilterCapability;

    public TestFrameworkCapabilities(ITrxReportCapability trxReportCapability)
    {
        _trxReportCapability = trxReportCapability;
        _treeNodeFilterCapability = new TestNodesTreeFilterTestFrameworkCapability();
    }

    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities
        => [_treeNodeFilterCapability, _trxReportCapability];
}

internal sealed class TestNodesTreeFilterTestFrameworkCapability : ITestNodesTreeFilterTestFrameworkCapability
{
    bool ITestNodesTreeFilterTestFrameworkCapability.IsSupported => true;
}
