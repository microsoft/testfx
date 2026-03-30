// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Regression tests for PR #6513 / Issue #6512: MTP v2 doesn't pass command-line arguments after `--`.
/// The `--` token (end-of-options marker) was not supported. Arguments after `--` weren't passed through.
/// These tests verify the parser's behavior when encountering `--` in the argument list.
/// </summary>
[TestClass]
public sealed class ParserEndOfOptionsTokenTests
{
    [TestMethod]
    public void Parse_WhenDoubleHyphenFollowsOption_TreatsItAsArgument()
    {
        // The parser doesn't recognize bare `--` as an option prefix (length must be > 2 for `--` prefix).
        // After an option, `--` becomes an argument to that option.
        CommandLineParseResult result = CommandLineParser.Parse(["--option1", "value1", "--", "arg1"], new SystemEnvironment());

        Assert.IsFalse(result.HasError, $"Expected no errors but got: {string.Join(", ", result.Errors)}");
        Assert.IsTrue(result.TryGetOptionArgumentList("option1", out string[]? args));
        Assert.Contains("value1", args);
    }

    [TestMethod]
    public void Parse_WhenDoubleHyphenIsFirstArgument_ProducesErrors()
    {
        // Bare `--` as the first argument doesn't match option patterns and isn't a tool name
        // (starts with '-'), so it produces an error.
        CommandLineParseResult result = CommandLineParser.Parse(["--", "arg1"], new SystemEnvironment());

        Assert.IsTrue(result.HasError, "Expected errors when `--` is the first argument with no prior option");
    }

    [TestMethod]
    public void Parse_WhenDoubleHyphenAppearsAlone_ProducesError()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--"], new SystemEnvironment());

        Assert.IsTrue(result.HasError, "Expected an error when `--` is the only argument");
    }

    [TestMethod]
    public void Parse_WhenTripleHyphen_ProducesError()
    {
        // `---` is explicitly rejected by the parser (neither `-x` nor `--x` pattern).
        CommandLineParseResult result = CommandLineParser.Parse(["---option1", "a"], new SystemEnvironment());

        Assert.IsTrue(result.HasError, "Expected error for `---` prefix");
        Assert.AreEqual(2, result.Errors.Count);
    }

    [TestMethod]
    public void Parse_WhenMultipleArgumentsAfterOption_AllAreCaptured()
    {
        // Ensures that multiple bare arguments after an option are all captured as that option's arguments.
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--option1", "a", "b", "c"],
            new SystemEnvironment());

        Assert.IsFalse(result.HasError, $"Expected no errors but got: {string.Join(", ", result.Errors)}");
        Assert.IsTrue(result.TryGetOptionArgumentList("option1", out string[]? args));
        Assert.AreEqual(3, args.Length);
        Assert.AreEqual("a", args[0]);
        Assert.AreEqual("b", args[1]);
        Assert.AreEqual("c", args[2]);
    }
}
