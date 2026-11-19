// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

/// <summary>
/// Represents an interface for providing environment variables to the test host.
/// </summary>
public interface ITestHostEnvironmentVariableProvider : ITestHostControllersExtension
{
    /// <summary>
    /// Updates the environment variables for the test host asynchronously.
    /// </summary>
    /// <param name="environmentVariables">The environment variables to update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(IEnvironmentVariables environmentVariables);

    /// <summary>
    /// Validates the test host environment variables asynchronously.
    /// </summary>
    /// <param name="environmentVariables">The environment variables to validate.</param>
    /// <returns>A task representing the asynchronous operation and containing the validation result.</returns>
    Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables);
}
