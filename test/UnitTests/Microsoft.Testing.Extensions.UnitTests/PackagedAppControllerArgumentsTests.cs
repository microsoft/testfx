// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class PackagedAppControllerArgumentsTests
{
    private static readonly string Target = Path.Combine(Path.GetTempPath(), "layout with spaces", "Tests.exe");

    [TestMethod]
    public void Configure_PreservesUserArgumentsAndLaunchProfileTail()
    {
        Dictionary<string, string?> environment = [];
        string[] before = ["--results-directory", @"C:\results with spaces\", "--filter", """Name="a b"|Name=c""", string.Empty];
        string[] after = ["--diagnostic", "--report-trx-filename", "quoted report.trx"];
        string[] result = Configure(
            [PackagedAppControllerArguments.Prefix, Target, "msbuild;packagedapp;trx", .. before,
                "--server", "dotnettestcli", "--dotnet-test-pipe", "sdk-pipe", .. after],
            environment);

        Assert.AreSequenceEqual([.. before, .. after], result);
        Assert.AreEqual(Target, environment[PackagedAppControllerArguments.TargetEnvironmentVariable]);
        Assert.AreEqual("msbuild;packagedapp;trx", environment[PackagedAppControllerArguments.ExtensionsEnvironmentVariable]);
        Assert.AreEqual("sdk-pipe", environment[PackagedAppControllerArguments.NativePipeEnvironmentVariable]);
        Assert.IsTrue(Guid.TryParseExact(environment[PackagedAppControllerArguments.ExecutionIdEnvironmentVariable], "N", out _));
    }

    [TestMethod]
    public void Configure_RetryKeepsTransportAndExecutionIdentity()
    {
        Dictionary<string, string?> environment = new()
        {
            [PackagedAppControllerArguments.ExecutionIdEnvironmentVariable] = "supplied-execution-id",
        };
        string[] result = Configure(
            [PackagedAppControllerArguments.Prefix, Target, "packagedapp;retry",
                "--retry-failed-tests", "2", "--server", "dotnettestcli", "--dotnet-test-pipe", "sdk-pipe"],
            environment);
        string[] retry = Configure(result, environment);

        Assert.AreSame(result, retry);
        Assert.AreEqual("supplied-execution-id", environment[PackagedAppControllerArguments.ExecutionIdEnvironmentVariable]);
        Assert.AreSequenceEqual(
            ["--retry-failed-tests", "2", "--server", "dotnettestcli", "--dotnet-test-pipe", "sdk-pipe"],
            PackagedAppControllerArguments.AddNativeTransport(retry, environment[PackagedAppControllerArguments.NativePipeEnvironmentVariable]));
    }

    [TestMethod]
    public void Configure_DotnetRunClearsStaleTransportAndKeepsTail()
    {
        Dictionary<string, string?> environment = new()
        {
            [PackagedAppControllerArguments.NativePipeEnvironmentVariable] = "stale-pipe",
        };
        string[] result = Configure(
            [PackagedAppControllerArguments.Prefix, Target, "packagedapp", "--report-trx", "--results-directory", "a b"],
            environment);

        Assert.AreSequenceEqual(["--report-trx", "--results-directory", "a b"], result);
        Assert.IsNull(environment[PackagedAppControllerArguments.NativePipeEnvironmentVariable]);
        Assert.AreSame(result, PackagedAppControllerArguments.AddNativeTransport(result, null));
    }

    [TestMethod]
    public void Configure_MarkerAfterUserArgumentsIsNotConsumed()
    {
        Dictionary<string, string?> environment = [];
        string[] arguments = ["--diagnostic", PackagedAppControllerArguments.Prefix, Target, "packagedapp"];
        Assert.AreSame(arguments, Configure(arguments, environment));
        Assert.IsEmpty(environment);
    }

    [TestMethod]
    [DataRow(new[] { "--internal-packagedapp-controller-v2" })]
    [DataRow(new[] { "--internal-packagedapp-controller-v1" })]
    [DataRow(new[] { "--internal-packagedapp-controller-v1", "relative.exe", "packagedapp" })]
    public void Configure_RejectsMalformedBootstrap(string[] arguments)
    {
        Dictionary<string, string?> environment = [];
        _ = Assert.ThrowsExactly<FormatException>(() => Configure(arguments, environment));
        Assert.IsEmpty(environment);
    }

    [TestMethod]
    [DataRow(new[] { "--server" })]
    [DataRow(new[] { "--server", "dotnettestcli", "--dotnet-test-pipe" })]
    [DataRow(new[] { "--server", "dotnettestcli", "--dotnet-test-pipe", "" })]
    [DataRow(new[] { "--server", "dotnettestcli", "--dotnet-test-pipe", "--diagnostic" })]
    [DataRow(new[] { "--dotnet-test-pipe", "pipe" })]
    [DataRow(new[] { "--server=dotnettestcli", "--dotnet-test-pipe", "pipe" })]
    [DataRow(new[] { "--server:dotnettestcli", "--dotnet-test-pipe", "pipe" })]
    [DataRow(new[] { "-server", "dotnettestcli", "-dotnet-test-pipe", "pipe", "--server", "dotnettestcli", "--dotnet-test-pipe", "sdk-pipe" })]
    [DataRow(new[] { "--server", "dotnettestcli", "--dotnet-test-pipe", "sdk-pipe", "-dotnet-test-http-endpoint", "http://localhost" })]
    [DataRow(new[] { "--server", "jsonrpc", "--dotnet-test-pipe", "pipe" })]
    [DataRow(new[] { "--server", "dotnettestcli", "--dotnet-test-transport", "http" })]
    [DataRow(new[] { "--server", "dotnettestcli", "--dotnet-test-pipe", "pipe", "--dotnet-test-pipe", "other" })]
    [DataRow(new[] { "--server", "dotnettestcli", "--dotnet-test-pipe", "pipe", "--server", "dotnettestcli", "--dotnet-test-pipe", "other" })]
    public void Configure_RejectsMalformedOrDuplicateTransport(string[] tail)
    {
        Dictionary<string, string?> environment = [];
        _ = Assert.ThrowsExactly<FormatException>(
            () => Configure([PackagedAppControllerArguments.Prefix, Target, "packagedapp", .. tail], environment));
        Assert.IsEmpty(environment);
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("--list-tests")]
    [DataRow("-help")]
    [DataRow("-?")]
    [DataRow("--?")]
    [DataRow("-list-tests")]
    public void Configure_HelpAndDiscoverySelectActualHostWithOrWithoutNativeTransport(string option)
    {
        Dictionary<string, string?> environment = [];
        (string[] arguments, bool informational) = PackagedAppControllerArguments.Configure(
            [PackagedAppControllerArguments.Prefix, Target, "packagedapp", option,
                "--server", "dotnettestcli", "--dotnet-test-pipe", "pipe"],
            name => environment.GetValueOrDefault(name),
            (name, value) => environment[name] = value);
        Assert.IsTrue(informational);
        Assert.AreSequenceEqual([option], arguments);
        (_, informational) = PackagedAppControllerArguments.Configure(
            [PackagedAppControllerArguments.Prefix, Target, "packagedapp", option],
            name => environment.GetValueOrDefault(name),
            (name, value) => environment[name] = value);
        Assert.IsTrue(informational);
    }

    [TestMethod]
    public void Configure_BootstrapFilePreservesArgumentTailAndRestartTransport()
    {
        Dictionary<string, string?> environment = [];
        string[] bootstrap = [PackagedAppControllerArguments.Prefix, Target, "packagedapp;retry"];
        (string[] arguments, bool informational) = PackagedAppControllerArguments.Configure(
            ["--retry-failed-tests", "2", "--server", "dotnettestcli", "--dotnet-test-pipe", "pipe"],
            name => environment.GetValueOrDefault(name),
            (name, value) => environment[name] = value,
            bootstrap);
        Assert.IsFalse(informational);
        Assert.AreSequenceEqual(["--retry-failed-tests", "2"], arguments);
        Assert.AreEqual(Target, environment[PackagedAppControllerArguments.TargetEnvironmentVariable]);
        Assert.AreEqual("pipe", environment[PackagedAppControllerArguments.NativePipeEnvironmentVariable]);
        (arguments, informational) = PackagedAppControllerArguments.Configure(
            ["--internal-retry-pipename", "retry", "--results-directory", "a b"],
            name => environment.GetValueOrDefault(name),
            (name, value) => environment[name] = value,
            bootstrap);
        Assert.AreSequenceEqual(["--internal-retry-pipename", "retry", "--results-directory", "a b"], arguments);
        Assert.IsFalse(informational);
        Assert.AreEqual("pipe", environment[PackagedAppControllerArguments.NativePipeEnvironmentVariable]);
    }

    [TestMethod]
    [DataRow(new string[0])]
    [DataRow(new[] { "--internal-packagedapp-controller-v2", "target", "packagedapp" })]
    [DataRow(new[] { "--internal-packagedapp-controller-v1", "target" })]
    [DataRow(new[] { "--internal-packagedapp-controller-v1", "target", "packagedapp", "extra" })]
    public void Configure_RejectsMalformedBootstrapFile(string[] bootstrap)
    {
        Dictionary<string, string?> environment = [];
        _ = Assert.ThrowsExactly<FormatException>(() => PackagedAppControllerArguments.Configure(
            [],
            name => environment.GetValueOrDefault(name),
            (name, value) => environment[name] = value,
            bootstrap));
        Assert.IsEmpty(environment);
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("--list-tests")]
    public void Configure_ResponseFileInformationalRequestSelectsActualHost(string option)
    {
        string responseFile = Path.Combine(Path.GetTempPath(), $"packagedapp-{Guid.NewGuid():N}.rsp");
        try
        {
            File.WriteAllText(responseFile, option);
            Dictionary<string, string?> environment = [];
            (string[] arguments, bool informational) = PackagedAppControllerArguments.Configure(
                [$"@{responseFile}"],
                name => environment.GetValueOrDefault(name),
                (name, value) => environment[name] = value,
                [PackagedAppControllerArguments.Prefix, Target, "packagedapp"]);
            Assert.IsTrue(informational);
            Assert.AreSequenceEqual([$"@{responseFile}"], arguments);
        }
        finally
        {
            File.Delete(responseFile);
        }
    }

    [TestMethod]
    public void ValidateNativeApplication_RejectsAppContainerButKeepsMSBuildAndFullTrust()
    {
        string[] native = ["--server", "dotnettestcli", "--dotnet-test-pipe", "pipe"];
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => PackagedAppControllerArguments.ValidateNativeApplication(true, native));
        Assert.Contains("InvokeTestingPlatform", exception.Message);
        PackagedAppControllerArguments.ValidateNativeApplication(false, native);
        PackagedAppControllerArguments.ValidateNativeApplication(true, ["--internal-msbuild-node", "pipe"]);
    }

    private static string[] Configure(string[] arguments, Dictionary<string, string?> environment)
        => PackagedAppControllerArguments.Configure(
            arguments,
            name => environment.GetValueOrDefault(name),
            (name, value) => environment[name] = value).Arguments;
}

#endif
