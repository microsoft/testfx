// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Logging;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Provides a bridge for logging messages using an <see cref="ILogger"/> instance.
/// </summary>
/// <remarks>
/// This class adapts logging calls to the <see cref="ILogger"/> interface, allowing messages to be
/// logged at various levels such as Error, Information, Debug, and Warning. It checks if the respective log level is
/// enabled before logging the message.
/// </remarks>
/// <param name="logger">The real logger.</param>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "MTP logger bridge")]
// Type is serializable to support serialization through AppDomains but ILogger is not so we handle it being null
// when we are inside the AppDomain.
// TODO: We should either not support AppDomains at all or make a marshaling version that would send the message and
// enum to the outside AppDomain instance that would then log it.
[Serializable]
internal sealed class BridgedTraceLogger(ILogger logger) : IAdapterTraceLogger
{
    public bool IsInfoEnabled => logger?.IsEnabled(LogLevel.Information) ?? false;

    public void LogError(string format, params object?[] args)
    {
        if (logger?.IsEnabled(LogLevel.Error) == true)
        {
            logger.LogError(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

    public void LogInfo(string format, params object?[] args)
    {
        if (logger?.IsEnabled(LogLevel.Information) == true)
        {
            logger.LogInformation(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

    public void LogVerbose(string format, params object?[] args)
    {
        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            logger.LogDebug(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

    public void LogWarning(string format, params object?[] args)
    {
        if (logger?.IsEnabled(LogLevel.Warning) == true)
        {
            logger.LogWarning(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }
}
#endif
