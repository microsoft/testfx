// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.ServerMode.Client.Sources.UnitTests;

/// <summary>
/// Tests for how <see cref="MtpServerProcess"/> chooses between a sibling apphost and <c>dotnet &lt;dll&gt;</c>.
/// </summary>
/// <remarks>
/// The scenario these guard is a test payload built on a Windows agent and executed on a Linux machine. Two
/// things go wrong there: a Windows PE <c>App.exe</c> travels next to <c>App.dll</c> and is not a Linux
/// executable, and a zip round trip does not carry POSIX permission bits, so the real extensionless apphost
/// can arrive without its execute bit. Launching either one aborts the run with <c>Permission denied</c>, so
/// the launcher has to reject both and fall back to <c>dotnet &lt;dll&gt;</c>.
/// </remarks>
[TestClass]
public sealed class MtpServerProcessTests
{
    // Any value works: BuildLaunch only formats the port into the argument string, it never binds it.
    private const int Port = 12345;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void BuildLaunchWhenSourceIsExeLaunchesItDirectly()
    {
        using var temp = TempDirectory.Create();
        string exe = temp.CreateFile("App.exe");

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(exe, Port);

        Assert.AreEqual(exe, launch.FileName, "A native executable source must be launched directly.");
        Assert.AreEqual(temp.Path, launch.WorkingDirectory);
        Assert.Contains("--server", launch.Arguments);
        Assert.Contains($"--client-port {Port}", launch.Arguments);
        Assert.Contains("--no-banner", launch.Arguments);
    }

    [TestMethod]
    public void BuildLaunchWhenDllHasNoApphostFallsBackToDotnet()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual("dotnet", launch.FileName, "Without an apphost the assembly must be launched by the muxer.");
        Assert.Contains($"\"{dll}\"", launch.Arguments, "The muxer needs the quoted assembly path as its first argument.");
        Assert.AreEqual(temp.Path, launch.WorkingDirectory);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "A '.exe' apphost is only launchable on Windows.")]
    public void BuildLaunchOnWindowsSelectsSiblingExeApphost()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");
        string exe = temp.CreateFile("App.exe");

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual(exe, launch.FileName, "On Windows the sibling '.exe' apphost is the preferred launch target.");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Asserts that a Windows PE sibling is rejected, which only matters off Windows.")]
    public void BuildLaunchOnUnixIgnoresSiblingWindowsExeAndFallsBackToDotnet()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");

        // The Windows apphost that rode along in the payload. It exists, but it is a Windows PE binary.
        _ = temp.CreateFile("App.exe");

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual("dotnet", launch.FileName, "A Windows '.exe' must never be selected as the apphost on Unix.");
    }

#if NET
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Unix file modes are a Unix concept.")]
    [UnsupportedOSPlatform("windows")]
    public void BuildLaunchOnUnixSelectsExecutableExtensionlessApphost()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");
        string apphost = temp.CreateFile("App");
        MakeExecutable(apphost);

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual(apphost, launch.FileName, "An executable extensionless apphost is the preferred launch target on Unix.");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Unix file modes are a Unix concept.")]
    [UnsupportedOSPlatform("windows")]
    public void BuildLaunchOnUnixIgnoresNonExecutableApphostAndFallsBackToDotnet()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");

        // The apphost survived the trip but the archive dropped its permission bits.
        string apphost = temp.CreateFile("App");
        File.SetUnixFileMode(apphost, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        MtpServerProcess.LaunchCommand launch = MtpServerProcess.BuildLaunch(dll, Port);

        Assert.AreEqual(
            "dotnet",
            launch.FileName,
            "Starting a file with no execute bit throws 'Permission denied', so the launch must fall back to the muxer.");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Unix execute permission classes do not apply on Windows.")]
    [UnsupportedOSPlatform("windows")]
    public async Task StartAsyncOnUnixWhenOnlyNonApplicableExecuteBitIsSetRetriesThroughDotnet()
    {
        using var temp = TempDirectory.Create();
        string dll = temp.CreateFile("App.dll");
        string apphost = temp.CreateFile("App");

        // The owner class applies to this process, so GroupExecute does not grant execution even when the
        // process is also a member of the file's group. The coarse mode-bit preflight accepts this candidate,
        // and Process.Start must recover from the resulting EACCES by retrying through dotnet.
        File.SetUnixFileMode(
            apphost,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupExecute);

        var log = new StringBuilder();
        var options = new MtpServerClientOptions
        {
            ConnectionTimeout = TimeSpan.FromSeconds(5),
            Logger = new DelegateMtpClientLogger((_, message) => log.AppendLine(message)),
        };

        _ = await Assert.ThrowsExactlyAsync<MtpServerConnectionClosedException>(
            () => MtpServerProcess.StartAsync(dll, options, TestContext.CancellationToken));

        Assert.Contains(
            "The sibling apphost could not be executed; retrying through 'dotnet",
            log.ToString(),
            "The EACCES failure should be recovered by retrying the managed assembly through dotnet.");
    }
#endif

    [TestMethod]
    public void IsApphostCandidateReturnsFalseWhenFileMissing()
    {
        using var temp = TempDirectory.Create();

        Assert.IsFalse(MtpServerProcess.IsApphostCandidate(Path.Combine(temp.Path, "Missing")));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Windows has no execute bit, so this asserts the Windows-only branch.")]
    public void IsApphostCandidateOnWindowsReturnsTrueForExistingFile()
    {
        using var temp = TempDirectory.Create();
        string apphost = temp.CreateFile("App.exe");

        Assert.IsTrue(MtpServerProcess.IsApphostCandidate(apphost), "Windows has no execute bit, so existence is the whole check there.");
    }

#if NET
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Unix file modes are a Unix concept.")]
    [UnsupportedOSPlatform("windows")]
    public void IsApphostCandidateOnUnixReturnsFalseForNonExecutableFile()
    {
        using var temp = TempDirectory.Create();
        string apphost = temp.CreateFile("App");
        File.SetUnixFileMode(apphost, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Assert.IsFalse(MtpServerProcess.IsApphostCandidate(apphost));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Unix file modes are a Unix concept.")]
    [UnsupportedOSPlatform("windows")]
    public void IsApphostCandidateOnUnixReturnsTrueForExecutableFile()
    {
        using var temp = TempDirectory.Create();
        string apphost = temp.CreateFile("App");
        MakeExecutable(apphost);

        Assert.IsTrue(MtpServerProcess.IsApphostCandidate(apphost));
    }

    /// <summary>
    /// Grants the owner execute permission only, so the test also proves the check does not require all three
    /// execute bits.
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    private static void MakeExecutable(string path)
        => File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#endif

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path) => Path = path;

        public string Path { get; }

        public static TempDirectory Create()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mtp-apphost-{Guid.NewGuid():N}");
            _ = Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        /// <summary>
        /// Creates an empty file in the directory and returns its full path. The content is irrelevant: the
        /// launcher only looks at the path, its existence, and its permissions.
        /// </summary>
        public string CreateFile(string fileName)
        {
            string fullPath = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(fullPath, string.Empty);
            return fullPath;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup: a temp directory left behind must never fail a test.
            }
        }
    }
}
