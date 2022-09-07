// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

/// <summary>
/// A service to log any trace messages from the adapter that would be shown in *.TpTrace files.
/// </summary>
public class AdapterTraceLogger : IAdapterTraceLogger
{
    /// <summary>
    /// Log an error in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogError(string format, params object[] args)
    {
#if NETFRAMEWORK || NETSTANDARD || (NETCOREAPP && !WIN_UI)
        if (EqtTrace.IsErrorEnabled)
        {
            EqtTrace.Error(PrependAdapterName(format), args);
        }
#else
        EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, format, args);
#endif
    }

    /// <summary>
    /// Log a warning in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogWarning(string format, params object[] args)
    {
#if NETFRAMEWORK || NETSTANDARD || (NETCOREAPP && !WIN_UI)
        if (EqtTrace.IsWarningEnabled)
        {
            EqtTrace.Warning(PrependAdapterName(format), args);
        }
#else
        EqtTrace.WarningIf(EqtTrace.IsWarningEnabled, format, args);
#endif
    }

    /// <summary>
    /// Log an information message in a given format.
    /// </summary>
    /// <param name="format"> The format. </param>
    /// <param name="args"> The args. </param>
    public void LogInfo(string format, params object[] args)
    {
#if NETFRAMEWORK || NETSTANDARD || (NETCOREAPP && !WIN_UI)
        if (EqtTrace.IsInfoEnabled)
        {
            EqtTrace.Info(PrependAdapterName(format), args);
        }
#else
        EqtTrace.InfoIf(EqtTrace.IsInfoEnabled, format, args);
#endif
    }

#if NETFRAMEWORK || NETSTANDARD || (NETCOREAPP && !WIN_UI)
    private static string PrependAdapterName(string format)
    {
        return $"MSTest - {format}";
    }
#endif
}
