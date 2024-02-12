// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public readonly struct ValidationResult
{
    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Gets a task that represents a valid validation result.
    /// </summary>
    public static Task<ValidationResult> ValidTask { get; } = Task.FromResult(Valid());

    /// <summary>
    /// Gets a value indicating whether the validation result is valid.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsValid { get; }

    /// <summary>
    /// Gets the error message associated with an invalid validation result.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Creates a valid validation result.
    /// </summary>
    /// <returns>A valid validation result.</returns>
    public static ValidationResult Valid()
        => new(true, null);

    /// <summary>
    /// Creates an invalid validation result with the specified error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>An invalid validation result.</returns>
    public static ValidationResult Invalid(string errorMessage)
        => new(false, errorMessage);

    /// <summary>
    /// Creates a task that represents an invalid validation result with the specified error message.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A task that represents an invalid validation result.</returns>
    [SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "This is not meant to be an async await call but rather a task helper")]
    public static Task<ValidationResult> InvalidTask(string errorMessage)
        => Task.FromResult(Invalid(errorMessage));
}
