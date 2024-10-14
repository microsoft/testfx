// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Diagnostics;
using System.Reflection;

using Microsoft.Testing.Extensions.VSTestBridge;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal sealed class MSTestBridgedTestFramework : SynchronizedSingleSessionVSTestBridgedTestFramework
{
    private readonly IConfiguration? _configration;

    public MSTestBridgedTestFramework(MSTestExtension mstestExtension, Func<IEnumerable<Assembly>> getTestAssemblies,
        IServiceProvider serviceProvider, ITestFrameworkCapabilities capabilities)
        : base(mstestExtension, getTestAssemblies, serviceProvider, capabilities)
    {
        _configration = serviceProvider.GetConfiguration();
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

        MSTestDiscoverer.DiscoverTests(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger, request.DiscoverySink, _configration);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task SynchronizedRunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_RUNTESTS") == "1"
            && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        MSTestExecutor testExecutor = new(cancellationToken);

        if (request.VSTestFilter.TestCases is { } testCases)
        {
            testExecutor.RunTests(testCases, request.RunContext, request.FrameworkHandle, _configration);
        }
        else
        {
            testExecutor.RunTests(request.AssemblyPaths, request.RunContext, request.FrameworkHandle, _configration);
        }

        return Task.CompletedTask;
    }
}
#endif
