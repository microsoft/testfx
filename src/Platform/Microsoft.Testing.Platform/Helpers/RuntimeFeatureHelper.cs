// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// Runtime feature detection that the platform uses to adapt its behavior to the host.
/// </summary>
internal static class RuntimeFeatureHelper
{
    /// <summary>
    /// Gets a value indicating whether the current runtime supports multiple threads.
    /// </summary>
    /// <remarks>
    /// Single-threaded WebAssembly runtimes (<c>browser-wasm</c> and <c>wasi-wasm</c>) do not have a
    /// thread pool: <see cref="System.Threading.Tasks.Task.Run(System.Action)"/> continuations never
    /// execute while the single thread is busy, and blocking waits
    /// (<c>Task.Wait</c> / <c>GetAwaiter().GetResult()</c> on an incomplete task) throw
    /// <see cref="PlatformNotSupportedException"/>. Code that would otherwise offload work to a
    /// background thread MUST fall back to inline/synchronous execution when this returns
    /// <see langword="false"/>, otherwise the operation deadlocks or throws.
    /// <para>
    /// .NET 11 exposes <c>System.Runtime.CompilerServices.RuntimeFeature.IsMultithreadingSupported</c>
    /// for exactly this purpose (see <see href="https://github.com/dotnet/runtime/issues/77541"/>),
    /// but the platform still targets earlier frameworks, so we derive the same information from the
    /// <see cref="OperatingSystem"/> single-threaded-wasm probes, which are available since .NET 8.
    /// </para>
    /// </remarks>
    public static bool IsMultiThreaded { get; } =
#if NETCOREAPP
        !OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi();
#else
        // netstandard2.0 / .NET Framework builds never run on a single-threaded wasm runtime.
        true;
#endif
}
