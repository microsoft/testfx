// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents output device data that should be displayed as error.
/// </summary>
/// <remarks>
/// It's up to the output device to decide how to display error messages.
/// The built-in terminal output device will print errors in red foreground.
/// The built-in server mode output device will send the data to Test Explorer with Error severity.
/// </remarks>
public sealed class ErrorMessageOutputDeviceData(string message) : IOutputDeviceData
{
    /// <summary>
    /// Gets the message text represented by this instance.
    /// </summary>
    public string Message { get; } = message;
}
