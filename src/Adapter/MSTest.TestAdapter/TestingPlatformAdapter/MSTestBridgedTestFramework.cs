﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Testing.Extensions.VSTestBridge;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestBridgedTestFramework : SynchronizedSingleSessionVSTestBridgedTestFramework
{
    private readonly BridgedConfiguration? _configuration;

    public MSTestBridgedTestFramework(MSTestExtension mstestExtension, Func<IEnumerable<Assembly>> getTestAssemblies,
        IServiceProvider serviceProvider, ITestFrameworkCapabilities capabilities)
        : base(mstestExtension, getTestAssemblies, serviceProvider, capabilities)
        => _configuration = new(serviceProvider.GetConfiguration());

    /// <inheritdoc />
    protected override Task SynchronizedDiscoverTestsAsync(VSTestDiscoverTestExecutionRequest request, IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_DEBUG_DISCOVERTESTS") == "1"
            && !Debugger.IsAttached)
        {
            Debugger.Launch();
        }

        MSTestDiscoverer.DiscoverTests(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger, request.DiscoverySink, _configuration);
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
            testExecutor.RunTests(testCases, request.RunContext, request.FrameworkHandle, _configuration);
        }
        else
        {
            testExecutor.RunTests(request.AssemblyPaths, request.RunContext, request.FrameworkHandle, _configuration);
        }

        return Task.CompletedTask;
    }
}
#endif
