// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents an exception output data.
/// </summary>
/// <param name="exception">The exception.</param>
public sealed class ExceptionOutputDeviceData(Exception exception) : IOutputDeviceData
{
    /// <summary>
    /// Gets the exception.
    /// </summary>
    public Exception Exception { get; } = exception;
}
