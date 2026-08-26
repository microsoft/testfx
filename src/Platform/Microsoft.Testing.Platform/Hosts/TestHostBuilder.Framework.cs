// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder
{
    private const string HangDumpOptionName = "hangdump";

    private static async Task<ITestFramework> BuildTestFrameworkAsync(TestFrameworkBuilderData testFrameworkBuilderData)
    {
        if (!testFrameworkBuilderData.IsForDiscoveryRequest)
        {
            // Register the run before its deadline timer can observe the execution-completion state.
            IStopPoliciesService stopPoliciesService = testFrameworkBuilderData.ServiceProvider.GetRequiredService<IStopPoliciesService>();
            stopPoliciesService.NotifyTestExecutionStarting();
            try
            {
                return await BuildTestFrameworkCoreAsync(testFrameworkBuilderData).ConfigureAwait(false);
            }
            catch
            {
                stopPoliciesService.NotifyTestExecutionCompleted();
                throw;
            }
        }

        return await BuildTestFrameworkCoreAsync(testFrameworkBuilderData).ConfigureAwait(false);
    }

    private static async Task<ITestFramework> BuildTestFrameworkCoreAsync(TestFrameworkBuilderData testFrameworkBuilderData)
    {
        ServiceProvider serviceProvider = testFrameworkBuilderData.ServiceProvider;
        serviceProvider.AddService(testFrameworkBuilderData.MessageBusProxy);

        IPushOnlyProtocolConsumer? pushOnlyProtocolDataConsumer = null;
        IPushOnlyProtocol? pushOnlyProtocol = serviceProvider.GetService<IPushOnlyProtocol>();
        if (pushOnlyProtocol?.IsServerMode == true)
        {
            pushOnlyProtocolDataConsumer = await pushOnlyProtocol.GetDataConsumerAsync().ConfigureAwait(false);
        }

        List<IDataConsumer> dataConsumersBuilder = [];

        await RegisterAsServiceOrConsumerOrBothAsync(testFrameworkBuilderData.PlatformOutputDisplayService, serviceProvider, dataConsumersBuilder).ConfigureAwait(false);
        await RegisterAsServiceOrConsumerOrBothAsync(testFrameworkBuilderData.TestExecutionRequestFactory, serviceProvider, dataConsumersBuilder).ConfigureAwait(false);
        await RegisterAsServiceOrConsumerOrBothAsync(testFrameworkBuilderData.TestExecutionRequestInvoker, serviceProvider, dataConsumersBuilder).ConfigureAwait(false);
        await RegisterAsServiceOrConsumerOrBothAsync(testFrameworkBuilderData.TestExecutionFilterFactory, serviceProvider, dataConsumersBuilder).ConfigureAwait(false);

        ITestFrameworkCapabilities testFrameworkCapabilities = serviceProvider.GetTestFrameworkCapabilities();
        if (testFrameworkBuilderData.IsServerRequest)
        {
            // Capabilities can contain request lifecycle state (for example graceful-stop pending/active/completed).
            // Replace the application capability copied into a server request's cloned provider. The framework and
            // its per-request extensions then share one fresh capability without affecting the console host.
            testFrameworkCapabilities = testFrameworkBuilderData.TestFrameworkManager.TestFrameworkCapabilitiesFactory(serviceProvider);
            if (testFrameworkCapabilities is IAsyncInitializableExtension testFrameworkCapabilitiesAsyncInitializable)
            {
                await testFrameworkCapabilitiesAsyncInitializable.InitializeAsync().ConfigureAwait(false);
            }

            serviceProvider.ReplaceService(testFrameworkCapabilities);
        }

        ITestFramework testFramework = testFrameworkBuilderData.TestFrameworkManager.TestFrameworkFactory(testFrameworkCapabilities, serviceProvider);
        await testFramework.TryInitializeAsync().ConfigureAwait(false);
        if (testFramework is IDataProducer dataProducer)
        {
            serviceProvider.GetRequiredService<TestCoverageCapabilities>().RegisterProducer(dataProducer);
        }

        serviceProvider.AllowTestAdapterFrameworkRegistration = true;
        try
        {
            await RegisterAsServiceOrConsumerOrBothAsync(new TestFrameworkProxy(testFramework), serviceProvider, dataConsumersBuilder).ConfigureAwait(false);
        }
        finally
        {
            serviceProvider.AllowTestAdapterFrameworkRegistration = false;
        }

        List<ITestSessionLifetimeHandler> testSessionLifetimeHandlers = [];
        if (!testFrameworkBuilderData.IsForDiscoveryRequest)
        {
            List<ICompositeExtensionFactory> newBuiltCompositeServices = [];
            (IExtension Consumer, int RegistrationOrder)[] consumers = await testFrameworkBuilderData.TestSessionManager.BuildDataConsumersAsync(serviceProvider, newBuiltCompositeServices).ConfigureAwait(false);
            (IExtension TestSessionLifetimeHandler, int RegistrationOrder)[] sessionLifeTimeHandlers = await testFrameworkBuilderData.TestSessionManager.BuildTestSessionLifetimeHandleAsync(serviceProvider, newBuiltCompositeServices).ConfigureAwait(false);

            testSessionLifetimeHandlers.AddRange(sessionLifeTimeHandlers.OrderBy(x => x.RegistrationOrder).Select(x => (ITestSessionLifetimeHandler)x.TestSessionLifetimeHandler));

            foreach ((IExtension Extension, int _) testhostExtension in consumers.Union(sessionLifeTimeHandlers).OrderBy(x => x.RegistrationOrder))
            {
                if (testhostExtension.Extension is IDataConsumer)
                {
                    await RegisterAsServiceOrConsumerOrBothAsync(testhostExtension.Extension, serviceProvider, dataConsumersBuilder).ConfigureAwait(false);
                }
                else
                {
                    await AddServiceIfNotSkippedAsync(testhostExtension.Extension, serviceProvider).ConfigureAwait(false);
                }
            }
        }

        foreach (IDataConsumer consumerService in testFrameworkBuilderData.ServerPerCallConsumers)
        {
            if (consumerService is ITestSessionLifetimeHandler handler)
            {
                testSessionLifetimeHandlers.Add(handler);
            }

            await RegisterAsServiceOrConsumerOrBothAsync(consumerService, serviceProvider, dataConsumersBuilder).ConfigureAwait(false);
        }

        if (pushOnlyProtocolDataConsumer is not null)
        {
            testSessionLifetimeHandlers.Add(pushOnlyProtocolDataConsumer);
        }

        ITestApplicationProcessExitCode testApplicationResult = serviceProvider.GetRequiredService<ITestApplicationProcessExitCode>();
        await RegisterAsServiceOrConsumerOrBothAsync(testApplicationResult, serviceProvider, dataConsumersBuilder).ConfigureAwait(false);

        TestCoverageResult testCoverageResult = serviceProvider.GetRequiredService<TestCoverageResult>();
        await RegisterAsServiceOrConsumerOrBothAsync(testCoverageResult, serviceProvider, dataConsumersBuilder).ConfigureAwait(false);

        if (pushOnlyProtocolDataConsumer is not null)
        {
            dataConsumersBuilder.Add(pushOnlyProtocolDataConsumer);
        }

        var abortForMaxFailedTestsExtension = new AbortForMaxFailedTestsExtension(
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetTestFrameworkCapabilities().GetCapability<IGracefulStopTestExecutionCapability>(),
            serviceProvider.GetRequiredService<IStopPoliciesService>(),
            serviceProvider.GetTestApplicationCancellationTokenSource());

        if (await abortForMaxFailedTestsExtension.IsEnabledAsync().ConfigureAwait(false))
        {
            dataConsumersBuilder.Add(abortForMaxFailedTestsExtension);
        }

        // Build one deadline extension for the active run request. In server mode the per-request service
        // provider and message bus own and dispose it when that request ends; discovery requests never arm it.
        if (!testFrameworkBuilderData.IsForDiscoveryRequest)
        {
            var abortAtDeadlineExtension = new AbortAtDeadlineExtension(
                serviceProvider.GetEnvironment(),
                serviceProvider.GetSystemClock(),
                serviceProvider.GetTestFrameworkCapabilities().GetCapability<IGracefulStopTestExecutionCapability>(),
                serviceProvider.GetRequiredService<IStopPoliciesService>(),
                serviceProvider.GetTestApplicationCancellationTokenSource(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetLoggerFactory(),
                isHangDumpEnabled: serviceProvider.GetCommandLineOptions().IsOptionSet(HangDumpOptionName));

            if (await abortAtDeadlineExtension.IsEnabledAsync().ConfigureAwait(false))
            {
                dataConsumersBuilder.Add(abortAtDeadlineExtension);

                // Also register it as a service so the host can tell it, the moment the test framework invoker
                // returns, that test execution is over. On that signal it disarms the deadline, so a timer
                // firing while the reporters finalize an already-finished run cannot wrongly mark the run as
                // deadline-truncated (exit code 15). A session-lifetime handler would be too late: this is an
                // IDataConsumer, and consumer handlers run at the very end of NotifyTestSessionEndAsync.
                serviceProvider.AddService(abortAtDeadlineExtension);

                // Keep the lifetime-handler registration as a backstop for host paths that do not execute the
                // invoker path above. It runs too late to protect reporting on its own.
                testSessionLifetimeHandlers.Add(abortAtDeadlineExtension);
            }
        }

        // The container captures the list by reference (so a later Add would still be observed), but populating
        // it fully before registering keeps this order-independent and free of that subtlety. Lifetime handlers
        // are enumerated later, during NotifyTestSessionEndAsync.
        serviceProvider.AddService(new TestSessionLifetimeHandlersContainer(testSessionLifetimeHandlers));

        AsynchronousMessageBus concreteMessageBusService = new(
            [.. dataConsumersBuilder],
            serviceProvider.GetTestApplicationCancellationTokenSource(),
            serviceProvider.GetTask(),
            serviceProvider.GetLoggerFactory(),
            serviceProvider.GetEnvironment(),
            serviceProvider.GetService<IShutdownProgressReporter>());
        await concreteMessageBusService.InitAsync().ConfigureAwait(false);
        testFrameworkBuilderData.MessageBusProxy.SetBuiltMessageBus(concreteMessageBusService);

        return testFramework;
    }

    private static ConsoleTestHost CreateConsoleTestHost(
        ServiceProvider serviceProvider,
        Func<TestFrameworkBuilderData, Task<ITestFramework>> buildTestFrameworkAsync,
        TestFrameworkManager testFrameworkManager,
        TestHostManager testHostManager)
        => new(serviceProvider, buildTestFrameworkAsync, testFrameworkManager, testHostManager);
}
