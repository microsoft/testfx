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
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
[StackTraceHidden]
internal sealed class MSTestBridgedTestFramework : SynchronizedSingleSessionVSTestBridgedTestFramework
{
    // Opt-in switch for the experimental native Microsoft.Testing.Platform integration: when enabled, MSTest
    // publishes test nodes directly from its neutral execution model instead of routing discovery and results
    // through the VSTest bridge object model. Off by default, so the shipping behavior is unchanged.
    private static readonly bool UseNativeMtpProduction =
        Environment.GetEnvironmentVariable("MSTEST_EXPERIMENTAL_NATIVE_MTP") is "1" or "true" or "True";

    private readonly BridgedConfiguration? _configuration;
    private readonly ILoggerFactory _loggerFactory;

    public MSTestBridgedTestFramework(MSTestExtension mstestExtension, Func<IEnumerable<Assembly>> getTestAssemblies,
        IServiceProvider serviceProvider, ITestFrameworkCapabilities capabilities)
        : base(mstestExtension, getTestAssemblies, serviceProvider, capabilities)
    {
        _configuration = new(serviceProvider.GetConfiguration());
        _loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        PlatformServiceProvider.Instance.AdapterTraceLogger = new MTPTraceLogger(_loggerFactory.CreateLogger("mstest-trace"));
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

        var discoverer = new MSTestDiscoverer(new TestSourceHandler(), CreateTelemetrySender());
        if (UseNativeMtpProduction && SessionUid is { } sessionUid)
        {
            // Publish discovered test nodes directly from the neutral model; the VSTest discovery sink in the
            // request is bypassed (no VSTest TestCase materialization / ObjectModelConverters round-trip).
            var elementSink = new MtpUnitTestElementSink(messageBus, this, sessionUid, IsTrxEnabled);
            return discoverer.DiscoverTestsAsync(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger, elementSink, _configuration, isMTP: true);
        }

        return discoverer.DiscoverTestsAsync(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger, request.DiscoverySink, _configuration, isMTP: true);
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

        MSTestExecutor testExecutor = new(cancellationToken, CreateTelemetrySender());
        if (UseNativeMtpProduction && SessionUid is { } sessionUid)
        {
            // Report results directly as native test nodes; the framework handle is still used for message logging
            // and apartment-state handling, but its VSTest RecordResult / ObjectModelConverters path is bypassed.
            await testExecutor.RunTestsAsync(
                request.AssemblyPaths,
                request.RunContext,
                request.FrameworkHandle,
                settings => new MtpTestResultRecorder(messageBus, this, sessionUid, IsTrxEnabled, settings),
                _configuration,
                isMTP: true).ConfigureAwait(false);
            return;
        }

        await testExecutor.RunTestsAsync(request.AssemblyPaths, request.RunContext, request.FrameworkHandle, _configuration, isMTP: true).ConfigureAwait(false);
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

    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
    private Func<string, IDictionary<string, object>, Task>? CreateTelemetrySender()
    {
        ITelemetryInformation telemetryInformation = ServiceProvider.GetTelemetryInformation();
        if (!telemetryInformation.IsEnabled)
        {
            return null;
        }

        ITelemetryCollector telemetryCollector = ServiceProvider.GetTelemetryCollector();

        return (eventName, metrics) => telemetryCollector.LogEventAsync(eventName, metrics, CancellationToken.None);
    }
}
#endif
