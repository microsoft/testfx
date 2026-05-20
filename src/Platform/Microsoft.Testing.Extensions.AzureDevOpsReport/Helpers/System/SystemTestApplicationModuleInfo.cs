// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport.Helpers;

/// <summary>
/// Default <see cref="ITestApplicationModuleInfo"/> implementation.
/// Mirrors the fallback semantics of
/// <c>Microsoft.Testing.Platform.Services.CurrentTestApplicationModuleInfo.TryGetAssemblyName()</c>:
/// prefer the entry assembly name; otherwise derive it from the process path
/// (so single-file / native-AOT / custom-host scenarios still report a usable name).
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "Wrapper for Environment / Process APIs.")]
internal sealed class SystemTestApplicationModuleInfo : ITestApplicationModuleInfo
{
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Handles the single-file / native-AOT case via the process path fallback.")]
    public string? TryGetAssemblyName()
    {
        string? executableName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!RoslynString.IsNullOrEmpty(executableName))
        {
            return executableName;
        }

#if NETCOREAPP
        string? processPath = Environment.ProcessPath;
#else
        using var process = Process.GetCurrentProcess();
        string? processPath = process.MainModule?.FileName;
#endif

        if (RoslynString.IsNullOrEmpty(processPath))
        {
            // Fallback for environments where the process path is unavailable (e.g., browser OS).
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            processPath = commandLineArgs.Length > 0 ? commandLineArgs[0] : null;
        }

        return RoslynString.IsNullOrEmpty(processPath)
            ? null
            : Path.GetFileNameWithoutExtension(processPath);
    }
}
