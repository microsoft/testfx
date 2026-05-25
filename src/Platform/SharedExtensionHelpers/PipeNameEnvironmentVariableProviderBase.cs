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
        => !environmentVariables.TryGetVariable(EnvironmentVariableName, out OwnedEnvironmentVariable? envVar)
            ? Task.FromResult(ValidationResult.Invalid(GetMissingEnvironmentVariableErrorMessage(EnvironmentVariableName)))
            : ShouldValidatePipeNameValue && envVar.Value != PipeName
                ? Task.FromResult(ValidationResult.Invalid(GetInvalidEnvironmentVariableValueErrorMessage(EnvironmentVariableName, envVar.Value, PipeName)))
                : ValidationResult.ValidTask;

    protected abstract string GetMissingEnvironmentVariableErrorMessage(string environmentVariableName);

    protected virtual string GetInvalidEnvironmentVariableValueErrorMessage(string environmentVariableName, string? actualValue, string expectedValue)
        => string.Format(CultureInfo.InvariantCulture, "Environment variable '{0}' has invalid value '{1}', expected '{2}'.", environmentVariableName, actualValue, expectedValue);
}
