// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Extensions.VSTestBridge;
using Microsoft.Testing.Extensions.VSTestBridge.Requests;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

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
        new MSTestDiscoverer().DiscoverTests(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger, request.DiscoverySink, _configuration);
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

    protected internal override void AddAdditionalProperties(TestNode testNode, TestCase testCase)
    {
        if (TryGetMethodIdentifierProperty(testCase, out TestMethodIdentifierProperty? testMethodIdentifierProperty))
        {
            testNode.Properties.Add(testMethodIdentifierProperty);
        }
    }

    private static bool TryGetMethodIdentifierProperty(TestCase testCase, [NotNullWhen(true)] out TestMethodIdentifierProperty? methodIdentifierProperty)
    {
        string? managedType = testCase.GetPropertyValue<string>(TestCaseExtensions.ManagedTypeProperty, defaultValue: null);
        string? managedMethod = testCase.GetPropertyValue<string>(TestCaseExtensions.ManagedMethodProperty, defaultValue: null);
        // NOTE: ManagedMethod, in case of MSTest, will have the parameter types.
        // So, we prefer using it to display the parameter types in Test Explorer.
        if (string.IsNullOrEmpty(managedType) || string.IsNullOrEmpty(managedMethod))
        {
            methodIdentifierProperty = null;
            return false;
        }

        methodIdentifierProperty = GetMethodIdentifierPropertyFromManagedTypeAndManagedMethod(managedType!, managedMethod!);
        return true;
    }

    private static TestMethodIdentifierProperty GetMethodIdentifierPropertyFromManagedTypeAndManagedMethod(
        string managedType,
        string managedMethod)
    {
        ManagedNameParser.ParseManagedMethodName(managedMethod, out string methodName, out int arity, out string[]? parameterTypes);

        parameterTypes ??= [];

        int lastIndexOfDot = managedType.LastIndexOf('.');
        string @namespace = lastIndexOfDot == -1 ? string.Empty : managedType.Substring(0, lastIndexOfDot);
        string typeName = lastIndexOfDot == -1 ? managedType : managedType.Substring(lastIndexOfDot + 1);

        // In the context of the VSTestBridge where we only have access to VSTest object model, we cannot determine ReturnTypeFullName.
        // For now, we lose this bit of information.
        // If really needed in the future, we can introduce a VSTest property to hold this info.
        // But the eventual goal should be to stop using the VSTestBridge altogether.
        // TODO: For AssemblyFullName, can we use Assembly.GetEntryAssembly().FullName?
        // Or alternatively, does VSTest object model expose the assembly full name somewhere?
        return new TestMethodIdentifierProperty(assemblyFullName: string.Empty, @namespace, typeName, methodName, arity, parameterTypes, returnTypeFullName: string.Empty);
    }
}
#endif
