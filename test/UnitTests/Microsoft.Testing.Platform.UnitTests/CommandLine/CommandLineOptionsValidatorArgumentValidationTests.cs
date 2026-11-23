// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.UnitTests.Helpers;

namespace Microsoft.Testing.Platform.UnitTests.CommandLine;

[TestClass]
public sealed class CommandLineOptionsValidatorArgumentValidationTests
{
    private static CommandLineOption Option(string name, ArgumentArity? arity = null, bool isBuiltIn = false)
        => new(name, "description", arity ?? ArgumentArity.ZeroOrMore, false, isBuiltIn: isBuiltIn);

    private static Task<ValidationResult> ValidateAsync(
        CommandLineParseResult parseResult,
        IEnumerable<ICommandLineOptionsProvider> system,
        IEnumerable<ICommandLineOptionsProvider> extension,
        IReadOnlyList<JsonCommandLineOptionEntry>? json = null,
        IReadOnlyList<JsonCommandLineOptionEntry>? jsonDefaults = null)
        => CommandLineOptionsValidator.ValidateAsync(parseResult, system, extension, new TestCommandLineOptions([]), json, jsonDefaults);

    [TestMethod]
    public async Task CommandLineOptions_EachFailingOptionIsReported()
    {
        var provider = new CommandLineValidatorTestProvider("Provider", [Option("first"), Option("second")], (option, _) => ValidationResult.Invalid("bad-" + option.Name));
        var parseResult = new CommandLineParseResult(null, [new CommandLineParseOption("first", ["x"]), new CommandLineParseOption("second", ["y"])], []);

        ValidationResult result = await ValidateAsync(parseResult, [provider], []);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("bad-first", result.ErrorMessage);
        Assert.Contains("bad-second", result.ErrorMessage);
    }

    [TestMethod]
    public async Task JsonOptions_EachFailingEntryIsReported()
    {
        var provider = new CommandLineValidatorTestProvider("Provider", [Option("first"), Option("second")], (option, _) => ValidationResult.Invalid("bad-" + option.Name));
        var parseResult = new CommandLineParseResult(null, [], []);
        JsonCommandLineOptionEntry[] json = [new("first", ["x"], isDisabled: false), new("second", ["y"], isDisabled: false)];

        ValidationResult result = await ValidateAsync(parseResult, [provider], [], json);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("bad-first", result.ErrorMessage);
        Assert.Contains("bad-second", result.ErrorMessage);
    }

