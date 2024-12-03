// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class ServerModePerCallOutputDevice : IPlatformOutputDevice
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentBag<ServerLogMessage> _messages = new();

    private IServerTestHost? _serverTestHost;

    private static readonly string[] NewLineStrings = { "\r\n", "\n" };

    public ServerModePerCallOutputDevice(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    internal async Task InitializeAsync(IServerTestHost serverTestHost)
    {
        _serverTestHost = serverTestHost;

        foreach (ServerLogMessage message in _messages)
        {
            await LogAsync(message);
        }

        _messages.Clear();
    }

    public string Uid => nameof(ServerModePerCallOutputDevice);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ServerModePerCallOutputDevice);

    public string Description => nameof(ServerModePerCallOutputDevice);

    public async Task DisplayAfterSessionEndRunAsync()
        => await LogAsync(LogLevel.Trace, PlatformResources.FinishedTestSession, padding: null);

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data)
    {
        switch (data)
        {
            case FormattedTextOutputDeviceData formattedTextOutputDeviceData:
                await LogAsync(LogLevel.Information, formattedTextOutputDeviceData.Text, formattedTextOutputDeviceData.Padding);
                break;

            case TextOutputDeviceData textOutputDeviceData:
                await LogAsync(LogLevel.Information, textOutputDeviceData.Text, padding: null);
                break;

            case WarningMessageOutputDeviceData warningData:
                await LogAsync(LogLevel.Warning, warningData.Message, padding: null);
                break;

            case ErrorMessageOutputDeviceData errorData:
                await LogAsync(LogLevel.Error, errorData.Message, padding: null);
                break;

            case ExceptionOutputDeviceData exceptionOutputDeviceData:
                await LogAsync(LogLevel.Error, exceptionOutputDeviceData.Exception.ToString(), padding: null);
                break;
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
            await LogAsync(LogLevel.Trace, string.Format(CultureInfo.InvariantCulture, PlatformResources.StartingTestSessionWithLogFilePath, logFileName), padding: null);
        }
        else
        {
            await LogAsync(LogLevel.Trace, PlatformResources.StartingTestSession, padding: null);
        }
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    private async Task LogAsync(LogLevel logLevel, string message, int? padding)
        => await LogAsync(GetServerLogMessage(logLevel, message, padding));

    private async Task LogAsync(ServerLogMessage message)
    {
        if (_serverTestHost is null)
        {
            _messages.Add(message);
        }
        else
        {
            await _serverTestHost.PushDataAsync(message);
        }
    }

    private static ServerLogMessage GetServerLogMessage(LogLevel logLevel, string message, int? padding)
        => new(logLevel, GetIndentedMessage(message, padding));

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
