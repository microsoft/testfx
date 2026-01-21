// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// A service to log any trace messages from the adapter that would be shown in *.TpTrace files.
/// </summary>
internal sealed class AdapterTraceLogger : IAdapterTraceLogger
{
    /// <summary>
    /// Log an error in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogError(string format, params object?[] args)
    {
        if (TraceLoggerHelper.Instance.IsErrorEnabled)
        {
#if !WINDOWS_UWP && !WIN_UI
            TraceLoggerHelper.Instance.Error(PrependAdapterName(format), args);
#else
            TraceLoggerHelper.Instance.Error(format, args);
#endif
        }
    }

    /// <summary>
    /// Log a warning in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogWarning(string format, params object?[] args)
    {
        if (TraceLoggerHelper.Instance.IsWarningEnabled)
        {
#if !WINDOWS_UWP && !WIN_UI
            TraceLoggerHelper.Instance.Warning(PrependAdapterName(format), args);
#else
            TraceLoggerHelper.Instance.Warning(format, args);
#endif
        }
    }

    /// <summary>
    /// Log an information message in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogInfo(string format, params object?[] args)
    {
        if (TraceLoggerHelper.Instance.IsInfoEnabled)
        {
#if !WINDOWS_UWP && !WIN_UI
            TraceLoggerHelper.Instance.Info(PrependAdapterName(format), args);
#else
            TraceLoggerHelper.Instance.Info(format, args);
#endif
        }
    }

#if !WINDOWS_UWP && !WIN_UI
    private static string PrependAdapterName(string format) => $"MSTest - {format}";
#endif
}
