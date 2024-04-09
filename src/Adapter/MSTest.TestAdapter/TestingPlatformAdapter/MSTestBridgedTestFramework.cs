// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Testing.Extensions.VSTestBridge;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

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

        if (MSTestDiscovererHelpers.InitializeDiscovery(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger))
        {
            new UnitTestDiscoverer().DiscoverTests(request.AssemblyPaths, request.MessageLogger, request.DiscoverySink, request.DiscoveryContext);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    protected override async Task SynchronizedRunTestsAsync(VSTestRunTestExecutionRequest request, IMessageBus messageBus,
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_RUNTESTS") == "1"
            && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        if (!MSTestDiscovererHelpers.InitializeDiscovery(request.AssemblyPaths, request.RunContext, request.FrameworkHandle))
        {
            return;
        }

        MSTestExecutor testExecutor = new();
        using (cancellationToken.Register(testExecutor.Cancel))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ApartmentState currentApartmentState = Thread.CurrentThread.GetApartmentState();
                ApartmentState? requestedApartmentState = MSTestSettings.RunConfigurationSettings.ExecutionApartmentState;
                if (requestedApartmentState is not null && currentApartmentState != requestedApartmentState)
                {
                    Thread entryPointThread = new(new ThreadStart(() => RunTests(testExecutor, request)))
                    {
                        Name = "MSTest Entry Point",
                    };

                    entryPointThread.SetApartmentState(requestedApartmentState.Value);
                    entryPointThread.Start();

                    try
                    {
                        var threadTask = Task.Run(entryPointThread.Join, cancellationToken);
#if NET6_0_OR_GREATER
                        await threadTask.WaitAsync(cancellationToken);
#else
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                        threadTask.Wait(cancellationToken);
#pragma warning restore VSTHRD103 // Call async methods when in an async method
#endif
                    }
                    catch
                    {
                    }

                    return;
                }
            }

            RunTests(testExecutor, request);
        }

        // Local functions
        static void RunTests(MSTestExecutor testExecutor, VSTestRunTestExecutionRequest request)
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
    }
}
#endif
