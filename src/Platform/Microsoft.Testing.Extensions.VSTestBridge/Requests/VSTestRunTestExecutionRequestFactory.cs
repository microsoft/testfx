// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.Requests;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// VSTest specific implementation of Microsoft Testing Platform <see cref="ITestExecutionRequestFactory"/>.
/// </summary>
internal static class VSTestRunTestExecutionRequestFactory
{
    /// <summary>
    /// Helper method to convert a <see cref="RunTestExecutionRequest"/> to a <see cref="VSTestRunTestExecutionRequest"/>.
    /// </summary>
    /// <param name="runTestExecutionRequest">The request to convert.</param>
    /// <param name="adapterExtension">The test adapter extension.</param>
    /// <param name="testAssemblyPaths">The list of assemblies to test.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public static VSTestRunTestExecutionRequest CreateRequest(
        RunTestExecutionRequest runTestExecutionRequest,
        VSTestBridgedTestFrameworkBase adapterExtension,
        string[] testAssemblyPaths,
        CancellationToken cancellationToken)
    {
        VSTestBridgeRequestContext requestContext = new(adapterExtension);

        FrameworkHandlerAdapter frameworkHandlerAdapter = new(
            adapterExtension,
            runTestExecutionRequest.Session,
            testAssemblyPaths,
            requestContext.TestApplicationModuleInfo,
            requestContext.NamedFeatureCapability,
            requestContext.CommandLineOptions,
            requestContext.ClientInfo,
            requestContext.MessageBus,
            requestContext.OutputDevice,
            requestContext.LoggerFactory,
            adapterExtension.IsTrxEnabled,
            cancellationToken);

        RunSettingsAdapter runSettings = new(
            requestContext.CommandLineOptions,
            requestContext.FileSystem,
            requestContext.Configuration,
            requestContext.ClientInfo,
            requestContext.LoggerFactory,
            frameworkHandlerAdapter);
        RunContextAdapter runContext = new(requestContext.CommandLineOptions, runSettings, runTestExecutionRequest.Filter, adapterExtension.UseFullyQualifiedNameAsTestNodeUid);

        return new(runTestExecutionRequest.Session, runTestExecutionRequest.Filter, testAssemblyPaths, runContext, frameworkHandlerAdapter);
    }
}
