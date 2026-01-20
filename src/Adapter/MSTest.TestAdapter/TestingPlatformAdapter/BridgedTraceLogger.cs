// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Logging;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

// Type is serializable to support serialization through AppDomains but ILogger is not so we handle it being null
// when we are inside the AppDomain.
// TODO: We should either not support AppDomains at all or make a marshaling version that would send the message and
// enum to the outside AppDomain instance that would then log it.
[Serializable]
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "MTP logger bridge")]
internal sealed class BridgedTraceLogger : IAdapterTraceLogger
{
    [NonSerialized]
    private readonly ILogger? _logger;

    public bool IsInfoEnabled => _logger?.IsEnabled(LogLevel.Information) == true;

    public bool IsWarningEnabled => _logger?.IsEnabled(LogLevel.Warning) == true;

    public bool IsErrorEnabled => _logger?.IsEnabled(LogLevel.Error) == true;

    public bool IsVerboseEnabled => _logger?.IsEnabled(LogLevel.Debug) == true;

    // This constructor is used when the logger is not available, e.g., in AppDomains.
    public BridgedTraceLogger()
        => _logger = null;

    public BridgedTraceLogger(ILogger logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public void LogError(string format, params object?[] args)
    {
        if (_logger?.IsEnabled(LogLevel.Error) == true)
        {
            _logger.LogError(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

    public void LogInfo(string format, params object?[] args)
    {
        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

    public void LogWarning(string format, params object?[] args)
    {
        if (_logger?.IsEnabled(LogLevel.Warning) == true)
        {
            _logger.LogWarning(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

    public void LogVerbose(string format, params object?[] args)
    {
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            _logger.LogDebug(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

#if NETFRAMEWORK
    public void PrepareRemoteAppDomain(AppDomain appDomain)
    {
    }
#endif
}
#endif
