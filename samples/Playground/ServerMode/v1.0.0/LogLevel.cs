// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

/// <summary>
/// Log level.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Trace.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Debug.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// Information.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Warning.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Error.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Critical.
    /// </summary>
    Critical = 5,

    /// <summary>
    /// None.
    /// </summary>
    None = 6,
}
