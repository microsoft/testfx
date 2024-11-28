// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal class ServerModePerCallOutputDevice : IPlatformOutputDevice
{
    private readonly IServerTestHost _serverTestHost;
    private readonly IAsyncMonitor _asyncMonitor;
    private readonly IServiceProvider _serviceProvider;

    private static readonly string[] NewLineStrings = { "\r\n", "\n" };

    public ServerModePerCallOutputDevice(IServerTestHost serverTestHost, IAsyncMonitor asyncMonitor, IServiceProvider serviceProvider)
    {
        _serverTestHost = serverTestHost;
        _asyncMonitor = asyncMonitor;
        _serviceProvider = serviceProvider;
    }

    public string Uid => nameof(ServerModePerCallOutputDevice);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ServerModePerCallOutputDevice);

    public string Description => nameof(ServerModePerCallOutputDevice);

    public async Task DisplayAfterSessionEndRunAsync()
        => await LogAsync(LogLevel.Trace, /*TODO: Localize*/"Finished test session", padding: null);

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data)
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            switch (data)
            {
                case FormattedTextOutputDeviceData formattedTextOutputDeviceData:
                    LogLevel logLevel = formattedTextOutputDeviceData.ForegroundColor is SystemConsoleColor color
                        ? color.ConsoleColor switch
                        {
                            ConsoleColor.Red => LogLevel.Error,
                            ConsoleColor.Yellow => LogLevel.Warning,
                            _ => LogLevel.Information,
                        }
                        : LogLevel.Information;

                    await LogAsync(logLevel, formattedTextOutputDeviceData.Text, formattedTextOutputDeviceData.Padding);
                    break;

                case TextOutputDeviceData textOutputDeviceData:
                    await LogAsync(LogLevel.Information, textOutputDeviceData.Text, padding: null);
                    break;

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    await LogAsync(LogLevel.Error, exceptionOutputDeviceData.Exception.ToString(), padding: null);
                    break;
            }
        }
    }

    public async Task DisplayBannerAsync(string? bannerMessage)
    {
        if (bannerMessage is not null)
        {
            await LogAsync(LogLevel.Debug, bannerMessage, padding: null);
        }
    }

    public async Task DisplayBeforeSessionStartAsync()
    {
        if (_serviceProvider.GetService<FileLoggerProvider>() is { FileLogger.FileName: { } logFileName })
        {
            await LogAsync(LogLevel.Trace, $"Starting test session. Log file path is '{logFileName}'."/*TODO: Localize*/, padding: null);
        }
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    private async Task LogAsync(LogLevel logLevel, string message, int? padding)
    {
        message = GetIndentedMessage(message, padding);
        ServerLogMessage logMessage = new(logLevel, message);
        await _serverTestHost.PushDataAsync(logMessage);
    }

    private static string GetIndentedMessage(string message, int? padding)
    {
        int paddingValue = padding.GetValueOrDefault();
        if (paddingValue == 0)
        {
            return message;
        }

        string indent = new(' ', paddingValue);

        if (!message.Contains('\n'))
        {
            return indent + message;
        }

        string[] lines = message.Split(NewLineStrings, StringSplitOptions.None);
        StringBuilder builder = new();
        foreach (string line in lines)
        {
            builder.Append(indent);
            builder.AppendLine(line);
        }

        return builder.ToString();
    }
}
