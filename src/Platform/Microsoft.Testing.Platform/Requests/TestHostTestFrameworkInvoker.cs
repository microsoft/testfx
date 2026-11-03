// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Requests;

[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "HotReload needs to inherit and override ExecuteRequestAsync")]
internal class TestHostTestFrameworkInvoker(IServiceProvider serviceProvider) : ITestFrameworkInvoker, IOutputDeviceDataProducer
{
    private readonly PlatformOpenTelemetryOptions _openTelemetryOptions =
        PlatformOpenTelemetryOptions.FromEnvironment(serviceProvider.GetEnvironment());

    protected IServiceProvider ServiceProvider { get; } = serviceProvider;

    public string Uid => nameof(TestHostTestFrameworkInvoker);

    public string Version => PlatformVersion.Version;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task ExecuteAsync(ITestFramework testFramework, ClientInfo client, CancellationToken cancellationToken)
    {
        ExtensionHelper.ConfigureOTelLegacyAttributes(_openTelemetryOptions);

        ILogger<TestHostTestFrameworkInvoker> logger = ServiceProvider.GetLoggerFactory().CreateLogger<TestHostTestFrameworkInvoker>();
        await logger.LogInformationAsync($"Test framework UID: '{testFramework.Uid}' Version: '{testFramework.Version}' DisplayName: '{testFramework.DisplayName}' Description: '{testFramework.Description}'").ConfigureAwait(false);

        foreach (ICapability capability in ServiceProvider.GetTestFrameworkCapabilities().Capabilities)
        {
            if (capability is ITestNodesTreeFilterTestFrameworkCapability testNodesTreeFilterCapability)
            {
                await logger.LogInformationAsync($"ITestNodesTreeFilterCapability.IsSupported: {testNodesTreeFilterCapability.IsSupported}").ConfigureAwait(false);
            }
        }

        SessionUid sessionId = ServiceProvider.GetTestSessionContext().SessionUid;
        await logger.LogDebugAsync($"Test session UID: '{sessionId.Value}'").ConfigureAwait(false);

        IPlatformOpenTelemetryService? otelService = ServiceProvider.GetPlatformOTelService();
        using (otelService?.StartActivity("CreateTestFrameworkSession", tags: GetSessionTags(sessionId)))
        {
            CreateTestSessionResult createTestSessionResult = await testFramework.CreateTestSessionAsync(new(sessionId, cancellationToken)).ConfigureAwait(false);
            await HandleTestSessionResultAsync(logger, "CreateTestSession", sessionId, createTestSessionResult.IsSuccess, createTestSessionResult.WarningMessage, createTestSessionResult.ErrorMessage, cancellationToken).ConfigureAwait(false);
        }

        TestExecutionRequest request;
        using (otelService?.StartActivity("CreateTestRequest", tags: GetSessionTags(sessionId)))
        {
            ITestExecutionRequestFactory testExecutionRequestFactory = ServiceProvider.GetTestExecutionRequestFactory();
            request = await testExecutionRequestFactory.CreateRequestAsync(new(sessionId), cancellationToken).ConfigureAwait(false);
        }

        IMessageBus messageBus = ServiceProvider.GetMessageBus();

        // Execute the test request
        using (otelService?.StartActivity(
            "ExecuteTestRequest",
            tags: GetExecuteTestRequestTags(sessionId, request)))
        {
            await ExecuteRequestAsync(testFramework, request, messageBus, cancellationToken).ConfigureAwait(false);
        }

        using (otelService?.StartActivity("CloseTestFrameworkSession", tags: GetSessionTags(sessionId)))
        {
            CloseTestSessionResult closeTestSessionResult = await testFramework.CloseTestSessionAsync(new(sessionId, cancellationToken)).ConfigureAwait(false);
            await HandleTestSessionResultAsync(logger, "CloseTestSession", sessionId, closeTestSessionResult.IsSuccess, closeTestSessionResult.WarningMessage, closeTestSessionResult.ErrorMessage, cancellationToken).ConfigureAwait(false);
        }
    }

