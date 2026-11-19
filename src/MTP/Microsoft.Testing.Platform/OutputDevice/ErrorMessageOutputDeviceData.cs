// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents an error message output data.
/// </summary>
/// <param name="message">The error message.</param>
public sealed class ErrorMessageOutputDeviceData(string message) : IOutputDeviceData
{
    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; } = message;
}
