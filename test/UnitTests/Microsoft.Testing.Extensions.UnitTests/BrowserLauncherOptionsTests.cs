// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

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
    public void Parse_ReadsMsBuildLauncherConfiguration()
    {
        string configurationFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(
                configurationFile,
                [
                    "host-command=node",
                    "host-arguments=server.mjs \"path with spaces\"",
                    $"host-working-directory={Path.GetTempPath()}",
                    "url-path=/tests",
                    "browser-executable=",
                    "browser-arguments=--disable-gpu",
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

        string path = ChromiumBrowser.GetPlaywrightNodeExecutablePath("root", osPlatform, architecture);

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
}

#endif
