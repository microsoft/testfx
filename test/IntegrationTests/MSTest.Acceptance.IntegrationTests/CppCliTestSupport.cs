// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Shared helpers for the C++/CLI acceptance tests (<see cref="CppCliVSTestTests"/> and
/// <see cref="CppCliMtpTests"/>): locating a Visual Studio install with the MSVC toolset and building a
/// child-process environment without the code-coverage profiler variables.
/// </summary>
internal static class CppCliTestSupport
{
    // The code-coverage profiler environment variables the acceptance host injects; inheriting them into a
    // nested .NET Framework test host breaks the run, so they are stripped from the child process environment.
    private static readonly string[] CodeCoverageEnvironmentVariables =
    [
        "MicrosoftInstrumentationEngine_ConfigPath32_VanguardInstrumentationProfiler",
        "MicrosoftInstrumentationEngine_ConfigPath64_VanguardInstrumentationProfiler",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "COR_PROFILER_PATH_32",
        "COR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "CODE_COVERAGE_SESSION_NAME",
        "CODE_COVERAGE_PIPE_PATH",
        "MicrosoftInstrumentationEngine_LogLevel",
        "MicrosoftInstrumentationEngine_DisableCodeSignatureValidation",
        "MicrosoftInstrumentationEngine_FileLogPath",
    ];

    /// <summary>
    /// Returns the installation path of a Visual Studio install that has the MSVC C++ toolset (needed to
    /// compile C++/CLI with classic <c>/clr</c>), or <see langword="null"/> when none is found. Classic
    /// C++/CLI targets .NET Framework and only requires the toolset; the dedicated C++/CLI-support component
    /// is for <c>/clr:netcore</c>.
    /// </summary>
    public static async Task<string?> TryFindVsInstallWithCppToolsetAsync(CancellationToken cancellationToken)
    {
        string vswherePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");
        if (!File.Exists(vswherePath))
        {
            return null;
        }

        using var commandLine = new CommandLine();
        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{vswherePath}\" -latest -prerelease -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath",
            cancellationToken: cancellationToken);
        if (exitCode != 0)
        {
            return null;
        }

        string installPath = commandLine.StandardOutput.Trim();
        return string.IsNullOrEmpty(installPath) ? null : installPath;
    }

    /// <summary>
    /// Builds a copy of the current process environment with the code-coverage profiler variables removed,
    /// for use as a child-process environment (pass with <c>cleanDefaultEnvironmentVariableIfCustomAreProvided: true</c>).
    /// </summary>
    public static Dictionary<string, string?> BuildEnvironmentWithoutCodeCoverage()
    {
        var environmentVariables = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string key = (string)entry.Key;
            if (CodeCoverageEnvironmentVariables.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentVariables[key] = entry.Value?.ToString();
        }

        return environmentVariables;
    }
}
