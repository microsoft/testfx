// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class RuntimeContext
{
    private static readonly Lazy<bool> IsHotReloadEnabledLazy = new(() =>
        // Ideally we would use a capability from the runner instead of looking at environment variables.
        Environment.GetEnvironmentVariable("DOTNET_WATCH") == "1"
        || Environment.GetEnvironmentVariable("TESTINGPLATFORM_HOTRELOAD_ENABLED") == "1");

    public static bool IsHotReloadEnabled => IsHotReloadEnabledLazy.Value;

    /// <summary>
    /// Gets a value indicating whether the current runtime supports multiple threads.
    /// </summary>
    /// <remarks>
    /// Single-threaded WebAssembly runtimes (<c>browser-wasm</c> / <c>wasi-wasm</c>) have no thread
    /// pool: <c>Task.Run</c> continuations never execute and blocking waits throw
    /// <see cref="PlatformNotSupportedException"/>. Adapter code that would otherwise offload work to
    /// a background thread (for parallelism) must run inline when this is <see langword="false"/>.
    /// .NET 11 exposes <c>RuntimeFeature.IsMultithreadingSupported</c>
    /// (see <see href="https://github.com/dotnet/runtime/issues/77541"/>); we derive the same signal
    /// from the <see cref="OperatingSystem"/> probes available since .NET 8.
    /// </remarks>
    public static bool IsMultiThreaded { get; } =
#if NETCOREAPP
        !OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi();
#else
        // .NET Framework / UWP / WinUI builds never run on a single-threaded wasm runtime.
        true;
#endif
}
