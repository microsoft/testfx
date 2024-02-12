// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents an owned environment variable.
/// </summary>
/// <remarks>
/// This class extends the <see cref="EnvironmentVariable"/> class and adds an owner property.
/// </remarks>
public sealed class OwnedEnvironmentVariable(IExtension owner, string variable, string? value, bool isSecret, bool isLocked)
    : EnvironmentVariable(variable, value, isSecret, isLocked)
{
    /// <summary>
    /// Gets the owner of the environment variable.
    /// </summary>
    public IExtension Owner { get; } = owner;
}