    public virtual async Task ExecuteRequestAsync(ITestFramework testFramework, TestExecutionRequest request, IMessageBus messageBus, CancellationToken cancellationToken)
    {
        ExtensionHelper.ConfigureOTelLegacyAttributes(_openTelemetryOptions);

        IPlatformOpenTelemetryService? otelService = ServiceProvider.GetPlatformOTelService();
        using IPlatformActivity? testFrameworkActivity = otelService?.StartActivity(
            TestingPlatformSemanticConventions.Activities.TestFramework,
            [
                new(TestingPlatformSemanticConventions.Attributes.TestFrameworkName, testFramework.DisplayName),
                new(TestingPlatformSemanticConventions.Attributes.TestFrameworkVersion, testFramework.Version),
                .. testFramework.ToOTelTags(),
            ]);
        otelService?.TestFrameworkActivity = testFrameworkActivity;
        using SemaphoreSlim requestSemaphore = new(0, 1);
        TestExecutionActivityBroker? testExecutionActivityBroker = otelService is IPlatformOpenTelemetryServiceWithTestExecutionActivities testExecutionActivityService
            ? new TestExecutionActivityBroker(testExecutionActivityService, _openTelemetryOptions)
            : null;
        var context = new ExecuteRequestContext(
            request,
            messageBus,
            new SemaphoreSlimRequestCompleteNotifier(requestSemaphore),
            cancellationToken,
            testExecutionActivityBroker);
        Exception? executionException = null;
        try
        {
            await testFramework.ExecuteRequestAsync(context).ConfigureAwait(false);
            await requestSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            executionException = ex;
            throw;
        }
        finally
        {
            await CompleteTestExecutionScopeAsync(
                testExecutionActivityBroker,
                executionException,
                cancellationToken).ConfigureAwait(false);
        }
    }

    protected async Task CompleteTestExecutionScopeAsync(
        TestExecutionActivityBroker? testExecutionActivityBroker,
        Exception? executionException,
        CancellationToken requestCancellationToken)
    {
        if (testExecutionActivityBroker is null)
        {
            return;
        }

        Exception? cleanupException = null;
        try
        {
            if (!requestCancellationToken.IsCancellationRequested)
            {
                await ServiceProvider.GetBaseMessageBus().DrainDataAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            cleanupException = ex;
        }

        try
        {
            testExecutionActivityBroker.Dispose();
        }
        catch (Exception ex)
        {
            cleanupException = cleanupException is null
                ? ex
                : new AggregateException(cleanupException, ex);
        }

        if (cleanupException is null)
        {
            return;
        }

        if (executionException is null)
        {
            ExceptionDispatchInfo.Capture(cleanupException).Throw();
            return;
        }

        AddSecondaryExceptionOrThrow(
            executionException,
            "TestExecutionScopeCleanupException",
            cleanupException);
    }

    private static void AddSecondaryExceptionOrThrow(Exception primaryException, string operation, Exception secondaryException)
    {
        try
        {
            if (primaryException.Data is { IsReadOnly: false } data)
            {
                data[operation] = secondaryException;
                return;
            }
        }
        catch (Exception)
        {
            // An exception can expose an IDictionary implementation that rejects reads or writes.
        }

        throw new AggregateException(
            $"{operation} failed while handling another exception.",
            primaryException,
            secondaryException);
    }

    private KeyValuePair<string, object?>[] GetSessionTags(SessionUid sessionId)
        => _openTelemetryOptions.EmitLegacyAttributes
            ? [
                new(TestingPlatformSemanticConventions.Attributes.TestSessionId, sessionId.Value),
                new("SessionUid", sessionId),
            ]
            : [
                new(TestingPlatformSemanticConventions.Attributes.TestSessionId, sessionId.Value),
            ];

    private KeyValuePair<string, object?>[] GetExecuteTestRequestTags(SessionUid sessionId, TestExecutionRequest request)
    {
        string requestType = request.GetType().Name;
        return _openTelemetryOptions.EmitLegacyAttributes
            ? [
                new(TestingPlatformSemanticConventions.Attributes.TestSessionId, sessionId.Value),
                new(TestingPlatformSemanticConventions.Attributes.TestRunRequestType, requestType),
                new("SessionUid", sessionId),
                new("RequestType", requestType),
            ]
            : [
                new(TestingPlatformSemanticConventions.Attributes.TestSessionId, sessionId.Value),
                new(TestingPlatformSemanticConventions.Attributes.TestRunRequestType, requestType),
            ];
    }

    private async Task HandleTestSessionResultAsync(ILogger logger, string phase, SessionUid sessionId, bool isSuccess, string? warningMessage, string? errorMessage, CancellationToken cancellationToken)
    {
        if (warningMessage is not null)
        {
            await logger.LogWarningAsync($"Test framework '{phase}' (session '{sessionId.Value}') reported warning: {warningMessage}").ConfigureAwait(false);
            IOutputDevice outputDisplay = ServiceProvider.GetOutputDevice();
            await outputDisplay.DisplayAsync(this, new WarningMessageOutputDeviceData(warningMessage), cancellationToken).ConfigureAwait(false);
        }

        if (!isSuccess)
        {
            string effectiveErrorMessage = errorMessage ?? PlatformResources.TestHostAdapterInvokerFailedTestSessionErrorMessage;
            await logger.LogErrorAsync($"Test framework '{phase}' (session '{sessionId.Value}') failed: {effectiveErrorMessage}").ConfigureAwait(false);
            ITestApplicationProcessExitCode testApplicationProcessExitCode = ServiceProvider.GetTestApplicationProcessExitCode();
            await testApplicationProcessExitCode.SetTestAdapterTestSessionFailureAsync(
                effectiveErrorMessage,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
