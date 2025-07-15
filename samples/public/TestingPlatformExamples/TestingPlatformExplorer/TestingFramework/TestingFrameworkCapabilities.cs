// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Services;

namespace TestingPlatformExplorer.TestingFramework;

internal sealed class TestingFrameworkCapabilities : ITestFrameworkCapabilities
{
    public TestingFrameworkCapabilities(IPlatformInformation platformInformation)
    {
        TrxCapability = new();
        HelpCapability = new(platformInformation);
    }

    public TrxCapability TrxCapability { get; }
    
    public TestingFrameworkHelpCapability HelpCapability { get; }

    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities => [TrxCapability, HelpCapability];
}

internal sealed class TrxCapability : ITrxReportCapability
{
    public bool IsTrxEnabled { get; set; }

    public bool IsSupported => true;

    public void Enable() => IsTrxEnabled = true;
}
