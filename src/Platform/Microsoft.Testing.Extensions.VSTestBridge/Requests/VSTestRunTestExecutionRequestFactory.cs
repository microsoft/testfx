// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

namespace Microsoft.Testing.Extensions.VSTestBridge.Requests;

/// <summary>
/// VSTest specific implementation of Microsoft Testing Platform <see cref="ITestExecutionRequestFactory"/>.
/// </summary>
public sealed class VSTestRunTestExecutionRequestFactory : ITestExecutionRequestFactory
{
    private readonly ITestFrameworkCapabilities _testFrameworkCapabilities;
    private readonly ITestFramework _testFrameworkAdapter;
    private readonly ICommandLineOptions _commandLineService;
    private readonly ILogger<VSTestRunTestExecutionRequestFactory> _logger;
    private readonly string[] _assemblyPaths;
    private readonly VSTestTestExecutionFilter _testExecutionFilter;
    private readonly IRunContext _runContext;
    private readonly IFrameworkHandle _frameworkHandle;

    internal VSTestRunTestExecutionRequestFactory(ITestFrameworkCapabilities testFrameworkCapabilities, ITestFramework testFrameworkAdapter,
        ICommandLineOptions commandLineService, ILoggerFactory loggerFactory, string[] assemblyPaths, VSTestTestExecutionFilter testExecutionFilter,
        IRunContext runContext, IFrameworkHandle frameworkHandle)
    {
        _testFrameworkCapabilities = testFrameworkCapabilities;
        _testFrameworkAdapter = testFrameworkAdapter;
        _commandLineService = commandLineService;
        _logger = loggerFactory.CreateLogger<VSTestRunTestExecutionRequestFactory>();
        _assemblyPaths = assemblyPaths;
        _testExecutionFilter = testExecutionFilter;
        _runContext = runContext;
        _frameworkHandle = frameworkHandle;
    }

    Task<TestExecutionRequest> ITestExecutionRequestFactory.CreateRequestAsync(Platform.TestHost.TestSessionContext session)
        => !_commandLineService.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey)
            ? throw new InvalidOperationException($"Command line argument {PlatformCommandLineProvider.VSTestAdapterModeOptionKey} is not set but we are in VSTest adapter mode. This is a bug in the adapter.")
            : _testFrameworkCapabilities.GetCapability<IVSTestFlattenedTestNodesReportCapability>()?.IsSupported != true
                ? throw new InvalidOperationException($"Skipping test adapter {_testFrameworkAdapter.DisplayName} because it is not {nameof(IVSTestFlattenedTestNodesReportCapability)} capable.")
                : _commandLineService.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
                    ? throw new NotSupportedException($"The {nameof(VSTestRunTestExecutionRequestFactory)} does not support creating a {nameof(DiscoverTestExecutionRequest)}.")
                    : Task.FromResult<TestExecutionRequest>(new VSTestRunTestExecutionRequest(session, _testExecutionFilter, _assemblyPaths, _runContext, _frameworkHandle));

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

        RunSettingsAdapter runSettings = new(commandLineOptions, fileSystem, configuration, runTestExecutionRequest.Session.Client, loggerFactory);
        RunContextAdapter runContext = runTestExecutionRequest.Filter is TestNodeUidListFilter uidListFilter
            ? new(commandLineOptions, runSettings, uidListFilter.TestNodeUids)
            : new(commandLineOptions, runSettings);

        ITestApplicationModuleInfo testApplicationModuleInfo = serviceProvider.GetTestApplicationModuleInfo();
        IMessageBus messageBus = serviceProvider.GetRequiredService<IMessageBus>();
        IOutputDevice outputDevice = serviceProvider.GetOutputDevice();
        FrameworkHandlerAdapter frameworkHandlerAdapter = new(adapterExtension, runTestExecutionRequest.Session, testAssemblyPaths,
            testApplicationModuleInfo, loggerFactory, messageBus, outputDevice, adapterExtension.IsTrxEnabled, cancellationToken);

        return new(runTestExecutionRequest.Session, new(), testAssemblyPaths, runContext, frameworkHandlerAdapter);
    }
}
