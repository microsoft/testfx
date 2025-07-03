// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Logging;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "MTP logger bridge")]
internal sealed class BridgedTraceLogger : IAdapterTraceLogger
{
    private readonly ILogger _logger;

    public BridgedTraceLogger(ILogger logger)
        => _logger = logger;

    public void LogError(string format, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.LogError(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

    public void LogInfo(string format, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

    public void LogWarning(string format, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }
}
#endif
