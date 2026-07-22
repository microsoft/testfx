// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

/// <summary>
/// Tests for <see cref="CommandLineArgumentsRedactor"/>, which masks secret option values (currently
/// <c>--dotnet-test-websocket-token</c>) before command-line arguments are written to the <c>--diagnostic</c>
/// log. See <c>docs/mstest-runner-protocol/004-protocol-dotnet-test-pipe.md</c> §15.4.
/// </summary>
[TestClass]
public sealed class CommandLineArgumentsRedactorTests
{
    [TestMethod]
    public void Redact_WithNoArguments_ReturnsEmptyString()
        => Assert.AreEqual(string.Empty, CommandLineArgumentsRedactor.Redact([]));

    [TestMethod]
    public void Redact_WithNoSensitiveOptions_ReturnsArgumentsUnchanged()
    {
        string[] args = ["--server", "dotnettestcli", "--dotnet-test-pipe", "some-pipe-name", "--diagnostic"];

        Assert.AreEqual("--server dotnettestcli --dotnet-test-pipe some-pipe-name --diagnostic", CommandLineArgumentsRedactor.Redact(args));
    }

    [TestMethod]
    public void Redact_WithSpaceSeparatedTokenValue_MasksValueAndKeepsOptionName()
    {
        string[] args = ["--server", "dotnettestcli", "--dotnet-test-websocket-token", "s3cr3t-token-value", "--dotnet-test-websocket-endpoint", "ws://127.0.0.1:5000/dotnettest"];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("s3cr3t-token-value", result);
        Assert.Contains("--dotnet-test-websocket-token ***REDACTED***", result);
        Assert.AreEqual("--server dotnettestcli --dotnet-test-websocket-token ***REDACTED*** --dotnet-test-websocket-endpoint ws://127.0.0.1:5000/dotnettest", result);
    }

    [TestMethod]
    [DataRow('=')]
    [DataRow(':')]
    public void Redact_WithInlineDelimitedTokenValue_MasksValueOnly(char delimiter)
    {
        string[] args = [$"--dotnet-test-websocket-token{delimiter}s3cr3t-token-value"];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("s3cr3t-token-value", result);
        Assert.AreEqual($"--dotnet-test-websocket-token{delimiter}***REDACTED***", result);
    }

    [TestMethod]
    public void Redact_IsCaseInsensitiveForOptionNameButPreservesOriginalCasing()
    {
        string[] args = ["--DOTNET-TEST-WEBSOCKET-TOKEN", "s3cr3t-token-value"];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("s3cr3t-token-value", result);
        Assert.AreEqual("--DOTNET-TEST-WEBSOCKET-TOKEN ***REDACTED***", result);
    }

    [TestMethod]
    public void Redact_WhenTokenOptionIsLastArgumentWithNoValue_DoesNotThrowAndLeavesOptionUnchanged()
    {
        string[] args = ["--server", "dotnettestcli", "--dotnet-test-websocket-token"];

        Assert.AreEqual("--server dotnettestcli --dotnet-test-websocket-token", CommandLineArgumentsRedactor.Redact(args));
    }

    [TestMethod]
    public void Redact_WhenTokenOptionIsRepeated_MasksEachOccurrenceIndependently()
    {
        string[] args = ["--dotnet-test-websocket-token", "first-secret", "--dotnet-test-websocket-token", "second-secret"];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("first-secret", result);
        Assert.DoesNotContain("second-secret", result);
        Assert.AreEqual("--dotnet-test-websocket-token ***REDACTED*** --dotnet-test-websocket-token ***REDACTED***", result);
    }

    [TestMethod]
    public void Redact_WhenSpaceSeparatedTokenLooksLikeOption_MasksToken()
    {
        string[] args = ["--dotnet-test-websocket-token", "-secret", "--diagnostic"];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("-secret", result);
        Assert.AreEqual("--dotnet-test-websocket-token ***REDACTED*** --diagnostic", result);
    }

    [TestMethod]
    [DataRow("--dotnet-test-websocket-endpoint", " ")]
    [DataRow("--dotnet-test-websocket-endpoint=", "=")]
    public void Redact_WebSocketEndpoint_RemovesUserInfoQueryAndFragment(string option, string separator)
    {
        string[] args = separator == " "
            ? [option, "ws://user:password@localhost:123/run?dotnetTestToken=secret#fragment"]
            : [$"{option}ws://user:password@localhost:123/run?dotnetTestToken=secret#fragment"];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("user", result);
        Assert.DoesNotContain("password", result);
        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("fragment", result);
        Assert.AreEqual($"--dotnet-test-websocket-endpoint{separator}ws://localhost:123/run", result);
    }

    [TestMethod]
    public void Redact_WhenTokenValueIsMissingBeforeEndpoint_SanitizesEndpoint()
    {
        string[] args =
        [
            "--dotnet-test-websocket-token",
            "--dotnet-test-websocket-endpoint",
            "ws://localhost/run?key=secret",
        ];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("secret", result);
        Assert.AreEqual("--dotnet-test-websocket-token --dotnet-test-websocket-endpoint ws://localhost/run", result);
    }

    [TestMethod]
    public void Redact_WhenEndpointHasExtraValues_SanitizesEveryValueUntilNextOption()
    {
        string[] args =
        [
            "--dotnet-test-websocket-endpoint",
            "ws://localhost/a?token=one",
            "ws://localhost/b?token=two",
            "--diagnostic",
        ];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("one", result);
        Assert.DoesNotContain("two", result);
        Assert.AreEqual("--dotnet-test-websocket-endpoint ws://localhost/a ws://localhost/b --diagnostic", result);
    }

    [TestMethod]
    public void Redact_WhenExtraStrayValueFollowsTokenOption_RedactsUntilNextOption()
    {
        // Defensive: even if a caller mistakenly supplies more than one token for the (ArgumentArity.ExactlyOne)
        // option, no fragment of the secret should survive in diagnostics.
        string[] args = ["--dotnet-test-websocket-token", "secret-part-one", "secret-part-two", "--diagnostic"];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("secret-part-one", result);
        Assert.DoesNotContain("secret-part-two", result);
        Assert.AreEqual("--dotnet-test-websocket-token ***REDACTED*** ***REDACTED*** --diagnostic", result);
    }

    [TestMethod]
    public void Redact_WithSingleDashOption_IsRecognizedAsAnOption()
    {
        string[] args = ["-dotnet-test-websocket-token", "s3cr3t-token-value"];

        string result = CommandLineArgumentsRedactor.Redact(args);

        Assert.DoesNotContain("s3cr3t-token-value", result);
        Assert.AreEqual("-dotnet-test-websocket-token ***REDACTED***", result);
    }
}
