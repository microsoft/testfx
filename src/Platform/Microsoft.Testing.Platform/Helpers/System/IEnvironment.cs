// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Microsoft.Testing.Platform.Helpers;

internal interface IEnvironment
{
    string CommandLine { get; }

    string MachineName { get; }

    string NewLine { get; }

    string OsVersion { get; }

#if NETCOREAPP
    string? ProcessPath { get; }
#endif

    string[] GetCommandLineArgs();

    string? GetEnvironmentVariable(string name);

    IDictionary GetEnvironmentVariables();

    string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option);

    void FailFast(string? message, Exception? exception);

    void FailFast(string? message);

    void SetEnvironmentVariable(string variable, string? value);

    void Exit(int exitCode);
}
