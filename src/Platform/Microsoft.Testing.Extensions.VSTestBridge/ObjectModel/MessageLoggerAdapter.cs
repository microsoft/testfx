// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.Helpers;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

/// <summary>
/// Bridge implementation of <see cref="IMessageLogger"/> that forwards calls to VSTest and Microsoft Testing Platforms.
/// </summary>
internal sealed class MessageLoggerAdapter : IMessageLogger, IOutputDeviceDataProducer
{
    /// <remarks>
    /// Not null when used in the context of VSTest.
    /// </remarks>
    private readonly IMessageLogger? _messageLogger;
    private readonly ILogger<MessageLoggerAdapter> _logger;
    private readonly IOutputDevice _outputDevice;
    private readonly IExtension _extension;

    public MessageLoggerAdapter(ILoggerFactory loggerFactory, IOutputDevice outputDevice, IExtension extension,
        IMessageLogger? messageLogger = null)
    {
        _outputDevice = outputDevice;
        _extension = extension;
        _messageLogger = messageLogger;
        _logger = loggerFactory.CreateLogger<MessageLoggerAdapter>();
    }

    string IExtension.Uid => _extension.Uid;

    string IExtension.Version => _extension.Version;

    string IExtension.DisplayName => _extension.DisplayName;

    string IExtension.Description => _extension.Description;

    /// <inheritdoc/>
    public void SendMessage(TestMessageLevel testMessageLevel, string message)
    {
        _messageLogger?.SendMessage(testMessageLevel, message);

        switch (testMessageLevel)
        {
            case TestMessageLevel.Informational:
                _logger.LogInformation(message);
                _outputDevice.DisplayAsync(this, new TextOutputDeviceData(message)).Await();
                break;
            case TestMessageLevel.Warning:
                _logger.LogWarning(message);
                _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(message)).Await();
                break;
            case TestMessageLevel.Error:
                _logger.LogError(message);
                _outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(message)).Await();
                break;
            default:
                throw new NotSupportedException($"Unsupported logging level '{testMessageLevel}'.");
        }
    }

    Task<bool> IExtension.IsEnabledAsync() => _extension.IsEnabledAsync();
}
