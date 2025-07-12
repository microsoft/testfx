// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Hosting;

internal sealed class HotReloadTestHostTestFrameworkInvoker : TestHostTestFrameworkInvoker
{
    private readonly bool _isHotReloadEnabled;

    public HotReloadTestHostTestFrameworkInvoker(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _isHotReloadEnabled = IsHotReloadEnabled(serviceProvider.GetEnvironment());
        if (_isHotReloadEnabled)
        {
            ((SystemRuntimeFeature)serviceProvider.GetRuntimeFeature()).EnableHotReload();
        }
    }

    private static bool IsHotReloadEnabled(IEnvironment environment)
        => environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_WATCH) == "1"
        || environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_HOTRELOAD_ENABLED) == "1";

    public override async Task ExecuteRequestAsync(ITestFramework testFrameworkAdapter, TestExecutionRequest request,
        IMessageBus messageBus, CancellationToken cancellationToken)
    {
        if (!_isHotReloadEnabled)
        {
            await base.ExecuteRequestAsync(testFrameworkAdapter, request, messageBus, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Using the output device here rather than Console WriteLine ensures that we don't break live logger output.
        IOutputDevice outputDevice = ServiceProvider.GetOutputDevice();
        var hotReloadHandler = new HotReloadHandler(ServiceProvider.GetConsole(), outputDevice, this);
        TaskCompletionSource<int>? executionCompleted = null;
        while (await hotReloadHandler.ShouldRunAsync(executionCompleted?.Task, ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken).ConfigureAwait(false))
        {
            executionCompleted = new();
            using SemaphoreSlim requestSemaphore = new(1);
            var hotReloadOutputDevice = ServiceProvider.GetPlatformOutputDevice() as IHotReloadPlatformOutputDevice;
            if (hotReloadOutputDevice is not null)
            {
                await hotReloadOutputDevice.DisplayBeforeHotReloadSessionStartAsync().ConfigureAwait(false);
            }

            await testFrameworkAdapter.ExecuteRequestAsync(new(request, messageBus, new SemaphoreSlimRequestCompleteNotifier(requestSemaphore), cancellationToken)).ConfigureAwait(false);

            await requestSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            await ServiceProvider.GetBaseMessageBus().DrainDataAsync().ConfigureAwait(false);
            if (hotReloadOutputDevice is not null)
            {
                await hotReloadOutputDevice.DisplayAfterHotReloadSessionEndAsync().ConfigureAwait(false);
            }

            executionCompleted.SetResult(0);
        }
    }
}
