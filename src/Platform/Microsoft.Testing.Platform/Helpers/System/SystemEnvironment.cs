// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.Helpers;

[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "This is the wrapper for Environment type.")]
internal sealed class SystemEnvironment : IEnvironment
{
    public string CommandLine => Environment.CommandLine;

    public string MachineName => Environment.MachineName;

    public string NewLine => Environment.NewLine;

    public string OsVersion => Environment.OSVersion.ToString();

#if NETCOREAPP
    public string? ProcessPath => Environment.ProcessPath;
#endif

    public string[] GetCommandLineArgs() => Environment.GetCommandLineArgs();

    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public IDictionary GetEnvironmentVariables() => Environment.GetEnvironmentVariables();

    public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => Environment.GetFolderPath(folder, option);

    public void FailFast(string? message, Exception? exception) => Environment.FailFast(message, exception);

    public void FailFast(string? message) => Environment.FailFast(message);

    public void SetEnvironmentVariable(string variable, string? value) => Environment.SetEnvironmentVariable(variable, value);

    public void Exit(int exitCode) => Environment.Exit(exitCode);
}
