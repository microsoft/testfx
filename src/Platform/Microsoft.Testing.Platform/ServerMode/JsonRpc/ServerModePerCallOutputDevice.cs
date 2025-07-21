// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class ServerModePerCallOutputDevice : IPlatformOutputDevice, IOutputDeviceDataProducer
{
    private readonly FileLoggerProvider? _fileLoggerProvider;
    private readonly IStopPoliciesService _policiesService;
    private readonly ConcurrentBag<ServerLogMessage> _messages = [];

    private ServerTestHost? _serverTestHost;

    private static readonly string[] NewLineStrings = ["\r\n", "\n"];

    public ServerModePerCallOutputDevice(FileLoggerProvider? fileLoggerProvider, IStopPoliciesService policiesService)
    {
        _fileLoggerProvider = fileLoggerProvider;
        _policiesService = policiesService;
    }

    internal async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        // Server mode output device is basically used to send messages to Test Explorer.
        // For that, it needs the ServerTestHost.
        // However, the ServerTestHost is available later than the time we create the output device.
        // So, the server mode output device is initially created early without the ServerTestHost, and
        // it keeps any messages in a list.
        // Later when ServerTestHost is created and is available, we initialize the server mode output device.
        // The initialization will setup the right state for pushing to Test Explorer, and will push any existing
        // messages to Test Explorer as well.
        _serverTestHost = serverTestHost;

        foreach (ServerLogMessage message in _messages)
        {
            await LogAsync(message, serverTestHost.ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken).ConfigureAwait(false);
        }

        _messages.Clear();
    }

    public string Uid => nameof(ServerModePerCallOutputDevice);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(ServerModePerCallOutputDevice);

    public string Description => nameof(ServerModePerCallOutputDevice);

    public async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
        => await LogAsync(LogLevel.Trace, PlatformResources.FinishedTestSession, padding: null, cancellationToken).ConfigureAwait(false);

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        switch (data)
        {
            case FormattedTextOutputDeviceData formattedTextOutputDeviceData:
                await LogAsync(LogLevel.Information, formattedTextOutputDeviceData.Text, formattedTextOutputDeviceData.Padding, cancellationToken).ConfigureAwait(false);
                break;

            case TextOutputDeviceData textOutputDeviceData:
                await LogAsync(LogLevel.Information, textOutputDeviceData.Text, padding: null, cancellationToken).ConfigureAwait(false);
                break;

            case WarningMessageOutputDeviceData warningData:
                await LogAsync(LogLevel.Warning, warningData.Message, padding: null, cancellationToken).ConfigureAwait(false);
                break;

            case ErrorMessageOutputDeviceData errorData:
                await LogAsync(LogLevel.Error, errorData.Message, padding: null, cancellationToken).ConfigureAwait(false);
                break;

            case ExceptionOutputDeviceData exceptionOutputDeviceData:
                await LogAsync(LogLevel.Error, exceptionOutputDeviceData.Exception.ToString(), padding: null, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    public async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        if (bannerMessage is not null)
        {
            await LogAsync(LogLevel.Debug, bannerMessage, padding: null, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
    {
        if (_fileLoggerProvider is { FileLogger.FileName: { } logFileName })
        {
            await LogAsync(LogLevel.Trace, string.Format(CultureInfo.InvariantCulture, PlatformResources.StartingTestSessionWithLogFilePath, logFileName), padding: null, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await LogAsync(LogLevel.Trace, PlatformResources.StartingTestSession, padding: null, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    private async Task LogAsync(LogLevel logLevel, string message, int? padding, CancellationToken cancellationToken)
        => await LogAsync(GetServerLogMessage(logLevel, message, padding), cancellationToken).ConfigureAwait(false);

    private async Task LogAsync(ServerLogMessage message, CancellationToken cancellationToken)
    {
        if (_serverTestHost is null)
        {
            _messages.Add(message);
        }
        else
        {
            await _serverTestHost.PushDataAsync(message, cancellationToken).ConfigureAwait(false);
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

    public async Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken)
    {
        if (processRole == TestProcessRole.TestHost)
        {
            await _policiesService.RegisterOnMaxFailedTestsCallbackAsync(
                async (maxFailedTests, _) => await DisplayAsync(
                    this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ReachedMaxFailedTestsMessage, maxFailedTests)), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
