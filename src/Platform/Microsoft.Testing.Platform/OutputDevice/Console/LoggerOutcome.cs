﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Console;

/// <summary>
/// Outcome of a test.
/// </summary>
internal enum LoggerOutcome
{
    /// <summary>
    /// Error.
    /// </summary>
    Error,

    /// <summary>
    /// Fail.
    /// </summary>
    Fail,

    /// <summary>
    /// Passed.
    /// </summary>
    Passed,

    /// <summary>
    /// Skipped.
    /// </summary>
    Skipped,

    /// <summary>
    ///  Timeout.
    /// </summary>
    Timeout,

    /// <summary>
    /// Cancelled.
    /// </summary>
    Cancelled,
}
