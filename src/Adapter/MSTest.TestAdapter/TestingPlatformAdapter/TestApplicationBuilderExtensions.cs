// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Extension methods for <see cref="ITestApplicationBuilder"/>.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
public static class TestApplicationBuilderExtensions
{
    // NOTE: We intentionally use this class and not VSTestBridgeExtensionBaseCapabilities because
    // we don't want MSTest to use vstestProvider capability
    private sealed class MSTestCapabilities : IInternalVSTestBridgeTrxReportCapability
    {
        public bool IsTrxEnabled { get; private set; }

        bool ITrxReportCapability.IsSupported { get; } = true;

        void ITrxReportCapability.Enable()
            => IsTrxEnabled = true;
    }

    /// <summary>
    /// Register MSTest as the test framework and register the necessary services.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder on which to register.</param>
    /// <param name="getTestAssemblies">The function to get the test assemblies.</param>
    public static void AddMSTest(this ITestApplicationBuilder testApplicationBuilder, Func<IEnumerable<Assembly>> getTestAssemblies)
    {
        MSTestExtension extension = new();
        testApplicationBuilder.AddRunSettingsService(extension);
        testApplicationBuilder.AddTestCaseFilterService(extension);
        testApplicationBuilder.AddTestRunParametersService(extension);
        testApplicationBuilder.AddMaximumFailedTestsService(extension);
        testApplicationBuilder.AddRunSettingsEnvironmentVariableProvider(extension);
        testApplicationBuilder.RegisterTestFramework(
            serviceProvider => new TestFrameworkCapabilities(
                new MSTestCapabilities(),
                new MSTestBannerCapability(serviceProvider.GetRequiredService<IPlatformInformation>()),
                MSTestGracefulStopTestExecutionCapability.Instance),
            (capabilities, serviceProvider) => new MSTestBridgedTestFramework(extension, getTestAssemblies, serviceProvider, capabilities));
    }
}
#endif
