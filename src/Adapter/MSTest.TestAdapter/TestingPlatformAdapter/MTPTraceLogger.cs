// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Logging;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "MTP logger bridge")]
internal sealed class MTPTraceLogger : MarshalByRefObject, ITraceLogger
{
    private readonly ILogger _logger;

    public MTPTraceLogger(ILogger logger)
        => _logger = logger;

    public bool IsVerboseEnabled
        => _logger.IsEnabled(LogLevel.Trace);

    public bool IsInfoEnabled
        => _logger.IsEnabled(LogLevel.Information);

    public bool IsWarningEnabled
        => _logger.IsEnabled(LogLevel.Warning);

    public bool IsErrorEnabled
        => _logger.IsEnabled(LogLevel.Warning);

    public void Verbose(string format, params object?[] args)
        => _logger.LogTrace(string.Format(CultureInfo.InvariantCulture, format, args));

    public void Verbose(string message)
        => _logger.LogTrace(message);

    public void Info(string format, params object?[] args)
        => _logger.LogInformation(string.Format(CultureInfo.InvariantCulture, format, args));

    public void Info(string message)
        => _logger.LogInformation(message);

    public void Warning(string format, params object?[] args)
        => _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, format, args));

    public void Warning(string message)
        => _logger.LogWarning(message);

    public void Error(string format, params object?[] args)
        => _logger.LogError(string.Format(CultureInfo.InvariantCulture, format, args));

    public void Error(string message)
        => _logger.LogError(message);

    public void Error(Exception exceptionToTrace)
        => _logger.LogError(exceptionToTrace);

#if NETFRAMEWORK
    public void SetupRemoteEqtTraceListeners(AppDomain? childDomain)
    {
        // No-op for MTP.
    }
#endif
}
#endif
