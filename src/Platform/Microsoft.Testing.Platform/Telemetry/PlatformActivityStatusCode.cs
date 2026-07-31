// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Telemetry;

/// <summary>
/// Mirrors <c>System.Diagnostics.ActivityStatusCode</c> without taking a dependency on it.
/// </summary>
internal enum PlatformActivityStatusCode
{
    /// <summary>
    /// The operation completed with an undetermined outcome.
    /// </summary>
    Unset,

    /// <summary>
    /// The operation completed successfully.
    /// </summary>
    Ok,

    /// <summary>
    /// The operation failed.
    /// </summary>
    Error,
}
