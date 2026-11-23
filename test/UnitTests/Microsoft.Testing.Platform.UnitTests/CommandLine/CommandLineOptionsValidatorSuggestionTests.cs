// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class CommandLineOptionsValidatorSuggestionTests
{
    private static readonly string SuggestionPrefix = PlatformResources.GetResourceString("CommandLineOptionSuggestion")[..PlatformResources.GetResourceString("CommandLineOptionSuggestion").IndexOf("{0}", StringComparison.Ordinal)];

    [TestMethod]
    [DataRow("hlep", "help", "help")] // adjacent transposition in the middle (distance 1 only with transposition)
    [DataRow("ehlp", "help", "help")] // transposition at the very start
    [DataRow("hepl", "help", "help")] // transposition at the very end
    [DataRow("HLEP", "help", "help")] // transposition is case-insensitive
    [DataRow("abcd", "abc", "abc")] // deletion; length difference equals the maximum distance
    [DataRow("abc", "abcd", "abcd")] // insertion at the end
    [DataRow("bcd", "abcd", "abcd")] // insertion at the start
    [DataRow("xabc", "abc", "abc")] // deletion at the start
    [DataRow("abcd", "abxy", null)] // 4 characters allow only one edit
    [DataRow("abcde", "abxye", "abxye")] // 5 characters allow two edits
    [DataRow("abcdefghijkl", "abcdefghixyz", null)] // 12 characters allow only two edits
    [DataRow("abcdefghijkl", "abcdefghijxy", "abcdefghijxy")]
    [DataRow("abcdefghijklm", "abcdefghijxyz", "abcdefghijxyz")] // 13 characters allow three edits
    [DataRow("abcdefghijklm", "abcdefghixyzw", null)]
    [DataRow("abcd", "dcba", null)] // reversal is not a single transposition
    [DataRow("abcd", "abce;abcf", null)] // two candidates at the best distance are ambiguous
    [DataRow("hlep", "help;hlea", null)] // transposition costs exactly one edit so it ties with the substitution
    [DataRow("abcd", "abxy;abce", "abce")] // the closest candidate wins over a farther earlier one
    [DataRow("abcd", "abce;abxy", "abce")] // and over a farther later one
    [DataRow("abcd", "abcdefghijkl", null)] // far-away length is never suggested
    public async Task UnknownOption_SuggestsOnlyUniqueClosestCandidateWithinDistanceLimit(string unknownOption, string registeredOptions, string? expectedSuggestion)
    {
        var provider = new CommandLineValidatorTestProvider("Provider", registeredOptions.Split(';').Select(name => new CommandLineOption(name, "description", ArgumentArity.Zero, false)));
        var parseResult = new CommandLineParseResult("tool", [new CommandLineParseOption(unknownOption, [])], []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, [provider], [], new TestCommandLineOptions([]));

        Assert.IsFalse(result.IsValid);
        if (expectedSuggestion is null)
        {
            Assert.DoesNotContain(SuggestionPrefix, result.ErrorMessage);
        }
        else
        {
            Assert.Contains(string.Format(CultureInfo.InvariantCulture, PlatformResources.GetResourceString("CommandLineOptionSuggestion"), expectedSuggestion), result.ErrorMessage);
        }
    }

    [TestMethod]
    public async Task UnknownOption_CloseToKnownExtensionOption_SuggestsOptionAndMissingPackage()
    {
        var parseResult = new CommandLineParseResult(null, [new CommandLineParseOption("report-trx-filenam", [])], []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, [], [], new TestCommandLineOptions([]));

        Assert.IsFalse(result.IsValid);
        Assert.Contains(string.Format(CultureInfo.InvariantCulture, PlatformResources.GetResourceString("CommandLineOptionSuggestion"), "report-trx-filename"), result.ErrorMessage);
        Assert.Contains(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.GetResourceString("CommandLineOptionRequiresExtension"), "report-trx-filename", "Microsoft.Testing.Extensions.TrxReport"),
            result.ErrorMessage);
    }

    [TestMethod]
    public async Task UnknownOption_ExactKnownExtensionOption_ReportsMissingPackageWithoutAlsoSuggestingTheSameOption()
    {
        var parseResult = new CommandLineParseResult(null, [new CommandLineParseOption("report-trx", [])], []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, [], [], new TestCommandLineOptions([]));

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.GetResourceString("CommandLineOptionRequiresExtension"), "report-trx", "Microsoft.Testing.Extensions.TrxReport"),
            result.ErrorMessage);
        Assert.DoesNotContain(SuggestionPrefix, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("publish-azdo-test-results")]
    [DataRow("report-azdo-annotations")]
    [DataRow("report-azdo-groups")]
    [DataRow("report-azdo-quarantine-file")]
    public async Task UnknownOption_AzureDevOpsReportOptions_AreMappedToTheirPackage(string optionName)
    {
        var parseResult = new CommandLineParseResult(null, [new CommandLineParseOption(optionName, [])], []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(parseResult, [], [], new TestCommandLineOptions([]));

        Assert.IsFalse(result.IsValid);
        Assert.Contains(
            string.Format(CultureInfo.InvariantCulture, PlatformResources.GetResourceString("CommandLineOptionRequiresExtension"), optionName, "Microsoft.Testing.Extensions.AzureDevOpsReport"),
            result.ErrorMessage);
        Assert.DoesNotContain(SuggestionPrefix, result.ErrorMessage);
    }
}
