// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public sealed class ProcessConfiguration
{
    public ProcessConfiguration(string fileName) => FileName = fileName;

    public string FileName { get; }

    public string? Arguments { get; init; }

    public string? WorkingDirectory { get; init; }

    public IDictionary<string, string>? EnvironmentVariables { get; init; }

    public Action<IProcessHandle, string>? OnErrorOutput { get; init; }

    public Action<IProcessHandle, string>? OnStandardOutput { get; init; }

    public Action<IProcessHandle, int>? OnExit { get; init; }
}
