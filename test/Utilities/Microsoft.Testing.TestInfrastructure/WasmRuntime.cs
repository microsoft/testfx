// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

/// <summary>
/// Shared helpers for the <c>wasi-wasm</c> acceptance tests (both the raw Microsoft.Testing.Platform
/// tests and the MSTest ones). Centralizes locating <c>wasmtime</c>, publishing an asset for
/// <c>wasi-wasm</c>, staging the ICU data file, and invoking the produced bundle under
/// <c>wasmtime</c> so the two acceptance projects don't duplicate the plumbing.
/// </summary>
public static class WasmRuntime
{
    /// <summary>
    /// The wasm runtime identifier that the mono wasi runtime pack targets.
    /// </summary>
    public const string WasiRid = "wasi-wasm";

    /// <summary>
    /// Message a test should surface (via an inconclusive result) when <c>wasmtime</c> is unavailable.
    /// </summary>
    public const string WasmtimeUnavailableMessage =
        "Skipping wasm execution: 'wasmtime' was not found on PATH (nor via WASMTIME_EXE). " +
        "Install wasmtime and the 'wasm-tools' workload to exercise this test.";

    /// <summary>
    /// Locates the <c>wasmtime</c> executable via the <c>WASMTIME_EXE</c> environment variable or
    /// <c>PATH</c>. Returns <see langword="null"/> when it is not available (e.g. the default
    /// Windows CI matrix), so callers can mark the test inconclusive.
    /// </summary>
    public static string? LocateWasmtime()
    {
        if (Environment.GetEnvironmentVariable("WASMTIME_EXE") is { Length: > 0 } fromEnv && File.Exists(fromEnv))
        {
            return fromEnv;
        }

        string exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "wasmtime.exe" : "wasmtime";
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (path is null)
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (directory.Length == 0)
            {
                continue;
            }

            string candidate = Path.Combine(directory, exeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Publishes the asset at <paramref name="targetAssetPath"/> for <c>wasi-wasm</c>. Failures are
    /// not treated as errors (a missing <c>wasm-tools</c> workload makes <c>dotnet publish</c> fail
    /// rather than skip), so the caller can inspect the exit code — and
    /// <see cref="IsMissingWasmToolsWorkload"/> — to decide between an inconclusive skip and a real
    /// failure.
    /// </summary>
    public static Task<DotnetMuxerResult> PublishForWasiAsync(string targetAssetPath, string targetFramework, CancellationToken cancellationToken)
        => DotnetCli.RunAsync(
            $"publish {targetAssetPath} -f {targetFramework} -r {WasiRid} -c Release",
            warnAsError: false,
            failIfReturnValueIsNotZero: false,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Determines whether a failed <c>dotnet publish -r wasi-wasm</c> result is the expected
    /// "the 'wasm-tools' workload is not installed" diagnostic (NETSDK1147, which names the missing
    /// <c>wasm-tools</c> workload) rather than a genuine build regression. Callers should treat only
    /// this signature as an inconclusive skip and fail the test on any other publish error, so real
    /// regressions are not silently hidden as skips.
    /// </summary>
    public static bool IsMissingWasmToolsWorkload(DotnetMuxerResult publishResult)
    {
        if (publishResult.ExitCode == 0)
        {
            return false;
        }

        string combined = publishResult.StandardOutput + Environment.NewLine + publishResult.StandardError;

        // NETSDK1147 is the "following workloads must be installed" error; 'wasm-tools' is the workload
        // it names when the publish toolchain is missing.
        return combined.Contains("NETSDK1147", StringComparison.Ordinal)
            && combined.Contains("wasm-tools", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the path to the <c>AppBundle</c> directory produced by a <c>wasi-wasm</c> Release publish.
    /// </summary>
    public static string GetAppBundlePath(string targetAssetPath, string targetFramework)
        => Path.Combine(targetAssetPath, "bin", "Release", targetFramework, WasiRid, "AppBundle");

    /// <summary>
    /// Invokes the published bundle under <c>wasmtime</c> the way MTP-on-wasi is documented to run:
    /// <c>wasmtime run -S http --dir . -- dotnet.wasm &lt;AppName&gt; &lt;mtp-args...&gt;</c>. <c>-S http</c>
    /// is required because the runtime imports <c>wasi:http</c>; <c>--dir .</c> grants filesystem access
    /// to the bundle so the platform can read/write its files.
    /// </summary>
    public static async Task<(int ExitCode, string Output, string Error, string Combined)> RunUnderWasmtimeAsync(
        string wasmtime, string appBundle, string appName, CancellationToken cancellationToken)
    {
        var commandLine = new CommandLine();
        int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
            $"\"{wasmtime}\" run -S http --dir . -- dotnet.wasm {appName}",
            workingDirectory: appBundle,
            cancellationToken: cancellationToken);

        string output = commandLine.StandardOutput;
        string error = commandLine.ErrorOutput;
        string combined = $"STDOUT:{Environment.NewLine}{output}{Environment.NewLine}STDERR:{Environment.NewLine}{error}";
        return (exitCode, output, error, combined);
    }

    /// <summary>
    /// The pre-built <c>dotnet.wasm</c> does not embed the ICU data file, so the runtime loads
    /// <c>icudt.dat</c> from disk. Stage it next to the bundle when it is not already present.
    /// </summary>
    public static void StageIcuData(string appBundle)
    {
        if (File.Exists(Path.Combine(appBundle, "icudt.dat")))
        {
            return;
        }

        string runtimePacksRoot = Path.Combine(RootFinder.Find(), ".dotnet", "packs", "Microsoft.NETCore.App.Runtime.Mono.wasi-wasm");
        if (!Directory.Exists(runtimePacksRoot))
        {
            return;
        }

        string? icu = Directory
            .GetFiles(runtimePacksRoot, "icudt.dat", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (icu is not null)
        {
            File.Copy(icu, Path.Combine(appBundle, "icudt.dat"), overwrite: true);
        }
    }
}
