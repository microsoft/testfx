// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

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
        IServiceProvider serviceProvider = adapterExtension.ServiceProvider;
        IConfiguration configuration = serviceProvider.GetConfiguration();
        ICommandLineOptions commandLineOptions = serviceProvider.GetRequiredService<ICommandLineOptions>();
        ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        IFileSystem fileSystem = serviceProvider.GetFileSystem();
        IClientInfo clientInfo = serviceProvider.GetClientInfo();

        FrameworkHandlerAdapter frameworkHandlerAdapter = new(
            adapterExtension,
            runTestExecutionRequest.Session,
            testAssemblyPaths,
            serviceProvider.GetTestApplicationModuleInfo(),
            serviceProvider.GetTestFrameworkCapabilities().GetCapability<INamedFeatureCapability>(),
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetClientInfo(),
            serviceProvider.GetMessageBus(),
            serviceProvider.GetOutputDevice(),
            loggerFactory,
            adapterExtension.IsTrxEnabled,
            cancellationToken);

        RunSettingsAdapter runSettings = new(commandLineOptions, fileSystem, configuration, clientInfo, loggerFactory, frameworkHandlerAdapter);
        RunContextAdapter runContext = new(commandLineOptions, runSettings, runTestExecutionRequest.Filter);

        return new(runTestExecutionRequest.Session, runTestExecutionRequest.Filter, testAssemblyPaths, runContext, frameworkHandlerAdapter);
    }
}
