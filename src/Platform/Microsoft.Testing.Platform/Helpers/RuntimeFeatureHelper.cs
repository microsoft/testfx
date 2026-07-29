// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

/// <summary>
/// Runtime feature detection that the platform uses to adapt its behavior to the host.
/// </summary>
[Embedded]
internal static class RuntimeFeatureHelper
{
    /// <summary>
    /// Gets a value indicating whether the current runtime supports multiple threads.
    /// </summary>
    /// <remarks>
    /// Single-threaded WebAssembly runtimes (<c>browser-wasm</c> and <c>wasi-wasm</c>) do not have a
    /// thread pool: <c>Task.Run</c> continuations never execute while the single thread is busy, and
    /// blocking waits (<c>Task.Wait</c> / <c>GetAwaiter().GetResult()</c> on an incomplete task) throw
    /// <see cref="PlatformNotSupportedException"/>. Code that would otherwise offload work to a
    /// background thread MUST fall back to inline/synchronous execution when this returns
    /// <see langword="false"/>, otherwise the operation deadlocks or throws.
    /// <para>
    /// This is the single condition to branch on before calling <c>ITask.RunLongRunning</c>, creating a
    /// <see cref="System.Threading.Thread"/>, or using a blocking primitive such as
    /// <c>BlockingCollection&lt;T&gt;</c>. Do not hand-roll an equivalent
    /// <see cref="OperatingSystem"/> probe at the call site. There are two established fallbacks,
    /// and which one applies depends on whether the work is observable:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///     Best-effort background diagnostics (shutdown watchdog, slow-test reporters) are simply
    ///     <b>skipped</b>. Scheduling them with <c>Task.Run</c> instead would not help: the loop
    ///     would not run while the single thread is busy, and awaiting it during teardown would hang.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Work whose result is observable (the TRX streaming store's sidecar writer) is restructured
    ///     to run <b>inline</b> on the calling thread. Note that swapping the producer/consumer
    ///     hand-off for <c>Task.Run</c> is not sufficient on its own when the loop body blocks —
    ///     the blocking wait has to be removed as well.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// .NET 11 exposes <c>System.Runtime.CompilerServices.RuntimeFeature.IsMultithreadingSupported</c>
    /// for exactly this purpose (approved and implemented via
    /// <see href="https://github.com/dotnet/runtime/issues/77541"/>), and it is additionally a trimming
    /// feature switch, so it can light up dead-code elimination. The platform still targets earlier
    /// frameworks, so we derive the same information from the <see cref="OperatingSystem"/>
    /// single-threaded-wasm probes, which are available since .NET 8. Once <c>net11.0</c> is in
    /// <c>SupportedNetFrameworks</c> this property should forward to the BCL API under a
    /// <c>#if NET11_0_OR_GREATER</c>.
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
