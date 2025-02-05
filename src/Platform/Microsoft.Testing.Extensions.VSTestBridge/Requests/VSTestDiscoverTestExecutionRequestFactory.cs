// Copyright (c) Microsoft Corporation. All rights reserved.
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
public sealed class VSTestDiscoverTestExecutionRequestFactory : ITestExecutionRequestFactory
{
    [Obsolete]
    private VSTestDiscoverTestExecutionRequestFactory()
    {
    }

    // This class is never instantiated.
    // It's not possible to reach this method.
    // The class should probably be static and not needing to implement the interface.
    [Obsolete]
    Task<TestExecutionRequest> ITestExecutionRequestFactory.CreateRequestAsync(Platform.TestHost.TestSessionContext session)
        => throw ApplicationStateGuard.Unreachable();

    public static VSTestDiscoverTestExecutionRequest CreateRequest(
        DiscoverTestExecutionRequest discoverTestExecutionRequest,
        VSTestBridgedTestFrameworkBase adapterExtension, string[] testAssemblyPaths, CancellationToken cancellationToken)
    {
        IServiceProvider serviceProvider = adapterExtension.ServiceProvider;
        IConfiguration configuration = serviceProvider.GetConfiguration();
        ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        IFileSystem fileSystem = serviceProvider.GetFileSystem();
        IClientInfo clientInfo = serviceProvider.GetClientInfo();

        IOutputDevice outputDevice = serviceProvider.GetOutputDevice();
        MessageLoggerAdapter messageLogger = new(loggerFactory, outputDevice, adapterExtension);

        ICommandLineOptions commandLineOptions = serviceProvider.GetRequiredService<ICommandLineOptions>();
        RunSettingsAdapter runSettings = new(commandLineOptions, fileSystem, configuration, clientInfo, loggerFactory, messageLogger);
        DiscoveryContextAdapter discoveryContext = new(commandLineOptions, runSettings, discoverTestExecutionRequest.Filter);

        ITestApplicationModuleInfo testApplicationModuleInfo = serviceProvider.GetTestApplicationModuleInfo();
        IMessageBus messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        TestCaseDiscoverySinkAdapter discoverySink = new(adapterExtension, discoverTestExecutionRequest.Session, testAssemblyPaths, testApplicationModuleInfo, loggerFactory, messageBus, adapterExtension.IsTrxEnabled, clientInfo, cancellationToken);

        return new(discoverTestExecutionRequest.Session, discoverTestExecutionRequest.Filter, testAssemblyPaths, discoveryContext, messageLogger, discoverySink);
    }
}
