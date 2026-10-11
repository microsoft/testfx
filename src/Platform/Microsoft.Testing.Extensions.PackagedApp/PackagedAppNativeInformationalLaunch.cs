// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal static class PackagedAppNativeInformationalLaunch
{
    private const int InvalidPlatformSetupExitCode = 4;
    private const int TestSessionAbortedExitCode = 3;
    private static readonly TimeSpan CooperativeShutdownTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TerminationTimeout = TimeSpan.FromSeconds(10);

    internal static async Task<int> RunAsync(
        ITestHostLauncher launcher,
        TestHostLaunchContext context,
        bool isPackagedAppEnabled,
        TextWriter errorOutput,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!isPackagedAppEnabled || !await launcher.IsEnabledAsync().ConfigureAwait(false))
            {
                await errorOutput.WriteLineAsync(ExtensionResources.PackagedAppControllerNativeLauncherDisabled).ConfigureAwait(false);
                return InvalidPlatformSetupExitCode;
            }

            using ITestHostHandle handle = await launcher.LaunchTestHostAsync(context, cancellationToken).ConfigureAwait(false);
            try
            {
                await handle.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return handle.ExitCode;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // AUMID activation is outside our console/process tree. Give an SDK-cancelled host a
                // brief opportunity to exit, then terminate only the host represented by this handle.
                bool exited = false;
                try
                {
                    await handle.WaitForExitAsync(CancellationToken.None).WaitAsync(CooperativeShutdownTimeout, CancellationToken.None).ConfigureAwait(false);
                    exited = true;
                }
                catch (Exception exception)
                {
                    if (exception is not TimeoutException)
                    {
                        await errorOutput.WriteLineAsync(exception.Message).ConfigureAwait(false);
                    }
                }

                if (!exited)
                {
                    try
                    {
                        handle.Terminate();
                        await handle.WaitForExitAsync(CancellationToken.None).WaitAsync(TerminationTimeout, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        await errorOutput.WriteLineAsync(exception.Message).ConfigureAwait(false);
                    }
                }

                return TestSessionAbortedExitCode;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TestSessionAbortedExitCode;
        }
        catch (Exception exception)
        {
            await errorOutput.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return InvalidPlatformSetupExitCode;
        }
    }
}
