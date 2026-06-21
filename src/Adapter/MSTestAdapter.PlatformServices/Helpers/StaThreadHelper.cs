// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

/// <summary>
/// Helper for running async operations on a Windows STA thread when required.
/// </summary>
internal static class StaThreadHelper
{
    /// <summary>
    /// Runs <paramref name="action"/> on a new Windows STA thread if <paramref name="needsSta"/>
    /// is <see langword="true"/>, the current OS is Windows, and the current thread is not already
    /// an STA thread. Otherwise, awaits <paramref name="action"/> on the calling thread.
    /// </summary>
    /// <typeparam name="TResult">The return type of <paramref name="action"/>.</typeparam>
    /// <remarks>
    /// If <paramref name="needsSta"/> is <see langword="true"/> but the OS is not Windows, a warning
    /// is logged and <paramref name="action"/> is awaited on the calling thread.
    /// </remarks>
    internal static async Task<TResult> RunOnStaThreadIfNeededAsync<TResult>(
        bool needsSta,
        Func<Task<TResult>> action,
        string threadName,
        TResult defaultResult)
    {
        bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (needsSta && isWindowsOS && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            TResult result = defaultResult;
            Thread entryPointThread = new(() => result = action().GetAwaiter().GetResult())
            {
                Name = threadName,
            };
            entryPointThread.SetApartmentState(ApartmentState.STA);
            entryPointThread.Start();

            try
            {
                entryPointThread.Join();
            }
            catch (Exception ex) when (ex is ThreadStateException or ThreadInterruptedException)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsErrorEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Error(
                        $"Failed to join STA thread '{threadName}': {ex}");
                }

                return defaultResult;
            }

            return result;
        }

        if (!isWindowsOS && needsSta
            && PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(Resource.STAIsOnlySupportedOnWindowsWarning);
        }

        return await action().ConfigureAwait(false);
    }
}
