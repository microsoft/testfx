// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

[SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "Match the Task API")]
internal interface ITask
{
    Task Run(Func<Task> function, CancellationToken cancellationToken);

    Task Run(Action action);

    Task<T> Run<T>(Func<Task<T>?> function, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="action"/> on a dedicated named thread.
    /// </summary>
    /// <param name="action">The work to run.</param>
    /// <param name="name">The thread name, used to identify the thread in a dump.</param>
    /// <param name="cancellationToken">A token observed before the thread is started.</param>
    /// <returns>A task that completes when <paramref name="action"/> completes.</returns>
    /// <remarks>
    /// Single-threaded WebAssembly runtimes cannot create threads, so this throws
    /// <see cref="PlatformNotSupportedException"/> there. Callers reachable on <c>browser-wasm</c> /
    /// <c>wasi-wasm</c> MUST branch on <c>RuntimeFeatureHelper.IsMultiThreaded</c> first; see that
    /// property for the two established fallbacks. The
    /// <see cref="UnsupportedOSPlatformAttribute"/> annotations below only produce CA1416 diagnostics
    /// for projects that actually target a browser TFM, which the extension projects do not, so they
    /// are documentation rather than enforcement.
    /// </remarks>
#if !MTP_MSBUILD_TASKS
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
#endif
    Task RunLongRunning(Func<Task> action, string name, CancellationToken cancellationToken);

    Task WhenAll(params Task[] tasks);

    Task Delay(int millisecondDelay);

    Task Delay(TimeSpan timeSpan, CancellationToken cancellationToken);
}
