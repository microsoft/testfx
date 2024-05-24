// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

/// <summary>
/// Enables users to log/write traces from unit tests for diagnostics.
/// </summary>
public class Logger
{
    /// <summary>
    /// Handler for LogMessage.
    /// </summary>
    /// <param name="message">Message to log.</param>
    public delegate void LogMessageHandler(string message);

    /// <summary>
    /// Event to listen. Raised when unit test writer writes some message.
    /// Mainly to consume by adapter.
    /// </summary>
    public static event LogMessageHandler? OnLogMessage;

    /// <summary>
    /// API for test writer to call to Log messages.
    /// </summary>
    /// <param name="format">String format with placeholders.</param>
    /// <param name="args">Parameters for placeholders.</param>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public static void LogMessage(string format, params object?[] args)
    {
        if (OnLogMessage == null)
        {
            return;
        }

        if (format == null)
        {
            throw new ArgumentNullException(nameof(format));
        }

        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        string message = args.Length == 0
            ? format
            : string.Format(CultureInfo.InvariantCulture, format, args);

        object?[] parameters = [message];
        // Making sure all event handlers are called in sync on same thread.
        foreach (Delegate invoker in OnLogMessage.GetInvocationList())
        {
            try
            {
                invoker.GetMethodInfo().Invoke(invoker.Target, parameters);
            }
            catch (Exception)
            {
                // Catch and ignore all exceptions thrown by event handlers.
            }
        }
    }
}
