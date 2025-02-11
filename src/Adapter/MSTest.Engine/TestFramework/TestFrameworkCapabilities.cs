// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace Microsoft.Testing.Framework;

internal sealed class TestFrameworkCapabilities : ITestFrameworkCapabilities
{
    private readonly ITestNodesBuilder[] _testNodesBuilders;

    public TestFrameworkCapabilities(ITestNodesBuilder[] testNodesBuilders)
        => _testNodesBuilders = testNodesBuilders;

    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities
        => new[] { new TestFrameworkCapabilitiesSet(_testNodesBuilders) };
}

internal sealed class TestFrameworkCapabilitiesSet :
    ITestNodesTreeFilterTestFrameworkCapability,
    ITrxReportCapability,
    INamedFeatureCapability
{
    private const string MultiRequestSupport = "experimental_multiRequestSupport";
    private readonly ITestNodesBuilder[] _testNodesBuilders;

    public TestFrameworkCapabilitiesSet(ITestNodesBuilder[] testNodesBuilders)
    {
        IsTrxReportCapabilitySupported = testNodesBuilders.All(x => x.HasCapability<ITrxReportCapability>());
        _testNodesBuilders = testNodesBuilders;
    }

    public bool IsTrxReportEnabled { get; private set; }

    public bool IsTrxReportCapabilitySupported { get; }

    bool ITestNodesTreeFilterTestFrameworkCapability.IsSupported { get; } = true;

    bool ITrxReportCapability.IsSupported => IsTrxReportCapabilitySupported;

    public void Enable()
    {
        IsTrxReportEnabled = true;
        foreach (ITestNodesBuilder item in _testNodesBuilders)
        {
            item.GetCapability<ITrxReportCapability>()?.Enable();
        }
    }

    bool INamedFeatureCapability.IsSupported(string featureName) => featureName == MultiRequestSupport;
}
