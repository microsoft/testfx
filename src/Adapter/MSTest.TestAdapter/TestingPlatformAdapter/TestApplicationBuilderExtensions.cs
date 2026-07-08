// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Extension methods for <see cref="ITestApplicationBuilder"/>.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
public static class TestApplicationBuilderExtensions
{
    // NOTE: We intentionally do not use the bridge's VSTestBridgeExtensionBaseCapabilities because we don't want
    // MSTest to use the vstestProvider capability. This implements BOTH the bridge's TRX capability interface (read
    // by MSTestBridgedTestFramework, the current default) and MSTest's native one (read by MSTestTestFramework), so
    // both frameworks observe the same TRX enablement.
    private sealed class MSTestCapabilities : IInternalVSTestBridgeTrxReportCapability, IMSTestTrxReportCapability
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

        // Register MSTest's own command-line options, runsettings configuration source and environment-variable
        // provider natively (identical option names/descriptions to the VSTest bridge), so both the native and the
        // bridged framework read them by name.
        if (testApplicationBuilder is TestApplicationBuilder concreteBuilder)
        {
            concreteBuilder.Configuration.AddConfigurationSource(() => new MSTestRunSettingsConfigurationProvider(extension, new SystemFileSystem()));
        }

        testApplicationBuilder.CommandLine.AddProvider(() => new MSTestRunSettingsCommandLineOptionsProvider(extension));
        testApplicationBuilder.CommandLine.AddProvider(() => new MSTestTestCaseFilterCommandLineOptionsProvider(extension));
        testApplicationBuilder.CommandLine.AddProvider(() => new MSTestTestRunParametersCommandLineOptionsProvider(extension));
        testApplicationBuilder.AddMaximumFailedTestsService(extension);
        testApplicationBuilder.TestHostControllers.AddEnvironmentVariableProvider(serviceProvider
            => new MSTestRunSettingsEnvironmentVariableProvider(extension, serviceProvider.GetCommandLineOptions(), serviceProvider.GetFileSystem(), serviceProvider.GetEnvironment()));

        // When the experimental native MTP integration is enabled, MSTest plugs into Microsoft.Testing.Platform
        // directly (no VSTest bridge object model on the request path). Off by default, so shipping behavior is
        // the bridged framework.
        bool useNativeMtp = Environment.GetEnvironmentVariable("MSTEST_EXPERIMENTAL_NATIVE_MTP") is "1" or "true" or "True";

        testApplicationBuilder.RegisterTestFramework(
            serviceProvider => new TestFrameworkCapabilities(
                new MSTestCapabilities(),
                new MSTestBannerCapability(serviceProvider.GetRequiredService<IPlatformInformation>()),
                MSTestGracefulStopTestExecutionCapability.Instance),
            (capabilities, serviceProvider) => useNativeMtp
                ? new MSTestTestFramework(extension, getTestAssemblies, serviceProvider, capabilities)
                : new MSTestBridgedTestFramework(extension, getTestAssemblies, serviceProvider, capabilities));
    }
}
#endif
