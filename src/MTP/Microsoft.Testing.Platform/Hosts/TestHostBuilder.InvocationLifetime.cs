// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.InvocationLifetime;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder
{
    private async Task<IReadOnlyList<IInvocationLifetimeHandler>?> BuildInvocationLifetimeHandlersIfOwnerAsync(BuildContext context)
    {
        // The controller PID and retry pipe identify children even when the launch does not inherit the
        // owner's environment (for example an AUMID-activated packaged application).
        if (!((InvocationLifetimeManager)InvocationLifetime).HasHandlers
            || context.IsJsonRpcProtocol
            || context.CommandLineHandler.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
            || context.TestHostControllerInfo.HasTestHostController
            || context.CommandLineHandler.IsOptionSet(RetryPipeNameOptionKey)
            || !RoslynString.IsNullOrEmpty(context.SystemEnvironment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_OWNER)))
        {
            return null;
        }

        // Kept even when every handler is disabled: the owner still publishes its marker, so a handler that a child
        // process sees as enabled (for example through a variable set by an environment provider) does not run there
        // once per test host process.
        return await ((InvocationLifetimeManager)InvocationLifetime).BuildAsync(context.ServiceProvider).ConfigureAwait(false);
    }

    private static void AddInvocationLifetimeRunner(
        BuildContext context,
        ServiceProvider serviceProvider,
        bool setOwnerMarker,
        TimeSpan? setupTimeout = null)
    {
        if (context.InvocationLifetimeHandlers is not { } handlers)
        {
            return;
        }

        serviceProvider.AddService(new InvocationLifetimeRunner(
            handlers,
            context.SystemEnvironment,
            context.ProxyOutputDevice,
            context.LoggerFactory.CreateLogger<InvocationLifetimeRunner>(),
            setOwnerMarker,
            restoreEnvironment: false,
            setupTimeout: setupTimeout));
    }

    private static async Task ReleaseInvocationLifetimeHandlersAsync(IReadOnlyList<IInvocationLifetimeHandler>? handlers)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (IInvocationLifetimeHandler handler in handlers)
        {
            try
            {
                await DisposeHelper.DisposeAsync(handler).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsNonFatalInvocationLifetimeException(ex))
            {
                // Host construction already failed; release every handler without replacing that failure.
            }
        }
    }

    private static bool IsNonFatalInvocationLifetimeException(Exception exception)
        => exception is AggregateException aggregateException
            ? aggregateException.Flatten().InnerExceptions.All(IsNonFatalInvocationLifetimeException)
            : exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException
                and not AppDomainUnloadedException
                and not BadImageFormatException;

    private static void ClearInvocationLifetimeEnvironmentVariables(IEnvironment environment)
    {
        List<string>? names = null;
        foreach (DictionaryEntry entry in environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name
                && name.StartsWith(EnvironmentVariableConstants.TESTINGPLATFORM_INVOCATIONLIFETIME_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                (names ??= []).Add(name);
            }
        }

        if (names is null)
        {
            return;
        }

        foreach (string name in names)
        {
            environment.SetEnvironmentVariable(name, null);
        }
    }
}
