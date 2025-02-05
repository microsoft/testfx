﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// VSTest specific implementation of Microsoft Testing Platform <see cref="ITestExecutionRequestFactory"/>.
/// </summary>
public sealed class VSTestRunTestExecutionRequestFactory : ITestExecutionRequestFactory
{
    [Obsolete]
    private VSTestRunTestExecutionRequestFactory()
    {
    }

    // This class is never instantiated.
    // It's not possible to reach this method.
    // The class should probably be static and not needing to implement the interface.
    [Obsolete]
    Task<TestExecutionRequest> ITestExecutionRequestFactory.CreateRequestAsync(Platform.TestHost.TestSessionContext session)
        => throw ApplicationStateGuard.Unreachable();

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

        ITestApplicationModuleInfo testApplicationModuleInfo = serviceProvider.GetTestApplicationModuleInfo();
        IMessageBus messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        IOutputDevice outputDevice = serviceProvider.GetOutputDevice();
        FrameworkHandlerAdapter frameworkHandlerAdapter = new(adapterExtension, runTestExecutionRequest.Session, clientInfo, testAssemblyPaths,
            testApplicationModuleInfo, loggerFactory, messageBus, outputDevice, adapterExtension.IsTrxEnabled, cancellationToken);

        RunSettingsAdapter runSettings = new(commandLineOptions, fileSystem, configuration, clientInfo, loggerFactory, frameworkHandlerAdapter);
        RunContextAdapter runContext = new(commandLineOptions, runSettings, runTestExecutionRequest.Filter);

        return new(runTestExecutionRequest.Session, runTestExecutionRequest.Filter, testAssemblyPaths, runContext, frameworkHandlerAdapter);
    }
}
