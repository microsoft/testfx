// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Diagnostics;
using System.Reflection;

using Microsoft.Testing.Extensions.VSTestBridge;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

internal sealed class MSTestBridgedTestFramework : SynchronizedSingleSessionVSTestBridgedTestFramework
{
    public MSTestBridgedTestFramework(MSTestExtension mstestExtension, Func<IEnumerable<Assembly>> getTestAssemblies,
        IServiceProvider serviceProvider, ITestFrameworkCapabilities capabilities)
        : base(mstestExtension, getTestAssemblies, serviceProvider, capabilities)
    {
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

        if (MSTestDiscovererHelpers.InitializeDiscovery(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger, true))
        {
            new UnitTestDiscoverer().DiscoverTests(request.AssemblyPaths, request.MessageLogger, request.DiscoverySink, request.DiscoveryContext);
        }

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

        bool isRunTestCases = request.VSTestFilter.TestCases is not null;

        if (!MSTestDiscovererHelpers.InitializeDiscovery(request.AssemblyPaths, request.RunContext, request.FrameworkHandle, !isRunTestCases))
        {
            return Task.CompletedTask;
        }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        ITestExecutor testExecutor = new MSTestExecutor();
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        using (cancellationToken.Register(testExecutor.Cancel))
        {
            if (request.VSTestFilter.TestCases is { } testCases)
            {
                testExecutor.RunTests(testCases, request.RunContext, request.FrameworkHandle);
            }
            else
            {
                testExecutor.RunTests(request.AssemblyPaths, request.RunContext, request.FrameworkHandle);
            }
        }

        return Task.CompletedTask;
    }
}
#endif
