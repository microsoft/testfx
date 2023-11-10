// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Platform.TestHostControllers;

internal sealed class EnvironmentVariables : IEnvironmentVariables
{
    private const string StrippedSecretValue = "*****";
    private readonly Dictionary<string, OwnedEnvironmentVariable> _environmentVariables = [];
    private readonly ILogger<EnvironmentVariables> _logger;

    public EnvironmentVariables(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<EnvironmentVariables>();
    }

    public ITestHostEnvironmentVariableProvider? CurrentProvider { get; set; }

    public void SetVariable(EnvironmentVariable environmentVariable)
    {
        if (CurrentProvider is null)
        {
            throw new InvalidOperationException("Cannot set environment variables at this stage.");
        }

        if (_environmentVariables.TryGetValue(environmentVariable.Variable, out OwnedEnvironmentVariable? existingVariable))
        {
            if (existingVariable.IsLocked)
            {
                throw new InvalidOperationException($"{CurrentProvider.Uid} tried to set environment variable '{environmentVariable.Variable}' but it was locked by '{existingVariable.Owner.Uid}' and cannot be updated.");
            }

            // Check if log level is enabled before logging to avoid waiting for task completion
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"{CurrentProvider.Uid} updated environment variable '{environmentVariable.Variable}' from '{existingVariable.Value}' to '{environmentVariable.Value}'.");
            }
        }

        _environmentVariables[environmentVariable.Variable] = new(CurrentProvider, environmentVariable.Variable, environmentVariable.Value,
            environmentVariable.IsSecret, environmentVariable.IsLocked);
    }

    public bool TryGetVariable(string variable, [NotNullWhen(true)] out OwnedEnvironmentVariable? environmentVariable)
    {
        if (!_environmentVariables.TryGetValue(variable, out OwnedEnvironmentVariable? envVar))
        {
            environmentVariable = null;
            return false;
        }

        environmentVariable = envVar.IsSecret
            ? new OwnedEnvironmentVariable(envVar.Owner, envVar.Variable, StrippedSecretValue, envVar.IsSecret, envVar.IsLocked)
            : envVar;
        return true;
    }

    public void RemoveVariable(string variable)
    {
        if (CurrentProvider is null)
        {
            throw new InvalidOperationException("Cannot remove environment variables at this stage.");
        }

        if (!_environmentVariables.TryGetValue(variable, out OwnedEnvironmentVariable? existingVariable))
        {
            return;
        }

        if (existingVariable.IsLocked)
        {
            throw new InvalidOperationException($"{CurrentProvider.Uid} tried to remove environment variable '{variable}' but it was locked by '{existingVariable.Owner.Uid}' and cannot be removed.");
        }

        // Check if log level is enabled before logging to avoid waiting for task completion
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace($"{CurrentProvider.Uid} removed environment variable '{variable}' with value '{existingVariable.Value}'.");
        }
    }

    public IReadOnlyCollection<EnvironmentVariable> GetAll() => _environmentVariables.Values;
}
