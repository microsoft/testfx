// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

internal static class DebugAssertionChildProbe
{
    internal const string ModeVariable = "TESTFX_ADAPTER_DEBUG_ASSERT_CHILD";
    internal const string MalformedReflectionMode = "malformed-reflection";
    internal const string NullPdbSourceMode = "null-pdb-source";
    internal const string ClosedOleSchemaMode = "closed-ole-schema";
    internal const string UnsupportedSqlMockMode = "unsupported-sql-mock";
    internal const string ClosedDefaultSchemaMode = "closed-default-schema";
    internal const string MarkerPrefix = "DEBUG_ASSERT_";
    internal const string ReadyMarker = MarkerPrefix + "READY";
    internal const string ReturnedMarker = MarkerPrefix + "RETURNED";
    internal const string FinallyMarker = MarkerPrefix + "FINALLY";

    internal static void ConfigureCrashReporting()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            CrashReporting.DisableInteractiveReporting();
        }
    }

#if DEBUG
    internal static async Task VerifyAsync(string mode, string expectedDiagnostic)
    {
        string module = typeof(DebugAssertionChildProbe).Assembly.Location;
#if NETCOREAPP
        DirectoryInfo? directory = new(Path.GetDirectoryName(module)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the compiled child probe must use its repository's pinned host");
        var startInfo = new ProcessStartInfo(Path.Combine(
            directory!.FullName, ".dotnet", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet"));
        startInfo.ArgumentList.Add(module);
#else
        var startInfo = new ProcessStartInfo(module);
#endif
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.EnvironmentVariables[ModeVariable] = mode;
        using var child = new Process { StartInfo = startInfo };
        child.Start().Should().BeTrue();
        Task<string> standardOutput = child.StandardOutput.ReadToEndAsync();
        Task<string> standardError = child.StandardError.ReadToEndAsync();
        var completion = Task.Run(child.WaitForExit);
        try
        {
            await WaitForCompletionAsync(completion);
            string output = await standardOutput;
            string error = await standardError;
            string[] markers = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimStart('\uFEFF'))
                .Where(line => line.StartsWith(MarkerPrefix, StringComparison.Ordinal))
                .ToArray();
            markers.Should().Equal(
                [ReadyMarker],
                "the guarded production call must run without returning or unwinding managed cleanup");
            child.ExitCode.Should().NotBe(0, "the '{0}' Debug assertion must terminate its owned child, diagnostic: {1}", expectedDiagnostic, error);
#if NETFRAMEWORK
            child.ExitCode.Should().BeOneOf(unchecked((int)0x80131623), 0xDEAD);
#else
            error.Should().Contain(expectedDiagnostic);
#endif
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill();
                await WaitForCompletionAsync(completion);
            }
        }
    }

    private static async Task WaitForCompletionAsync(Task task)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cts.Token));
        completed.Should().BeSameAs(task, "Timed out waiting for the owned assertion child.");
        await task;
    }
#endif

    [SupportedOSPlatform("windows")]
    private static class CrashReporting
    {
        internal static void DisableInteractiveReporting()
        {
            _ = SetErrorMode(0x0001 | 0x0002);
            _ = WerSetFlags(0x0020);
        }

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint SetErrorMode(uint mode);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int WerSetFlags(uint flags);
    }
}
