// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestFramework;

/// <summary>
/// Represents the result of creating a test session.
/// </summary>
public sealed class CreateTestSessionResult
{
    /// <summary>
    /// Gets or sets the warning message, if any.
    /// </summary>
    public string? WarningMessage { get; set; }

    /// <summary>
    /// Gets or sets the error message, if any.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test session creation was successful.
    /// </summary>
    public bool IsSuccess { get; set; }
}
