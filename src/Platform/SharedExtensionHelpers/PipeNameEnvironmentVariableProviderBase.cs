// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions;

internal abstract class PipeNameEnvironmentVariableProviderBase(string pipeName, string environmentVariableName) : ITestHostEnvironmentVariableProvider
{
    public string Version => ExtensionVersion.DefaultSemVer;

    public abstract string Uid { get; }

    public abstract string DisplayName { get; }

    public abstract string Description { get; }

    /// <summary>
    /// Gets a value indicating whether the provider validates that the environment variable value matches <see cref="PipeName"/>.
    /// Override and return <see langword="false"/> when only variable presence should be validated.
    /// </summary>
    protected virtual bool ShouldValidatePipeNameValue => true;

    protected string PipeName { get; } = pipeName;

    protected string EnvironmentVariableName { get; } = environmentVariableName;

    public abstract Task<bool> IsEnabledAsync();

    public Task UpdateAsync(IEnvironmentVariables environmentVariables)
    {
        environmentVariables.SetVariable(new EnvironmentVariable(EnvironmentVariableName, PipeName, isSecret: false, isLocked: true));
        return Task.CompletedTask;
    }

    public Task<ValidationResult> ValidateTestHostEnvironmentVariablesAsync(IReadOnlyEnvironmentVariables environmentVariables)
    {
#pragma warning disable IDE0046 // Convert to conditional expression
        if (!environmentVariables.TryGetVariable(EnvironmentVariableName, out OwnedEnvironmentVariable? envVar))
        {
            return Task.FromResult(ValidationResult.Invalid(GetMissingEnvironmentVariableErrorMessage(EnvironmentVariableName)));
        }

        if (ShouldValidatePipeNameValue && envVar.Value != PipeName)
        {
            return Task.FromResult(ValidationResult.Invalid(GetInvalidEnvironmentVariableValueErrorMessage(EnvironmentVariableName, envVar.Value, PipeName)));
        }
#pragma warning restore IDE0046 // Convert to conditional expression

        return ValidationResult.ValidTask;
    }

    protected abstract string GetMissingEnvironmentVariableErrorMessage(string environmentVariableName);

    protected abstract string GetInvalidEnvironmentVariableValueErrorMessage(string environmentVariableName, string? actualValue, string expectedValue);
}
