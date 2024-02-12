// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents an interface for managing environment variables.
/// </summary>
public interface IEnvironmentVariables : IReadOnlyEnvironmentVariables
{
    /// <summary>
    /// Sets the value of the specified environment variable.
    /// </summary>
    /// <param name="environmentVariable">The environment variable to set.</param>
    void SetVariable(EnvironmentVariable environmentVariable);

    /// <summary>
    /// Removes the specified environment variable.
    /// </summary>
    /// <param name="variable">The name of the environment variable to remove.</param>
    void RemoveVariable(string variable);
}
