// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.VideoRecorder;

/// <summary>
/// Entry point used by test code to access the video recording service.
/// </summary>
/// <remarks>
/// The recorder is registered by calling <c>AddVideoRecorderProvider()</c> on the test
/// application builder and enabled with <c>--capture-video</c>. Once the test session starts,
/// <see cref="Current"/> points to the live recorder; it is reset when the session finishes. If
/// the extension is not registered/enabled, <see cref="Current"/> returns a no-op recorder so
/// test code never throws.
/// <para>
/// This is a per-process singleton intended for a single running test application. It is not
/// designed for multiple <c>ITestApplication</c> instances built in the same process.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class VideoRecorder
{
    private static IVideoRecorder? s_current;

    /// <summary>
    /// Gets the current video recorder, or a no-op recorder when the extension is not registered.
    /// </summary>
    public static IVideoRecorder Current => Volatile.Read(ref s_current) ?? NullVideoRecorder.Instance;

    internal static void SetCurrent(IVideoRecorder recorder)
        => Volatile.Write(ref s_current, recorder);

    // Clear the registration only if it still points at this recorder, so a handler never resets
    // a registration that a different one has since installed.
    internal static void ResetCurrent(IVideoRecorder recorder)
        => Interlocked.CompareExchange(ref s_current, null, recorder);

    private sealed class NullVideoRecorder : IVideoRecorder
    {
        public static readonly NullVideoRecorder Instance = new();

        public bool IsAvailable => false;

        public bool IsRecording => false;

        public void Start(string? name = null)
        {
            // No recorder registered: do nothing.
        }

        public Task<string?> StopAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }
}
