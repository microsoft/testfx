// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

using System.Security.AccessControl;
using System.Security.Principal;

using Microsoft.Testing.Platform.Browser;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class BrowserLauncherOptionsTests
{
    [TestMethod]
    public void Parse_ExpandsSdkResponseFileAndPreservesArguments()
    {
        string responseFile = CreateResponseFile(
            """
            --server dotnettestcli
            --dotnet-test-transport http
            --dotnet-test-http-endpoint http://127.0.0.1:1234/dotnettest/run/
            --dotnet-test-http-token abcdef0123456789
            """);

        try
        {
            var options = BrowserLauncherOptions.Parse(
            [
                "--host-command-base64", Encode("dotnet"),
                "--host-arguments-base64", Encode("run --project \"path with spaces.csproj\""),
                "--host-working-directory-base64", Encode(Path.GetTempPath()),
                "--url-path-base64", Encode("/tests"),
                "--browser-executable-base64", Encode(string.Empty),
                "--browser-arguments-base64", Encode("--disable-gpu \"--custom=value with spaces\""),
                "--startup-timeout-seconds", "30",
                "--completion-timeout-seconds", "120",
                "--",
                "--list-tests",
                $"@{responseFile}",
            ]);

            Assert.AreEqual("dotnet", options.HostCommand);
            Assert.AreSequenceEqual(
                new[] { "run", "--project", "path with spaces.csproj" },
                options.HostArguments.ToArray());
            Assert.AreSequenceEqual(
                new[] { "--disable-gpu", "--custom=value with spaces" },
                options.BrowserArguments.ToArray());
            Assert.Contains("--list-tests", options.TestApplicationArguments);
            Assert.Contains("abcdef0123456789", options.TestApplicationArguments);
            Assert.AreEqual("http://127.0.0.1:1234/dotnettest/run/", options.Bootstrap.Endpoint.AbsoluteUri);
            Assert.AreEqual("abcdef0123456789", options.Bootstrap.Token);
        }
        finally
        {
            File.Delete(responseFile);
        }
    }

    [TestMethod]
    public void Parse_RejectsNonLoopbackHttpBootstrap()
    {
        string[] arguments =
        [
            "--host-command-base64", Encode("dotnet"),
            "--host-arguments-base64", Encode("run"),
            "--host-working-directory-base64", Encode(Path.GetTempPath()),
            "--url-path-base64", Encode("/"),
            "--browser-executable-base64", Encode(string.Empty),
            "--browser-arguments-base64", Encode(string.Empty),
            "--startup-timeout-seconds", "30",
            "--completion-timeout-seconds", "120",
            "--",
            "--server", "dotnettestcli",
            "--dotnet-test-transport", "http",
            "--dotnet-test-http-endpoint", "http://example.com/dotnettest/run/",
            "--dotnet-test-http-token", "secret",
        ];

        BrowserLauncherException exception = Assert.ThrowsExactly<BrowserLauncherException>(
            () => BrowserLauncherOptions.Parse(arguments));

        Assert.Contains("valid loopback authenticated HTTP", exception.Message);
    }

    [TestMethod]
    public void Parse_AcceptsLoopbackHttpsIpv6Bootstrap()
    {
        string[] arguments =
        [
            "--host-command-base64", Encode("dotnet"),
            "--host-arguments-base64", Encode("run"),
            "--host-working-directory-base64", Encode(Path.GetTempPath()),
            "--url-path-base64", Encode("/"),
            "--browser-executable-base64", Encode(string.Empty),
            "--browser-arguments-base64", Encode(string.Empty),
            "--startup-timeout-seconds", "30",
            "--completion-timeout-seconds", "120",
            "--",
            "--server", "dotnettestcli",
            "--dotnet-test-transport", "http",
            "--dotnet-test-http-endpoint", "https://[::1]:1234/dotnettest/run/",
            "--dotnet-test-http-token", "secret-token",
        ];

        var options = BrowserLauncherOptions.Parse(arguments);

        Assert.AreEqual("https://[::1]:1234/dotnettest/run/", options.Bootstrap.Endpoint.AbsoluteUri);
    }

    [TestMethod]
    public void ResolveBrowserUri_RejectsProtocolRelativePath()
    {
        BrowserLauncherException exception = Assert.ThrowsExactly<BrowserLauncherException>(
            () => BrowserLauncherOptions.ResolveBrowserUri(
                new Uri("http://127.0.0.1:1234/"),
                "//example.com/tests"));

        Assert.Contains("outside the browser host origin", exception.Message);
    }

    [TestMethod]
    public void ResolveBrowserUri_PreservesLoopbackOrigin()
    {
        Uri uri = BrowserLauncherOptions.ResolveBrowserUri(
            new Uri("http://127.0.0.1:1234/root/"),
            "/tests/index.html");

        Assert.AreEqual("http://127.0.0.1:1234/tests/index.html", uri.AbsoluteUri);
    }

    [TestMethod]
    public void Parse_ReadsMsBuildLauncherConfiguration()
    {
        string configurationFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(
                configurationFile,
                [
                    $"host-command-uri={Uri.EscapeDataString("node")}",
                    $"host-arguments-uri={Uri.EscapeDataString("server.mjs \"path with spaces\"")}",
                    $"host-working-directory-uri={Uri.EscapeDataString(Path.GetTempPath())}",
                    $"url-path-uri={Uri.EscapeDataString("/tests")}",
                    "browser-executable-uri=",
                    $"browser-arguments-uri={Uri.EscapeDataString("--disable-gpu")}",
                    "startup-timeout-seconds=30",
                    "completion-timeout-seconds=120",
                ]);

            var options = BrowserLauncherOptions.Parse(
            [
                "--config", configurationFile,
                "--",
                "--server", "dotnettestcli",
                "--dotnet-test-transport", "http",
                "--dotnet-test-http-endpoint", "http://127.0.0.1:1234/dotnettest/run/",
                "--dotnet-test-http-token", "secret",
            ]);

            Assert.AreEqual("node", options.HostCommand);
            Assert.AreSequenceEqual(["server.mjs", "path with spaces"], options.HostArguments);
            Assert.AreEqual("/tests", options.UrlPath);
            Assert.AreSequenceEqual(["--disable-gpu"], options.BrowserArguments);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.StartupTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(120), options.CompletionTimeout);
        }
        finally
        {
            File.Delete(configurationFile);
        }
    }

    [TestMethod]
    public void Parse_ReadsEncodedMultilineMsBuildLauncherConfiguration()
    {
        const string hostArguments = "server.mjs \"line1\r\nline2\" \"\" --special \"%25;#'\"";
        const string browserArguments = "--note \"line1\r\nline2;%#'\"";
        string configurationFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(
                configurationFile,
                [
                    $"host-command-uri={Uri.EscapeDataString("node")}",
                    $"host-arguments-uri={Uri.EscapeDataString(hostArguments)}",
                    $"host-working-directory-uri={Uri.EscapeDataString(Path.GetTempPath())}",
                    $"url-path-uri={Uri.EscapeDataString("/tests?value=%25;#'")}",
                    "browser-executable-uri=",
                    $"browser-arguments-uri={Uri.EscapeDataString(browserArguments)}",
                    "startup-timeout-seconds=30",
                    "completion-timeout-seconds=120",
                ]);

            var options = BrowserLauncherOptions.Parse(
            [
                "--config", configurationFile,
                "--",
                "--server", "dotnettestcli",
                "--dotnet-test-transport", "http",
                "--dotnet-test-http-endpoint", "http://127.0.0.1:1234/dotnettest/run/",
                "--dotnet-test-http-token", "secret",
            ]);

            Assert.AreSequenceEqual(
                new[] { "server.mjs", "line1\r\nline2", string.Empty, "--special", "%25;#'" },
                options.HostArguments);
            Assert.AreSequenceEqual(
                new[] { "--note", "line1\r\nline2;%#'" },
                options.BrowserArguments);
            Assert.AreEqual("/tests?value=%25;#'", options.UrlPath);
        }
        finally
        {
            File.Delete(configurationFile);
        }
    }

    [TestMethod]
    public void CommandLineTokenizer_RoundTripsQuotedHostArguments()
    {
        string[] arguments = CommandLineTokenizer.Split(
            "run --project \"path with spaces.csproj\" --property \"Name=quoted \\\"value\\\"\"");

        Assert.AreSequenceEqual(
            new[] { "run", "--project", "path with spaces.csproj", "--property", "Name=quoted \"value\"" },
            arguments);
    }

    [TestMethod]
    public void CommandLineTokenizer_PreservesQuotedEmptyArguments()
    {
        string[] arguments = CommandLineTokenizer.Split(
            "host.dll \"\" --name \"quoted value\" \"\" tail");

        Assert.AreSequenceEqual(
            new[] { "host.dll", string.Empty, "--name", "quoted value", string.Empty, "tail" },
            arguments);
    }

    [TestMethod]
    public void CommandLineTokenizer_PreservesDoubledQuotesInsideQuotedArgument()
    {
        string[] arguments = CommandLineTokenizer.Split("\"a\"\"b\" \"\"\"quoted\"\"\"");

        Assert.AreSequenceEqual(new[] { "a\"b", "\"quoted\"" }, arguments);
    }

    [TestMethod]
    public void DiagnosticBuffer_RedactsBootstrapSecretsAndBoundsEntries()
    {
        var diagnostics = new DiagnosticBuffer("secret-token", "/dotnettest/private/");
        for (int i = 0; i < 210; i++)
        {
            diagnostics.Add("browser", $"{i}: secret-token /dotnettest/private/");
        }

        string output = diagnostics.Format();

        Assert.DoesNotContain("secret-token", output);
        Assert.DoesNotContain("/dotnettest/private/", output);
        Assert.DoesNotContain("[browser] 0:", output);
        Assert.Contains("[browser] 209:", output);
        Assert.HasCount(200, output.Split(Environment.NewLine));
    }

    [TestMethod]
    [DataRow("--remote-debugging-port=9222")]
    [DataRow("-remote-debugging-port=9222")]
    [DataRow("/remote-debugging-port=9222")]
    [DataRow("--remote-debugging-address=0.0.0.0")]
    [DataRow("-remote-debugging-address=0.0.0.0")]
    [DataRow("--remote-debugging-pipe")]
    [DataRow("--remote-allow-origins=*")]
    [DataRow("--profile-directory=Default")]
    [DataRow("--user-data-dir=shared")]
    public void Parse_RejectsBrowserArgumentsThatOverrideLauncherSecurity(string browserArgument)
    {
        string responseFile = CreateResponseFile(
            """
            --server dotnettestcli
            --dotnet-test-transport http
            --dotnet-test-http-endpoint http://127.0.0.1:1234/dotnettest/run/
            --dotnet-test-http-token abcdef0123456789
            """);

        try
        {
            Assert.ThrowsExactly<BrowserLauncherException>(() => BrowserLauncherOptions.Parse(
            [
                "--host-command-base64", Encode("dotnet"),
                "--host-arguments-base64", Encode("host.dll"),
                "--host-working-directory-base64", Encode(Path.GetTempPath()),
                "--url-path-base64", Encode("/"),
                "--browser-executable-base64", Encode(string.Empty),
                "--browser-arguments-base64", Encode(browserArgument),
                "--startup-timeout-seconds", "30",
                "--completion-timeout-seconds", "120",
                "--",
                "@" + responseFile,
            ]));
        }
        finally
        {
            File.Delete(responseFile);
        }
    }

    [TestMethod]
    [DataRow("--config-file", null)]
    [DataRow("--config-file=config.json", null)]
    [DataRow("--config-file:config.json", null)]
    [DataRow("--config-file", "config.json")]
    [DataRow("-config-file", "config.json")]
    [DataRow("--diagnostic", null)]
    [DataRow("--diagnostic-output-directory", "diagnostics")]
    [DataRow("--results-directory", "results")]
    [DataRow("--settings", "settings.runsettings")]
    [DataRow("--report-trx", null)]
    [DataRow("--report-trx-filename", "results.trx")]
    [DataRow("--report-html", null)]
    [DataRow("--report-junit", null)]
    [DataRow("--report-ctrf", null)]
    [DataRow("--coverage", null)]
    [DataRow("--coverage-output", "coverage.xml")]
    [DataRow("-results-directory:results", null)]
    public void Parse_RejectsBrowserInapplicableFileOptions(string option, string? value)
    {
        var testApplicationArguments = new List<string> { option };
        if (value is not null)
        {
            testApplicationArguments.Add(value);
        }

        testApplicationArguments.AddRange(CreateBootstrapArguments());

        BrowserLauncherException exception = Assert.ThrowsExactly<BrowserLauncherException>(
            () => BrowserLauncherOptions.Parse(CreateLauncherArguments(testApplicationArguments)));

        Assert.Contains("not supported by the browser launcher preview", exception.Message);
    }

    [TestMethod]
    public void Parse_AllowsFiltersHelpAndListTests()
    {
        string[] testApplicationArguments =
        [
            "--help",
            "--list-tests",
            "--filter",
            "FullyQualifiedName~MyTests",
            .. CreateBootstrapArguments(),
        ];

        var options = BrowserLauncherOptions.Parse(
            CreateLauncherArguments(testApplicationArguments));

        Assert.Contains("--help", options.TestApplicationArguments);
        Assert.Contains("--list-tests", options.TestApplicationArguments);
        Assert.Contains("--filter", options.TestApplicationArguments);
    }

    [TestMethod]
    public void DiagnosticBuffer_DoesNotTreatShortUrlSegmentsAsSecrets()
    {
        var diagnostics = new DiagnosticBuffer("/");

        diagnostics.Add("browser", "https://localhost/path");

        Assert.Contains("https://localhost/path", diagnostics.Format());
    }

    [TestMethod]
    public void BrowserExecutableLocator_PrefersConfiguredExecutable()
    {
        string executable = Path.GetTempFileName();
        try
        {
            Assert.AreEqual(Path.GetFullPath(executable), BrowserExecutableLocator.Locate(executable));
        }
        finally
        {
            File.Delete(executable);
        }
    }

    [TestMethod]
    public void BrowserUnitTests_DoNotReferencePlaywrightRuntime()
    {
        string dependencyContextPath = Path.ChangeExtension(
            typeof(BrowserLauncherOptionsTests).Assembly.Location,
            ".deps.json");
        using FileStream stream = File.OpenRead(dependencyContextPath);
        using var dependencyContext = System.Text.Json.JsonDocument.Parse(stream);

        string[] dependencies =
        [
            .. dependencyContext.RootElement.GetProperty("libraries").EnumerateObject()
                .Select(static library => library.Name),
        ];
        Assert.IsNull(
            dependencies.FirstOrDefault(
                static dependency => dependency.StartsWith("Microsoft.Playwright/", StringComparison.Ordinal)),
            "Browser helper tests must source-link the BCL-only files instead of copying the cross-platform Playwright payload.");
    }

    [TestMethod]
    public void TryParseListeningUri_ParsesLoopbackAndIgnoresOtherOutput()
    {
        Assert.IsNull(HostProcess.TryParseListeningUri("Application started."));
        Assert.AreEqual(
            "http://127.0.0.1:4321/",
            HostProcess.TryParseListeningUri("Now listening on: http://127.0.0.1:4321/")?.AbsoluteUri);
    }

    [TestMethod]
    public void TryParseListeningUri_RejectsNonLoopbackHost()
        => Assert.ThrowsExactly<BrowserLauncherException>(
            () => HostProcess.TryParseListeningUri("Now listening on: http://example.com/"));

    [TestMethod]
    public void TryReadLaunchInfo_PreservesValidationAndAbsenceBehavior()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mtp-browser-launch-info-test-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "launch-info.json");
        try
        {
            Assert.IsNull(HostProcess.TryReadLaunchInfo(path));

            Directory.CreateDirectory(directory);
            WriteLaunchInfo(path, """{"version":1,"url":"https://[::1]:1234/"}""");
            Assert.AreEqual("https://[::1]:1234/", HostProcess.TryReadLaunchInfo(path)?.AbsoluteUri);

            WriteLaunchInfo(path, """{"version":2,"url":"http://127.0.0.1:1234/"}""");
            Assert.ThrowsExactly<BrowserLauncherException>(() => HostProcess.TryReadLaunchInfo(path));

            WriteLaunchInfo(path, """{"version":1,"url":"http://example.com/"}""");
            Assert.ThrowsExactly<BrowserLauncherException>(() => HostProcess.TryReadLaunchInfo(path));

            WriteLaunchInfo(path, "{");
            Assert.ThrowsExactly<BrowserLauncherException>(() => HostProcess.TryReadLaunchInfo(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "Validates the Windows launch-info directory DACL.")]
    [SupportedOSPlatform("windows")]
    public void CreateLaunchInfoDirectory_IsPrivateOnWindows()
    {
        string directory = HostProcess.CreateLaunchInfoDirectory();
        try
        {
            DirectorySecurity security = new DirectoryInfo(directory).GetAccessControl();
            SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User
                ?? throw new AssertFailedException("The current Windows SID is unavailable.");
            Assert.IsTrue(security.AreAccessRulesProtected);
            Assert.AreEqual(
                currentUser,
                security.GetOwner(typeof(SecurityIdentifier)));

            AuthorizationRuleCollection rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                typeof(SecurityIdentifier));
            Assert.IsTrue(
                rules.Cast<FileSystemAccessRule>().All(rule =>
                    rule.IdentityReference.Equals(currentUser)
                    && rule.AccessControlType == AccessControlType.Allow));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Validates Unix launch-info directory permissions.")]
    [UnsupportedOSPlatform("windows")]
    public void CreateLaunchInfoDirectory_IsPrivateOnUnix()
    {
        string directory = HostProcess.CreateLaunchInfoDirectory();
        try
        {
            Assert.AreEqual(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                File.GetUnixFileMode(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows, IgnoreMessage = "Validates Unix file modes and symbolic-link rejection.")]
    [UnsupportedOSPlatform("windows")]
    public void TryReadLaunchInfo_RejectsSymlinkAndPermissiveUnixFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"mtp-browser-launch-info-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "target.json");
        string link = Path.Combine(directory, "link.json");
        try
        {
            WriteLaunchInfo(target, """{"version":1,"url":"http://127.0.0.1:1234/"}""");
            File.SetUnixFileMode(
                target,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            Assert.ThrowsExactly<BrowserLauncherException>(() => HostProcess.TryReadLaunchInfo(target));

            File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            try
            {
                File.CreateSymbolicLink(link, target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Inconclusive($"Symbolic links are unavailable: {ex.Message}");
                return;
            }

            Assert.ThrowsExactly<BrowserLauncherException>(() => HostProcess.TryReadLaunchInfo(link));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("linux", Architecture.X64, "linux-x64")]
    [DataRow("linux", Architecture.Arm64, "linux-arm64")]
    [DataRow("osx", Architecture.X64, "darwin-x64")]
    [DataRow("osx", Architecture.Arm64, "darwin-arm64")]
    public void GetPlaywrightNodeExecutablePath_MapsSupportedPlatforms(
        string operatingSystem,
        Architecture architecture,
        string platformDirectory)
    {
        OSPlatform osPlatform = operatingSystem == "linux" ? OSPlatform.Linux : OSPlatform.OSX;

        string path = PlaywrightNodeExecutable.GetPath("root", osPlatform, architecture);

        Assert.AreEqual(
            Path.Combine("root", ".playwright", "node", platformDirectory, "node"),
            path);
    }

    private static string CreateResponseFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"dotnet-test-http-{Guid.NewGuid():N}.rsp");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return path;
    }

    private static string Encode(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string[] CreateLauncherArguments(IReadOnlyList<string> testApplicationArguments)
        =>
        [
            "--host-command-base64", Encode("dotnet"),
            "--host-arguments-base64", Encode("host.dll"),
            "--host-working-directory-base64", Encode(Path.GetTempPath()),
            "--url-path-base64", Encode("/"),
            "--browser-executable-base64", Encode(string.Empty),
            "--browser-arguments-base64", Encode(string.Empty),
            "--startup-timeout-seconds", "30",
            "--completion-timeout-seconds", "120",
            "--",
            .. testApplicationArguments,
        ];

    private static string[] CreateBootstrapArguments()
        =>
        [
            "--server", "dotnettestcli",
            "--dotnet-test-transport", "http",
            "--dotnet-test-http-endpoint", "http://127.0.0.1:1234/dotnettest/run/",
            "--dotnet-test-http-token", "secret-token",
        ];

    private static void WriteLaunchInfo(string path, string content)
    {
        File.WriteAllText(path, content);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}

#endif
