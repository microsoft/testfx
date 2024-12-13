// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// A service to log any trace messages from the adapter that would be shown in *.TpTrace files.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class AdapterTraceLogger : IAdapterTraceLogger
{
    /// <summary>
    /// Log an error in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogError(string format, params object?[] args)
    {
#if !WINDOWS_UWP && !WIN_UI
        if (EqtTrace.IsErrorEnabled)
        {
            EqtTrace.Error(PrependAdapterName(format), args);
        }
#else
#pragma warning disable IDE0022 // Use expression body for method
        EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, format, args);
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

    /// <summary>
    /// Log a warning in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogWarning(string format, params object?[] args)
    {
#if !WINDOWS_UWP && !WIN_UI
        if (EqtTrace.IsWarningEnabled)
        {
            EqtTrace.Warning(PrependAdapterName(format), args);
        }
#else
#pragma warning disable IDE0022 // Use expression body for method
        EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, format, args);
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

    /// <summary>
    /// Log an information message in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogInfo(string format, params object?[] args)
    {
#if !WINDOWS_UWP && !WIN_UI
        if (EqtTrace.IsInfoEnabled)
        {
            EqtTrace.Info(PrependAdapterName(format), args);
        }
#else
#pragma warning disable IDE0022 // Use expression body for method
        EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, format, args);
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

#if !WINDOWS_UWP && !WIN_UI
    private static string PrependAdapterName(string format) => $"MSTest - {format}";
#endif
}
