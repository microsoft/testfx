// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class CommandLineArgumentsRedactorTests
{
    [TestMethod]
    [DataRow("--dotnet-test-http-token", "secret", "--dotnet-test-http-token ***REDACTED***")]
    [DataRow("--dotnet-test-http-token=secret", null, "--dotnet-test-http-token=***REDACTED***")]
    [DataRow("-dotnet-test-http-token:secret", null, "-dotnet-test-http-token:***REDACTED***")]
    public void Redact_MasksToken(string option, string? value, string expected)
    {
        string[] arguments = value is null ? [option] : [option, value];

        Assert.AreEqual(expected, CommandLineArgumentsRedactor.Redact(arguments));
    }

    [TestMethod]
    public void Redact_RemovesEndpointPathAndPreservesOtherArguments()
    {
        string[] arguments =
        [
            "--server",
            "dotnettestcli",
            "--dotnet-test-http-endpoint",
            "https://gateway.example:8443/private/run-id",
            "--list-tests",
        ];

        Assert.AreEqual(
            "--server dotnettestcli --dotnet-test-http-endpoint https://gateway.example:8443 --list-tests",
            CommandLineArgumentsRedactor.Redact(arguments));
    }

    [TestMethod]
    public void Redact_RepeatedTokenOptionDoesNotExposeValue()
        => Assert.AreEqual(
            "--dotnet-test-http-token --dotnet-test-http-token ***REDACTED***",
            CommandLineArgumentsRedactor.Redact(
                ["--dotnet-test-http-token", "--dotnet-test-http-token", "secret"]));

    [TestMethod]
    public void Redact_OptionShapedTokenValueDoesNotExposeValue()
        => Assert.AreEqual(
            "--dotnet-test-http-token ***REDACTED***",
            CommandLineArgumentsRedactor.Redact(["--dotnet-test-http-token", "-secret"]));

    [TestMethod]
    public void Redact_OptionShapedEndpointValueDoesNotExposeValue()
        => Assert.AreEqual(
            "--dotnet-test-http-endpoint ***REDACTED***",
            CommandLineArgumentsRedactor.Redact(
                ["--dotnet-test-http-endpoint", "-https://gateway.example/private/run-id"]));

    [TestMethod]
    public void Redact_MasksEveryPositionalSensitiveValueUntilNextOption()
        => Assert.AreEqual(
            "--dotnet-test-http-token ***REDACTED*** ***REDACTED*** --server dotnettestcli " +
            "--dotnet-test-http-endpoint https://gateway.example https://other.example --list-tests",
            CommandLineArgumentsRedactor.Redact(
                [
                    "--dotnet-test-http-token", "first-secret", "second-secret",
                    "--server", "dotnettestcli",
                    "--dotnet-test-http-endpoint", "https://gateway.example/private/run", "https://other.example/secret",
                    "--list-tests",
                ]));

    [TestMethod]
    [DataRow("--dotnet-test-http-token=", "--dotnet-test-http-token=***REDACTED***")]
    [DataRow("--dotnet-test-http-endpoint=", "--dotnet-test-http-endpoint=***REDACTED***")]
    public void Redact_MasksEmptyInlineSensitiveValue(string argument, string expected)
        => Assert.AreEqual(expected, CommandLineArgumentsRedactor.Redact([argument]));

    [TestMethod]
    public void Redact_MasksExcessValuesAfterInlineSensitiveValue()
        => Assert.AreEqual(
            "--dotnet-test-http-token=***REDACTED*** ***REDACTED*** --server dotnettestcli " +
            "--dotnet-test-http-endpoint=https://gateway.example https://other.example --list-tests",
            CommandLineArgumentsRedactor.Redact(
                [
                    "--dotnet-test-http-token=first-secret", "second-secret",
                    "--server", "dotnettestcli",
                    "--dotnet-test-http-endpoint=https://gateway.example/private/run", "https://other.example/secret",
                    "--list-tests",
                ]));

    [TestMethod]
    [DataRow("not-a-url")]
    [DataRow("https://user:password@gateway.example/private/run-id")]
    public void Redact_MasksInvalidOrCredentialedEndpoint(string endpoint)
        => Assert.AreEqual(
            "--dotnet-test-http-endpoint ***REDACTED***",
            CommandLineArgumentsRedactor.Redact(["--dotnet-test-http-endpoint", endpoint]));
}
