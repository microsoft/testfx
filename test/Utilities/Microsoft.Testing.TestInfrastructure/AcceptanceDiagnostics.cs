// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;

namespace Microsoft.Testing.TestInfrastructure;

public static class AcceptanceDiagnostics
{
    public static void WriteProcessSummary(string[] args, int exitCode, int processLaunchAttempts)
        => WriteProcessSummary(args, exitCode, processLaunchAttempts, Console.Error.WriteLine);

    internal static void WriteProcessSummary(string[] args, int exitCode, int processLaunchAttempts, Action<string> writeLine)
    {
        // Failure collection prefers stderr, so statistics must not obscure the failure on stdout.
        if (exitCode != 0)
        {
            return;
        }

#if NETCOREAPP
        int processId = Environment.ProcessId;
        string? targetFramework = AppContext.TargetFrameworkName;
#else
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        int processId = process.Id;
        string? targetFramework = AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName;
#endif
        writeLine(FormatProcessSummary(
            Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown",
            targetFramework ?? "unknown",
            processId,
            processLaunchAttempts,
            args is ["internal-merge-artifacts", ..]));
    }

    internal static string FormatProcessSummary(
        string moduleName,
        string targetFramework,
        int processId,
        int processLaunchAttempts,
        bool isArtifactWorker)
        => $"Process launch attempts: {processLaunchAttempts.ToString(CultureInfo.InvariantCulture)} "
            + $"[module={moduleName}, framework={targetFramework}, pid={processId.ToString(CultureInfo.InvariantCulture)}, "
            + $"role={(isArtifactWorker ? "artifact-worker" : "acceptance-host")}]";
}
