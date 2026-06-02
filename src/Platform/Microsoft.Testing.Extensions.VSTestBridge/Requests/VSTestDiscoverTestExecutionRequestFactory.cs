// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// VSTest specific implementation of Microsoft Testing Platform <see cref="ITestExecutionRequestFactory"/>.
/// </summary>
internal static class VSTestDiscoverTestExecutionRequestFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="VSTestDiscoverTestExecutionRequest"/> with the given parameters.
    /// </summary>
    /// <param name="discoverTestExecutionRequest">The discover tests request.</param>
    /// <param name="adapterExtension">The adapter extension.</param>
    /// <param name="testAssemblyPaths">The paths to test assemblies.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An instance of <see cref="VSTestDiscoverTestExecutionRequest"/>.</returns>
    public static VSTestDiscoverTestExecutionRequest CreateRequest(
        DiscoverTestExecutionRequest discoverTestExecutionRequest,
        VSTestBridgedTestFrameworkBase adapterExtension, string[] testAssemblyPaths, CancellationToken cancellationToken)
    {
        VSTestBridgeRequestContext requestContext = new(adapterExtension);

        MessageLoggerAdapter messageLogger = new(requestContext.LoggerFactory, requestContext.OutputDevice, adapterExtension, null, cancellationToken);

        RunSettingsAdapter runSettings = new(
            requestContext.CommandLineOptions,
            requestContext.FileSystem,
            requestContext.Configuration,
            requestContext.ClientInfo,
            requestContext.LoggerFactory,
            messageLogger);
        DiscoveryContextAdapter discoveryContext = new(requestContext.CommandLineOptions, runSettings, discoverTestExecutionRequest.Filter);

        TestCaseDiscoverySinkAdapter discoverySink = new(
            adapterExtension,
            discoverTestExecutionRequest.Session,
            testAssemblyPaths,
            requestContext.TestApplicationModuleInfo,
            requestContext.NamedFeatureCapability,
            requestContext.CommandLineOptions,
            requestContext.ClientInfo,
            requestContext.MessageBus,
            requestContext.LoggerFactory,
            adapterExtension.IsTrxEnabled,
            cancellationToken);

        return new(discoverTestExecutionRequest.Session, discoverTestExecutionRequest.Filter, testAssemblyPaths, discoveryContext, messageLogger, discoverySink);
    }
}
