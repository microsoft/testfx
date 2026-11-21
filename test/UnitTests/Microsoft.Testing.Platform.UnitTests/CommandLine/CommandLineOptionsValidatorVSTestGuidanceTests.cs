// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class CommandLineOptionsValidatorVSTestGuidanceTests
{
    private const string LoggerGuidance = "CommandLineVSTestLoggerGuidance";
    private const string CollectorGuidance = "CommandLineVSTestCollectorGuidance";
    private const string ReplacementSuggestion = "CommandLineVSTestReplacementSuggestion";
    private const string ConsoleReplacement = "CommandLineVSTestConsoleLoggerReplacement";
    private const string XPlatReplacement = "CommandLineVSTestXPlatCoverageReplacement";
    private const string BlameReplacement = "CommandLineVSTestBlameReplacement";

    [TestMethod]
    [DataRow("logger", "trx", ReplacementSuggestion, LoggerGuidance)]
    [DataRow("logger", "trx;LogFileName=x.trx", ReplacementSuggestion, LoggerGuidance)]
    [DataRow("logger", "console;verbosity=minimal", ConsoleReplacement, LoggerGuidance)]
    [DataRow("logger", "console;verbosity=normal", ConsoleReplacement, LoggerGuidance)]
    [DataRow("logger", "console;verbosity=detailed", ConsoleReplacement, LoggerGuidance)]
    [DataRow("logger", "console;verbosity=quiet", LoggerGuidance, ConsoleReplacement)]
    [DataRow("logger", "html", LoggerGuidance, ReplacementSuggestion)]
    [DataRow("collect", "Code Coverage", ReplacementSuggestion, CollectorGuidance)]
    [DataRow("collect", "Code Coverage;Format=cobertura", ReplacementSuggestion, CollectorGuidance)]
    [DataRow("collect", "XPlat Code Coverage", XPlatReplacement, CollectorGuidance)]
    [DataRow("collect", "blame", BlameReplacement, CollectorGuidance)]
    [DataRow("collect", "something else", CollectorGuidance, XPlatReplacement)]
    public async Task VSTestOption_GivesOnlyTheMatchingGuidance(string optionName, string value, string expectedResource, string unexpectedResource)
    {
        var parseResult = new CommandLineParseResult(null, [new CommandLineParseOption(optionName, [value])], []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, [], [], new TestCommandLineOptions([]));

        Assert.IsFalse(result.IsValid);
        Assert.Contains(Prefix(expectedResource), result.ErrorMessage);
        Assert.DoesNotContain(Prefix(unexpectedResource), result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("logger", "trx")]
    [DataRow("logger", "console;verbosity=minimal")]
    [DataRow("logger", "html")]
    [DataRow("collect", "Code Coverage")]
    [DataRow("collect", "XPlat Code Coverage")]
    [DataRow("collect", "blame")]
    [DataRow("collect", "other")]
    public async Task VSTestOption_DoesNotSuggestSimilarlyNamedRegisteredOption(string optionName, string value)
    {
        // "loggers"/"collects" are within edit distance 1 of the unknown option. Once the VSTest guidance applies, no suggestion is made.
        var provider = new CommandLineValidatorTestProvider("Provider", [new CommandLineOption("loggers", "d", ArgumentArity.Zero, false), new CommandLineOption("collects", "d", ArgumentArity.Zero, false)]);
        var parseResult = new CommandLineParseResult(null, [new CommandLineParseOption(optionName, [value])], []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, [provider], [], new TestCommandLineOptions([]));

        Assert.IsFalse(result.IsValid);
        Assert.DoesNotContain("--loggers", result.ErrorMessage);
        Assert.DoesNotContain("--collects", result.ErrorMessage);
    }

    [TestMethod]
    public async Task VSTestOption_WhenRunByATool_IsReportedAsUnknownWithoutVSTestGuidance()
    {
        var parseResult = new CommandLineParseResult("tool", [new CommandLineParseOption("logger", ["trx"])], []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, [], [], new TestCommandLineOptions([]));

        Assert.IsFalse(result.IsValid);
        Assert.DoesNotContain(Prefix(LoggerGuidance), result.ErrorMessage);
        Assert.DoesNotContain(Prefix(ReplacementSuggestion), result.ErrorMessage);
    }

    private static string Prefix(string resourceKey)
    {
        string text = PlatformResources.GetResourceString(resourceKey);
        int index = text.IndexOf('{');
        return index < 0 ? text : text[..index];
    }
}
