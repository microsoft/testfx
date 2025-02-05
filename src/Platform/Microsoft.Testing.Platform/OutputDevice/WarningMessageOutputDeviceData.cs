// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents a warning message output data.
/// </summary>
/// <param name="message">The warning message.</param>
public sealed class WarningMessageOutputDeviceData(string message) : IOutputDeviceData
{
    /// <summary>
    /// Gets the warning message.
    /// </summary>
    public string Message { get; } = message;
}
