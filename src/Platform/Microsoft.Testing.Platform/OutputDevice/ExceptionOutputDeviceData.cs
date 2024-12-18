// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents output device data that is associated with an exception.
/// </summary>
/// <remarks>
/// It's up to the output device to decide how to display exceptions.
/// The built-in terminal output device will print the exception in red foreground.
/// The built-in server mode output device will send the data to Test Explorer with Error severity.
/// </remarks>
public sealed class ExceptionOutputDeviceData(Exception exception) : IOutputDeviceData
{
    /// <summary>
    /// Gets the exception associated with this instance.
    /// </summary>
    public Exception Exception { get; } = exception;
}
