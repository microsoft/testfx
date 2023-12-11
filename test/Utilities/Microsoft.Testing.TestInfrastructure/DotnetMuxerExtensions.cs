// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Microsoft.Testing.TestInfrastructure;

public static class DotnetMuxerExtensions
{
    public static async Task<DotnetMuxerResult> ExecDotnetMuxerAsync(
        this string command,
        string nugetGlobalPackagesFolder,
        Dictionary<string, string>? environmentVariables = null,
        bool failIfReturnValueIsNotZero = true)
    {
        environmentVariables ??= new Dictionary<string, string>();
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            // Skip all unwanted environment variables.
            if (KnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(entry.Key!.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentVariables.Add(entry.Key!.ToString()!, entry.Value!.ToString()!);
        }

        environmentVariables["NUGET_PACKAGES"] = nugetGlobalPackagesFolder;

        // Retry in case of:
        // Plugin 'CredentialProvider.Microsoft' failed within 21.143 seconds with exit code
        return await RetryHelper.Retry(
            async () =>
        {
            if (!command.StartsWith("dotnet ", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("'dotnet' command expected");
            }

            using var dotnet = new DotnetMuxer(environmentVariables);
            string args = command[(command.IndexOf(" ", StringComparison.OrdinalIgnoreCase) + 1)..];
            int exitCode = await dotnet.Args(args);

            return exitCode != 0 && failIfReturnValueIsNotZero
                ? throw new InvalidOperationException($"Command '{command}' failed.\n\nStandardOutput:\n{dotnet.StandardOutput}\nStandardError:\n{dotnet.StandardError}")
                : new DotnetMuxerResult(args, exitCode, dotnet.StandardOutput, dotnet.StandardOutputLines, dotnet.StandardError, dotnet.StandardErrorLines);
        }, 3, TimeSpan.FromSeconds(3), exception => exception.ToString().Contains("Plugin 'CredentialProvider.Microsoft' failed"));
    }
}

public sealed class DotnetMuxerResult(string args, int exitCode, string standardOutput, ReadOnlyCollection<string> standardOutputLines,
    string standardError, ReadOnlyCollection<string> standardErrorLines)
{
    public string Args { get; } = args;

    public int ExitCode { get; } = exitCode;

    public string StandardOutput { get; } = standardOutput;

    public ReadOnlyCollection<string> StandardOutputLines { get; } = standardOutputLines;

    public string StandardError { get; } = standardError;

    public ReadOnlyCollection<string> StandardErrorLines { get; } = standardErrorLines;

    public override string ToString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"Args: {Args}");
        stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"ExitCode: {ExitCode}");
        stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"StandardOutput: {StandardOutput}");
        stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"StandardError: {StandardError}");

        return stringBuilder.ToString();
    }
}
