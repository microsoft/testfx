// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Extensions;

public readonly struct ValidationResult
{
    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static Task<ValidationResult> ValidTask { get; } = Task.FromResult(Valid());

    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsValid { get; }

    public string? ErrorMessage { get; }

    public static ValidationResult Valid()
        => new(true, null);

    public static ValidationResult Invalid(string errorMessage)
        => new(false, errorMessage);

#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods
    public static Task<ValidationResult> InvalidTask(string errorMessage)
#pragma warning restore VSTHRD200 // Use "Async" suffix for async methods
        => Task.FromResult(Invalid(errorMessage));
}