    [TestMethod]
    public async Task JsonOptions_DisabledEntryIsNotValidated()
    {
        var provider = new CommandLineValidatorTestProvider("Provider", [Option("first")], (_, _) => ValidationResult.Invalid("bad"));
        var parseResult = new CommandLineParseResult(null, [], []);
        JsonCommandLineOptionEntry[] json = [new("first", ["x"], isDisabled: true)];

        ValidationResult result = await ValidateAsync(parseResult, [provider], [], json);

        result.AssertIsValid();
        Assert.IsEmpty(provider.ValidatedArguments);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task JsonOptions_EntryWithArgumentCountWithinArityIsValidatedByProvider(int argumentCount)
    {
        var provider = new CommandLineValidatorTestProvider("Provider", [Option("ranged", new ArgumentArity(1, 3))], (_, _) => ValidationResult.Invalid("bad-ranged"));
        var parseResult = new CommandLineParseResult(null, [], []);
        JsonCommandLineOptionEntry[] json = [new("ranged", [.. Enumerable.Repeat("v", argumentCount)], isDisabled: false)];

        ValidationResult result = await ValidateAsync(parseResult, [provider], [], json);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("bad-ranged", result.ErrorMessage);
        Assert.HasCount(1, provider.ValidatedArguments);
        Assert.HasCount(argumentCount, provider.ValidatedArguments[0].Arguments);
    }

    [TestMethod]
    public async Task ConfigurationValidation_ReportsEveryFailingProviderSeparatedByBlankLine()
    {
        var system = new CommandLineValidatorTestProvider("SystemProvider", [Option("sysopt")], configurationError: "config-error-system");
        var extension = new CommandLineValidatorTestProvider("ExtensionProvider", [Option("extopt")], configurationError: "config-error-extension");
        var parseResult = new CommandLineParseResult(null, [], []);

        ValidationResult result = await ValidateAsync(parseResult, [system], [extension]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("config-error-system", result.ErrorMessage);
        Assert.Contains("config-error-extension", result.ErrorMessage);
        Assert.Contains("SystemProvider", result.ErrorMessage);
        Assert.Contains("ExtensionProvider", result.ErrorMessage);
        Assert.Contains("config-error-system" + Environment.NewLine + Environment.NewLine, result.ErrorMessage.Replace("\r\n", Environment.NewLine));
    }

    [TestMethod]
    public async Task UnknownJsonOptions_EachUnknownEntryIsReported()
    {
        var parseResult = new CommandLineParseResult(null, [], []);
        JsonCommandLineOptionEntry[] json = [new("zzzfirst", [], isDisabled: false), new("yyysecond", [], isDisabled: false)];

        ValidationResult result = await ValidateAsync(parseResult, [], [], json);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("zzzfirst", result.ErrorMessage);
        Assert.Contains("yyysecond", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Arity_EachViolatingOptionIsReported()
    {
        var provider = new CommandLineValidatorTestProvider("Provider", [Option("one", ArgumentArity.ExactlyOne), Option("two", ArgumentArity.ExactlyOne)]);
        var parseResult = new CommandLineParseResult(null, [new CommandLineParseOption("one", []), new CommandLineParseOption("two", [])], []);

        ValidationResult result = await ValidateAsync(parseResult, [provider], []);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("'--one'", result.ErrorMessage);
        Assert.Contains("'--two'", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ReservedPrefix_EachOffendingExtensionOptionIsReported()
    {
        var provider = new CommandLineValidatorTestProvider("Provider", [Option("internal-alpha"), Option("internal-beta")]);

        ValidationResult result = await ValidateAsync(new CommandLineParseResult(null, [], []), [], [provider]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("internal-alpha", result.ErrorMessage);
        Assert.Contains("internal-beta", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ReservedPrefix_BuiltInExtensionOptionIsAllowed()
    {
        var provider = new CommandLineValidatorTestProvider("Provider", [Option("internal-alpha", isBuiltIn: true), Option("internal-beta", isBuiltIn: true)]);

        ValidationResult result = await ValidateAsync(new CommandLineParseResult(null, [], []), [], [provider]);

        result.AssertIsValid();
    }

    [TestMethod]
    public async Task ReservedOptions_EachExtensionOptionShadowingASystemOptionIsReported()
    {
        var system = new CommandLineValidatorTestProvider("SystemProvider", [Option("sysone"), Option("systwo")]);
        var extension = new CommandLineValidatorTestProvider("ExtensionProvider", [Option("sysone"), Option("systwo")]);

        ValidationResult result = await ValidateAsync(new CommandLineParseResult(null, [], []), [system], [extension]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("sysone", result.ErrorMessage);
        Assert.Contains("systwo", result.ErrorMessage);
    }

    [TestMethod]
    public async Task DuplicatedOptions_EachDuplicatedNameIsReported()
    {
        var first = new CommandLineValidatorTestProvider("FirstProvider", [Option("dupone"), Option("duptwo")]);
        var second = new CommandLineValidatorTestProvider("SecondProvider", [Option("dupone"), Option("duptwo")]);
        var third = new CommandLineValidatorTestProvider("ThirdProvider", [Option("dupone"), Option("duptwo")]);

        ValidationResult result = await ValidateAsync(new CommandLineParseResult(null, [], []), [], [first, second, third]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("dupone", result.ErrorMessage);
        Assert.Contains("duptwo", result.ErrorMessage);
        Assert.Contains("ThirdProvider", result.ErrorMessage);
    }

    [TestMethod]
    public async Task JsonDefaults_WhenRunByATool_AcceptOptionsRegisteredByToolProvider()
    {
        var toolProvider = new CommandLineValidatorTestToolProvider("ToolProvider", "tool", [Option("toolopt")]);
        var parseResult = new CommandLineParseResult("tool", [], []);
        JsonCommandLineOptionEntry[] defaults = [new("toolopt", ["v"], isDisabled: false)];

        ValidationResult result = await ValidateAsync(parseResult, [], [toolProvider], jsonDefaults: defaults);

        result.AssertIsValid();
    }

    [TestMethod]
    public async Task JsonDefaults_WhenNotRunByATool_RejectOptionsOnlyRegisteredByToolProvider()
    {
        var toolProvider = new CommandLineValidatorTestToolProvider("ToolProvider", "tool", [Option("toolopt")]);
        var parseResult = new CommandLineParseResult(null, [], []);
        JsonCommandLineOptionEntry[] defaults = [new("toolopt", ["v"], isDisabled: false)];

        ValidationResult result = await ValidateAsync(parseResult, [], [toolProvider], jsonDefaults: defaults);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("toolopt", result.ErrorMessage);
    }
}
