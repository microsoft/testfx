// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.ServerMode;

internal sealed class ServerModePerCallOutputDevice : IPlatformOutputDevice, IDisposable
{
    private readonly FileLoggerProvider? _fileLoggerProvider;
    private readonly ConcurrentQueue<ServerLogMessage> _messages = [];
    private readonly Dictionary<ProgressMessageIdentity, string> _progressMessages = [];
    private readonly SemaphoreSlim _progressMessagesSemaphore = new(1, 1);

    private ServerTestHost? _serverTestHost;

    private static readonly string[] NewLineStrings = ["\r\n", "\n"];

    public ServerModePerCallOutputDevice(FileLoggerProvider? fileLoggerProvider)
        => _fileLoggerProvider = fileLoggerProvider;

    internal async Task InitializeAsync(ServerTestHost serverTestHost)
    {
        // Opted-in clients attach after initialize; legacy clients attach at discovery/run.
        // Share the lock with enqueueing to avoid stranding messages during handover.
        lock (_messages)
        {
            if (_serverTestHost == serverTestHost)
            {
                return;
            }

            _serverTestHost = serverTestHost;
        }

        while (_messages.TryDequeue(out ServerLogMessage? message))
        {
            await LogAsync(message, serverTestHost.ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken).ConfigureAwait(false);
        }
    }

    public string Uid => nameof(ServerModePerCallOutputDevice);

    public string Version => PlatformVersion.Version;

    public string DisplayName => nameof(ServerModePerCallOutputDevice);

    public string Description => nameof(ServerModePerCallOutputDevice);

    public async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
    {
        await _progressMessagesSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _progressMessages.Clear();
        }
        finally
        {
            _progressMessagesSemaphore.Release();
        }

        await LogAsync(LogLevel.Trace, PlatformResources.FinishedTestSession, padding: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
        => await ForwardAsync(producer, data, cancellationToken).ConfigureAwait(false);

    internal async Task<bool> ForwardAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        bool forwarded = true;
        switch (data)
        {
            case SessionMessageOutputDeviceData sessionMessageData:
                forwarded = await LogAsync(LogLevel.Information, sessionMessageData.Message, padding: null, cancellationToken).ConfigureAwait(false);
                break;

            case ProgressMessageOutputDeviceData progressMessageData:
                var identity = new ProgressMessageIdentity(producer.Uid, progressMessageData.Key);
                await _progressMessagesSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (progressMessageData.Message is null)
                    {
                        _progressMessages.Remove(identity);
                    }
                    else if (!_progressMessages.TryGetValue(identity, out string? existingMessage)
                        || existingMessage != progressMessageData.Message)
                    {
                        _progressMessages[identity] = progressMessageData.Message;
                        forwarded = await LogAsync(LogLevel.Information, progressMessageData.Message, padding: null, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _progressMessagesSemaphore.Release();
                }

                break;

            case FormattedTextOutputDeviceData formattedTextOutputDeviceData:
                forwarded = await LogAsync(LogLevel.Information, formattedTextOutputDeviceData.Text, formattedTextOutputDeviceData.Padding, cancellationToken).ConfigureAwait(false);
                break;

            case TextOutputDeviceData textOutputDeviceData:
                forwarded = await LogAsync(LogLevel.Information, textOutputDeviceData.Text, padding: null, cancellationToken).ConfigureAwait(false);
                break;

            case WarningMessageOutputDeviceData warningData:
                forwarded = await LogAsync(LogLevel.Warning, warningData.Message, padding: null, cancellationToken).ConfigureAwait(false);
                break;

            case ErrorMessageOutputDeviceData errorData:
                forwarded = await LogAsync(LogLevel.Error, errorData.Message, padding: null, cancellationToken).ConfigureAwait(false);
                break;

            case ExceptionOutputDeviceData exceptionOutputDeviceData:
                forwarded = await LogAsync(LogLevel.Error, exceptionOutputDeviceData.Exception.ToString(), padding: null, cancellationToken).ConfigureAwait(false);
                break;
        }

        return forwarded;
    }

    private readonly record struct ProgressMessageIdentity(string ProducerUid, string Key);

    public async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
        => await ForwardBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);

    internal async Task<bool> ForwardBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
        => bannerMessage is null || await LogAsync(LogLevel.Debug, bannerMessage, padding: null, cancellationToken).ConfigureAwait(false);

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

    public void Dispose()
        => _progressMessagesSemaphore.Dispose();

    private async Task<bool> LogAsync(LogLevel logLevel, string message, int? padding, CancellationToken cancellationToken)
        => await LogAsync(GetServerLogMessage(logLevel, message, padding), cancellationToken).ConfigureAwait(false);

    private async Task<bool> LogAsync(ServerLogMessage message, CancellationToken cancellationToken)
    {
        ServerTestHost? serverTestHost;
        lock (_messages)
        {
            serverTestHost = _serverTestHost;
            if (serverTestHost is null)
            {
                _messages.Enqueue(message);
                return true;
            }
        }

        return await serverTestHost.TryPushLogAsync(message, cancellationToken).ConfigureAwait(false);
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

    public Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
