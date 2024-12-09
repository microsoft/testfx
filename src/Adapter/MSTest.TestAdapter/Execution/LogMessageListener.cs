// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Listens for log messages and Debug.WriteLine
/// Note that this class is not thread-safe and thus should only be used when unit tests are being run serially.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class LogMessageListener : IDisposable
{
    private static readonly Lock TraceLock = new();
    private static int s_listenerCount;
    private static ThreadSafeStringWriter? s_redirectedDebugTrace;

    /// <summary>
    /// Trace listener to capture Trace.WriteLines in the test cases.
    /// </summary>
    private static ITraceListener? s_traceListener;
    private readonly ThreadSafeStringWriter _redirectedStandardOutput;
    private readonly ThreadSafeStringWriter _redirectedStandardError;
    private readonly bool _captureDebugTraces;

    /// <summary>
    /// Trace listener Manager to perform operation on trace listener objects.
    /// </summary>
    private ITraceListenerManager? _traceListenerManager;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogMessageListener"/> class.
    /// </summary>
    /// <param name="captureDebugTraces">Captures debug traces if true.</param>
    public LogMessageListener(bool captureDebugTraces)
    {
        // Cache the original output/error streams and replace it with the own stream.
        _redirectedStandardOutput = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "out");
        _redirectedStandardError = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "err");

        Logger.OnLogMessage += _redirectedStandardOutput.WriteLine;

        _captureDebugTraces = captureDebugTraces;
        if (!_captureDebugTraces)
        {
            return;
        }

        // This is awkward, it has a side-effect of setting up Console output redirection, but the naming is suggesting that we are
        // just getting TraceListener manager.
        _traceListenerManager = PlatformServiceProvider.Instance.GetTraceListenerManager(_redirectedStandardOutput, _redirectedStandardError);

        // The Debug listener uses Debug.WriteLine and Debug.Write to write the messages, which end up written into Trace.Listeners.
        // These listeners are static and hence shared across the whole process. We need to capture Debug output only for the current
        // test, which was historically done by registering a listener in constructor of this class, and by removing the listener on Dispose.
        // The newly created listener replaced previously registered listener, which was remembered, and put back on dispose.
        //
        // This works well as long as there are no tests running in parallel. But as soon as there are tests running in parallel. Then all the
        // debug output of all tests will be output into the test that was most recently created (because it registered the listener most recently).
        //
        // To prevent mixing of outputs, the ThreadSafeStringWriter was re-implemented for net46 and newer to leverage AsyncLocal, which allows the writer to
        // write only to the output of the current test. This leaves the LogMessageListener with only one task. Make sure that a trace listener is registered
        // as long as there is any active test. This is still done by constructor and Dispose, but instead of replacing the listener every time, we use listenerCount
        // to only add the listener when there is none, and remove it when we are the last one to dispose.
        //
        // This would break the behavior for net451, but that functionality was moved further into ThreadSafeStringWriter.
        lock (TraceLock)
        {
            if (s_listenerCount == 0)
            {
                s_redirectedDebugTrace = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "trace");
                s_traceListener = PlatformServiceProvider.Instance.GetTraceListener(s_redirectedDebugTrace);
                _traceListenerManager.Add(s_traceListener);
            }

            s_listenerCount++;
        }
    }

    ~LogMessageListener()
    {
        Dispose(false);
    }

    /// <summary>
    /// Gets logger output.
    /// </summary>
    public string StandardOutput => _redirectedStandardOutput.ToString();

    /// <summary>
    /// Gets 'Error' Output from the redirected stream.
    /// </summary>
    public string StandardError => _redirectedStandardError.ToString();

    /// <summary>
    /// Gets 'Trace' Output from the redirected stream.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Part of the public API")]
    public string? DebugTrace => s_redirectedDebugTrace?.ToString();

    public string? GetAndClearStandardOutput()
    {
        string? output = _redirectedStandardOutput.ToStringAndClear();
        return output;
    }

    public string? GetAndClearStandardError()
    {
        string? output = _redirectedStandardError.ToStringAndClear();
        return output;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Part of the public API")]
    public string? GetAndClearDebugTrace()
        => s_redirectedDebugTrace?.ToStringAndClear();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing || _isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Logger.OnLogMessage -= _redirectedStandardOutput.WriteLine;
        Logger.OnLogMessage -= _redirectedStandardError.WriteLine;

        _redirectedStandardOutput.Dispose();
        _redirectedStandardError.Dispose();

        if (!_captureDebugTraces)
        {
            return;
        }

        lock (TraceLock)
        {
            if (s_listenerCount == 1)
            {
                try
                {
                    if (s_traceListener != null)
                    {
                        _traceListenerManager?.Remove(s_traceListener);
                    }
                }
                catch (Exception e)
                {
                    // Catch all exceptions since Dispose should not throw.
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogError("ConsoleOutputRedirector.Dispose threw exception: {0}", e);
                }

                if (s_traceListener != null)
                {
                    // Dispose trace manager and listeners
                    _traceListenerManager?.Dispose(s_traceListener);
                    _traceListenerManager = null;
                    s_traceListener = null;
                }
            }

            s_listenerCount--;
        }
    }
}
