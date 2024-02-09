// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents an interface for reading environment variables.
/// </summary>
public interface IReadOnlyEnvironmentVariables
{
    /// <summary>
    /// Tries to get the value of the specified environment variable.
    /// </summary>
    /// <param name="variable">The name of the environment variable.</param>
    /// <param name="environmentVariable">When this method returns, contains the value of the environment variable, if it is found; otherwise, null.</param>
    /// <returns>true if the environment variable is found; otherwise, false.</returns>
    bool TryGetVariable(string variable, [NotNullWhen(true)] out OwnedEnvironmentVariable? environmentVariable);
}
