// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal sealed partial class TestContextImplementation
{
    private static readonly AsyncLocal<LiveOutputScope?> CurrentLiveOutputScope = new();
    private static TextWriter? s_liveOutputWriter;

    private sealed class LiveOutputScope(TestContext? testContext)
    {
        private int _isActive = 1;

        internal TestContext? TestContext { get; } = testContext;

        internal bool IsActive => Volatile.Read(ref _isActive) == 1;

        internal void Deactivate()
            => Volatile.Write(ref _isActive, 0);
    }

    private sealed class LiveOutputWriterScope(TextWriter? previousLiveOutputWriter) : IDisposable
    {
        public void Dispose()
            => Volatile.Write(ref s_liveOutputWriter, previousLiveOutputWriter);
    }

    // This writer is captured together with the process-wide Console routers and shares their install-once lifetime.
    internal static void ConfigureLiveOutputWriter(TextWriter liveOutputWriter)
        => Volatile.Write(ref s_liveOutputWriter, liveOutputWriter);

    internal static IDisposable SetLiveOutputWriterForTesting(TextWriter liveOutputWriter)
    {
        TextWriter? previousLiveOutputWriter = Volatile.Read(ref s_liveOutputWriter);
        Volatile.Write(ref s_liveOutputWriter, liveOutputWriter);

        return new LiveOutputWriterScope(previousLiveOutputWriter);
    }

    private void WriteLive(string? message, bool appendLine)
    {
        if (_liveOutputWriter is null
            || _outputCaptureModeProvider() != TestOutputCaptureMode.Live
            || CurrentLiveOutputScope.Value is not { IsActive: true } liveOutputScope
            || !ReferenceEquals(liveOutputScope.TestContext, this))
        {
            return;
        }

        if (appendLine)
        {
            _liveOutputWriter.WriteLine(message);
        }
        else
        {
            _liveOutputWriter.Write(message);
        }
    }
}
