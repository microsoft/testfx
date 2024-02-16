// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents an environment variable.
/// </summary>
/// <param name="variable">The name of the environment variable.</param>
/// <param name="value">The value of the environment variable.</param>
/// <param name="isSecret">Indicates whether the environment variable is a secret.</param>
/// <param name="isLocked">Indicates whether the environment variable is locked.</param>
public class EnvironmentVariable(string variable, string? value, bool isSecret, bool isLocked)
{
    /// <summary>
    /// Gets the name of the environment variable.
    /// </summary>
    public string Variable { get; } = variable;

    /// <summary>
    /// Gets the value of the environment variable.
    /// </summary>
    public string? Value { get; } = value;

    /// <summary>
    /// Gets a value indicating whether the environment variable is a secret.
    /// </summary>
    public bool IsSecret { get; } = isSecret;

    /// <summary>
    /// Gets a value indicating whether the environment variable is locked.
    /// </summary>
    public bool IsLocked { get; } = isLocked;
}
