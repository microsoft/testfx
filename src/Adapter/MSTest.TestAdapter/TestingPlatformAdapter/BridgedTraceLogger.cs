// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.Logging;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Bridges the MSTest adapter trace logging interface to the Microsoft Testing Platform logger.
/// </summary>
/// <remarks>
/// On .NET Framework, this class inherits from <see cref="MarshalByRefObject"/> to support
/// cross-AppDomain logging. When the logger is set on objects in child AppDomains (like
/// <see cref="TestPlatform.MSTestAdapter.PlatformServices.AssemblyResolver"/>), calls are
/// proxied back to the parent domain where the actual <see cref="ILogger"/> instance resides.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "MTP logger bridge")]
internal sealed class BridgedTraceLogger :
#if NETFRAMEWORK
    MarshalByRefObject,
#endif
    IAdapterTraceLogger
{
    private readonly ILogger _logger;

    public bool IsInfoEnabled => _logger.IsEnabled(LogLevel.Information);

    public bool IsWarningEnabled => _logger.IsEnabled(LogLevel.Warning);

    public bool IsErrorEnabled => _logger.IsEnabled(LogLevel.Error);

    public bool IsVerboseEnabled => _logger.IsEnabled(LogLevel.Debug);

    public BridgedTraceLogger(ILogger logger)
        => _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

    public void LogVerbose(string format, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(string.Format(CultureInfo.CurrentCulture, format, args));
        }
    }

#if NETFRAMEWORK
    public void PrepareRemoteAppDomain(AppDomain appDomain)
        // Force loading Microsoft.Testing.Platform in the child AppDomain to ensure the ILogger
        // type can be resolved when creating a transparent proxy for this MarshalByRefObject.
        => appDomain.Load(typeof(ILogger).Assembly.GetName());
#endif
}
#endif
