// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Interface to abstract environment related information.
/// </summary>
internal interface IEnvironment
{
    /// <summary>
    /// Gets the machine name.
    /// </summary>
    string MachineName { get; }

    /// <inheritdoc cref="Environment.SetEnvironmentVariable(string, string)" />
    void SetEnvironmentVariable(string variable, string? value);

    /// <inheritdoc cref="Environment.GetEnvironmentVariable(string)" />
    string? GetEnvironmentVariable(string name);
}

internal sealed class EnvironmentWrapper : IEnvironment
{
    public string MachineName => Environment.MachineName;

    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public void SetEnvironmentVariable(string variable, string? value) => Environment.SetEnvironmentVariable(variable, value);
}
