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
    public string Variable { get; } = variable;

    public string? Value { get; } = value;

    public bool IsSecret { get; } = isSecret;

    public bool IsLocked { get; } = isLocked;
}
