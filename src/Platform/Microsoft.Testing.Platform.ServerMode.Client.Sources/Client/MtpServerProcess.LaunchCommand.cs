// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.Testing.Platform.ServerMode.Client;

internal sealed partial class MtpServerProcess
{
    internal static LaunchCommand BuildLaunch(string source, int port)
    {
        // Deliberately NOT composed from MtpServerConnector.BuildInProcessServerArguments: this string is the
        // command line an already-shipped API hands to already-shipped test applications, so it is kept
        // byte-identical rather than gaining the in-process path's explicit protocol and host. The server
        // maps its 'localhost' default to IPAddress.Loopback, which is what the listener binds, so the two
        // forms are equivalent on the wire; only the in-process array states them explicitly because an
        // embedded host reads it as the documentation of what the client asked for.
        string serverArgs = $"{ServerArgument} {ClientPortArgument} {port} {NoBannerArgument}";
        string workingDirectory = Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
        string extension = Path.GetExtension(source);

        // A managed .NET assembly must be launched through its apphost (preferred) or `dotnet <dll>`.
        // The apphost is only preferred when it passes the checks available on the consumer's target
        // framework. Process startup remains authoritative and retries through `dotnet <dll>` when Unix
        // rejects the candidate with EACCES.
        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            string apphost = GetAppHostPath(source);
            return IsApphostCandidate(apphost)
                ? new LaunchCommand(apphost, serverArgs, workingDirectory)
                : CreateDotnetLaunch(source, serverArgs, workingDirectory);
        }

        // Otherwise `source` is already a native executable: a Windows `.exe` apphost or an
        // extensionless native apphost on Linux/macOS. Run it directly.
        return new LaunchCommand(source, serverArgs, workingDirectory);
    }

    private static LaunchCommand CreateDotnetLaunch(string source, string serverArgs, string workingDirectory)
        => new("dotnet", $"\"{source}\" {serverArgs}", workingDirectory);

    // A named launch descriptor rather than a value tuple: System.ValueTuple is not in the .NET
    // Framework before 4.7, and this source is compiled into consumers that may target net462 without
    // referencing the System.ValueTuple package. A tiny class keeps the package dependency-free.
    internal sealed class LaunchCommand
    {
        public LaunchCommand(string fileName, string arguments, string workingDirectory)
        {
            FileName = fileName;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
        }

        public string FileName { get; }

        public string Arguments { get; }

        public string WorkingDirectory { get; }
    }

    private static string GetAppHostPath(string managedAssembly)
    {
        string directory = Path.GetDirectoryName(managedAssembly) ?? string.Empty;
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(managedAssembly);
        // The probe is OS-aware rather than always appending ".exe": a test payload built on a Windows
        // agent and executed on a Linux machine (the dotnet/aspnetcore Helix layout) ships a Windows PE
        // `Foo.exe` next to `Foo.dll`. Probing for ".exe" on Linux finds that Windows binary and launching
        // it aborts the run, which is the CI failure fixed in microsoft/vstest#16336. A Unix apphost has
        // no extension, so asking for the right name per OS never selects the foreign one.
        string appHostFileName = IsWindows()
            ? nameWithoutExtension + ".exe"
            : nameWithoutExtension;
        return Path.Combine(directory, appHostFileName);
    }

    /// <summary>
    /// Determines whether <paramref name="apphost"/> is worth attempting on the current operating system.
    /// </summary>
    /// <remarks>
    /// Existence is not sufficient on Unix. Archive formats used to move test payloads between agents (zip in
    /// particular) do not carry the POSIX permission bits, so an extensionless apphost that survives a
    /// Windows-build/Linux-run round trip can arrive without its execute bit. Launching such a file throws
    /// <c>Permission denied</c> instead of degrading, which is the second half of the fix in
    /// microsoft/vstest#16336. On .NET 7+, requiring at least one execute bit cheaply rejects the common case.
    /// This is only a preflight check: the bit can belong to a POSIX identity class that does not apply to the
    /// current process. The process-start path therefore handles EACCES and retries through
    /// <c>dotnet &lt;dll&gt;</c>.
    /// </remarks>
    internal static bool IsApphostCandidate(string apphost)
    {
        if (!File.Exists(apphost))
        {
            return false;
        }

#if NET7_0_OR_GREATER
        // Fenced on the target framework because File.GetUnixFileMode is .NET 7+. Deliberately not the
        // package's modern-.NET compilation symbol: that one records which JSON slice a consumer compiles
        // and is defined only for net8.0+, while NuGet serves the net5.0 slice to net5.0, net6.0 and net7.0
        // consumers alike. Fencing on it would drop a Linux net7.0 consumer back to the existence-only
        // preflight even though it has the API. net462, netstandard2.0, net5.0 and net6.0 consumers genuinely
        // lack the API and keep the existence check above; all target frameworks still recover from EACCES
        // during Process.Start.
        if (!OperatingSystem.IsWindows())
        {
            const UnixFileMode ExecuteBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(apphost) & ExecuteBits) != 0;
        }
#endif

        return true;
    }

    private static bool ShouldRetryApphostThroughDotnet(string source, LaunchCommand launch, Win32Exception exception)
        => Path.GetExtension(source).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && !launch.FileName.Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && !IsWindows()
            && exception.NativeErrorCode == UnixPermissionDeniedErrorCode;

#if NETFRAMEWORK
    // System.Runtime.InteropServices.RuntimeInformation is not available on .NET Framework before
    // 4.7.1, and this source is compiled into consumers that may target net462. .NET Framework only
    // runs on Windows (and, rarely, Unix via Mono), so PlatformID.Win32NT is a reliable Windows check.
    private static bool IsWindows()
        => Environment.OSVersion.Platform == PlatformID.Win32NT;
#else
    private static bool IsWindows()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif

}
