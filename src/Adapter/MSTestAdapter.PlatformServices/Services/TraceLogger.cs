// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// A service to log any trace messages from the adapter that would be shown in *.TpTrace files.
/// </summary>
#if NETFRAMEWORK
internal sealed class AdapterTraceLogger : MarshalByRefObject, IAdapterTraceLogger
#else
internal sealed class AdapterTraceLogger : IAdapterTraceLogger
#endif
{
    public bool IsInfoEnabled => EqtTrace.IsInfoEnabled;

    public bool IsWarningEnabled => EqtTrace.IsWarningEnabled;

    public bool IsErrorEnabled => EqtTrace.IsErrorEnabled;

    public bool IsVerboseEnabled => EqtTrace.IsVerboseEnabled;

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void LogVerbose(string format, params object?[] args)
    {
#if !WINDOWS_UWP && !WIN_UI
        if (EqtTrace.IsVerboseEnabled)
        {
            EqtTrace.Verbose(PrependAdapterName(format), args);
        }
#else
#pragma warning disable IDE0022 // Use expression body for method
        EqtTrace.VerboseIf(EqtTrace.IsVerboseEnabled, format, args);
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

#if NETFRAMEWORK
    /// <inheritdoc />
    public void PrepareRemoteAppDomain(AppDomain appDomain)
    {
        // Force loading Microsoft.TestPlatform.CoreUtilities in the new app domain to ensure there is no assembly resolution issue.
        // For unknown reasons, with MSTest 3.4+ we start to see infinite cycles of assembly resolution of this dll in the new app
        // domain. In older versions, this was not the case, and the callback was allowing to fully lookup and load the dll before
        // triggering the next resolution.
        appDomain.Load(typeof(EqtTrace).Assembly.GetName());
        EqtTrace.SetupRemoteEqtTraceListeners(appDomain);
    }
#endif

#if !WINDOWS_UWP && !WIN_UI
    private static string PrependAdapterName(string format) => $"MSTest - {format}";
#endif
}
