// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable TPEXP // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Microsoft.Testing.Extensions.VSTestBridge.Capabilities;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// VSTest specific implementation of Microsoft Testing Platform <see cref="ITestExecutionRequestFactory"/>.
/// </summary>
public sealed class VSTestDiscoverTestExecutionRequestFactory : ITestExecutionRequestFactory
{
    private readonly ITestFrameworkCapabilities _testFrameworkCapabilities;
    private readonly ITestFramework _testFrameworkAdapter;
    private readonly ICommandLineOptions _commandLineService;
    private readonly string[] _assemblyPaths;
    private readonly VSTestTestExecutionFilter _testExecutionFilter;
    private readonly IDiscoveryContext _discoveryContext;
    private readonly IMessageLogger _messageLogger;
    private readonly ITestCaseDiscoverySink _discoverySink;

    internal VSTestDiscoverTestExecutionRequestFactory(ITestFrameworkCapabilities testFrameworkCapabilities, ITestFramework testFrameworkAdapter,
        ICommandLineOptions commandLineService, string[] assemblyPaths, VSTestTestExecutionFilter testExecutionFilter,
        IDiscoveryContext discoveryContext, IMessageLogger messageLogger, ITestCaseDiscoverySink discoverySink)
    {
        _testFrameworkCapabilities = testFrameworkCapabilities;
        _testFrameworkAdapter = testFrameworkAdapter;
        _commandLineService = commandLineService;
        _assemblyPaths = assemblyPaths;
        _discoveryContext = discoveryContext;
        _messageLogger = messageLogger;
        _discoverySink = discoverySink;
        _testExecutionFilter = testExecutionFilter;
    }

    Task<TestExecutionRequest> ITestExecutionRequestFactory.CreateRequestAsync(Platform.TestHost.TestSessionContext session)
        => !_commandLineService.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey)
            ? throw new InvalidOperationException($"Command line argument {PlatformCommandLineProvider.VSTestAdapterModeOptionKey} is not set but we are in VSTest adapter mode. This is a bug in the adapter.")
             : _testFrameworkCapabilities.GetCapability<IVSTestFlattenedTestNodesReportCapability>()?.IsSupported != true
                ? throw new InvalidOperationException($"Skipping test adapter {_testFrameworkAdapter.DisplayName} because it is not {nameof(IVSTestFlattenedTestNodesReportCapability)} capable.")
                : !_commandLineService.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
                    ? throw new NotSupportedException($"The {nameof(VSTestRunTestExecutionRequestFactory)} does not support creating a {nameof(DiscoverTestExecutionRequest)}.")
                    : Task.FromResult<TestExecutionRequest>(new VSTestDiscoverTestExecutionRequest(session, _testExecutionFilter, _assemblyPaths, _discoveryContext, _messageLogger, _discoverySink));

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
        DiscoveryContextAdapter discoveryContext = new(commandLineOptions, runSettings);

        ITestApplicationModuleInfo testApplicationModuleInfo = serviceProvider.GetTestApplicationModuleInfo();
        IMessageBus messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        TestCaseDiscoverySinkAdapter discoverySink = new(adapterExtension, discoverTestExecutionRequest.Session, testAssemblyPaths, testApplicationModuleInfo, loggerFactory, messageBus, adapterExtension.IsTrxEnabled, clientInfo, cancellationToken);

        return new(discoverTestExecutionRequest.Session, new(), testAssemblyPaths, discoveryContext, messageLogger, discoverySink);
    }
}
