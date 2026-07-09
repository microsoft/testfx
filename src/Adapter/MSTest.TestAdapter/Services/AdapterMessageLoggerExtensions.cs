// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Bridges a VSTest <see cref="IMessageLogger"/> to the platform-agnostic <see cref="IAdapterMessageLogger"/>.
/// </summary>
/// <remarks>
/// This is the single translation point between the VSTest object model and the platform services
/// message-logging abstraction. It is expected to move entirely into the adapter layer once the
/// execution pipeline no longer flows VSTest handles through the platform services (see the tracking
/// issue linked in the pull request that removes the VSTest object model from platform services).
/// </remarks>
internal static class AdapterMessageLoggerExtensions
{
    /// <summary>
    /// Wraps a VSTest <see cref="IMessageLogger"/> as an <see cref="IAdapterMessageLogger"/>.
    /// </summary>
    /// <param name="messageLogger">The host message logger to wrap.</param>
    /// <returns>A platform-agnostic logger that forwards to <paramref name="messageLogger"/>.</returns>
    internal static IAdapterMessageLogger ToAdapterMessageLogger(this IMessageLogger messageLogger)
        => new HostMessageLogger(messageLogger ?? throw new ArgumentNullException(nameof(messageLogger)));

    private sealed class HostMessageLogger : IAdapterMessageLogger
    {
        private readonly IMessageLogger _messageLogger;

        public HostMessageLogger(IMessageLogger messageLogger)
            => _messageLogger = messageLogger;

        public void SendMessage(MessageLevel level, string message)
            => _messageLogger.SendMessage(level.ToTestMessageLevel(), message);
    }
}
