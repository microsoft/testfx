// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestFramework;

/// <summary>
/// Represents the result of closing a test session.
/// </summary>
public sealed class CloseTestSessionResult
{
    /// <summary>
    /// Gets or sets the warning message associated with the test session closing.
    /// </summary>
    public string? WarningMessage { get; set; }

    /// <summary>
    /// Gets or sets the error message associated with the test session closing.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test session closing was successful.
    /// </summary>
    public bool IsSuccess { get; set; }
}
