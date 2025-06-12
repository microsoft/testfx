// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions.VSTestBridge;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestBridgedTestFramework : SynchronizedSingleSessionVSTestBridgedTestFramework
{
    private readonly BridgedConfiguration? _configuration;
    private readonly ILoggerFactory _loggerFactory;

    public MSTestBridgedTestFramework(MSTestExtension mstestExtension, Func<IEnumerable<Assembly>> getTestAssemblies,
        IServiceProvider serviceProvider, ITestFrameworkCapabilities capabilities)
        : base(mstestExtension, getTestAssemblies, serviceProvider, capabilities)
    {
        _configuration = new(serviceProvider.GetConfiguration());
        _loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    }

    /// <inheritdoc />
    protected override Task SynchronizedDiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_DISCOVERTESTS") == "1"
            && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        PlatformServiceProvider.Instance.AdapterTraceLogger = new BridgedTraceLogger(_loggerFactory.CreateLogger("mstest-trace"));
        MSTestDiscoverer.DiscoverTests(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger, request.DiscoverySink, _configuration);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task SynchronizedRunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_RUNTESTS") == "1"
            && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        PlatformServiceProvider.Instance.AdapterTraceLogger = new BridgedTraceLogger(_loggerFactory.CreateLogger("mstest-trace"));
        MSTestExecutor testExecutor = new(cancellationToken);
        await testExecutor.RunTestsAsync(request.AssemblyPaths, request.RunContext, request.FrameworkHandle, _configuration).ConfigureAwait(false);
    }
}
#endif
