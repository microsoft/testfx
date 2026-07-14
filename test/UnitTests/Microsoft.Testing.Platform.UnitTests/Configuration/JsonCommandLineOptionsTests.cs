// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Validates the JSON-aware enumeration backing the unified read model:
/// <see cref="AggregatedConfiguration.EnumerateJsonCommandLineOptions"/> /
/// <c>JsonConfigurationProvider.EnumerateCommandLineOptions</c>, plus the corresponding
/// validator passes in <see cref="CommandLineOptionsValidator"/> that prevent runtime crashes
/// when a testconfig.json file declares unknown options, supplies the wrong arity, or trips a
/// provider's argument validator.
/// </summary>
[TestClass]
public sealed class JsonCommandLineOptionsTests
{
    // ---------------------------------------------------------------------
    // AggregatedConfiguration.EnumerateJsonCommandLineOptions
    // ---------------------------------------------------------------------
    [TestMethod]
    public void EnumerateJsonCommandLineOptions_NoJsonSource_ReturnsEmpty()
    {
        AggregatedConfiguration configuration = new(
            [],
            new CurrentTestApplicationModuleInfo(new SystemEnvironment(), new SystemProcessHandler()),
            new Mock<IFileSystem>().Object,
            new SystemEnvironment(),
            CommandLineParseResult.Empty);

        IReadOnlyList<JsonCommandLineOptionEntry> entries = configuration.EnumerateJsonCommandLineOptions();
        Assert.IsEmpty(entries);
    }

    // ---------------------------------------------------------------------
    // JsonConfigurationProvider.EnumerateCommandLineOptions schema cases
    // ---------------------------------------------------------------------
    [TestMethod]
    public async Task EnumerateCommandLineOptions_ScalarString_YieldsSingleArgumentEntry()
    {
        IReadOnlyList<JsonCommandLineOptionEntry> entries = await EnumerateAsync(
            "{\"commandLineOptions\": {\"timeout\": \"30s\"}}");

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(entries);
        Assert.AreEqual("timeout", entry.OptionName);
        Assert.IsFalse(entry.IsDisabled);
        Assert.AreSequenceEqual(["30s"], entry.Arguments.ToArray());
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_BooleanTrue_YieldsZeroArityEntry()
    {
        IReadOnlyList<JsonCommandLineOptionEntry> entries = await EnumerateAsync(
            "{\"commandLineOptions\": {\"no-banner\": true}}");

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(entries);
        Assert.AreEqual("no-banner", entry.OptionName);
        Assert.IsFalse(entry.IsDisabled);
        Assert.IsEmpty(entry.Arguments);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_BooleanFalse_YieldsDisabledEntry()
    {
        IReadOnlyList<JsonCommandLineOptionEntry> entries = await EnumerateAsync(
            "{\"commandLineOptions\": {\"no-banner\": false}}");

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(entries);
        Assert.AreEqual("no-banner", entry.OptionName);
        Assert.IsTrue(entry.IsDisabled);
        Assert.IsEmpty(entry.Arguments);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_Array_YieldsAllArguments()
    {
        IReadOnlyList<JsonCommandLineOptionEntry> entries = await EnumerateAsync(
            "{\"commandLineOptions\": {\"filter-uid\": [\"a\", \"b\", \"c\"]}}");

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(entries);
        Assert.AreEqual("filter-uid", entry.OptionName);
        Assert.IsFalse(entry.IsDisabled);
        Assert.AreSequenceEqual(["a", "b", "c"], entry.Arguments.ToArray());
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_EmptyArray_IsSkipped()
    {
        IReadOnlyList<JsonCommandLineOptionEntry> entries = await EnumerateAsync(
            "{\"commandLineOptions\": {\"filter-uid\": []}}");

        Assert.IsEmpty(entries);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_EmptyObject_IsRejected()
    {
        FormatException ex = await Assert.ThrowsExactlyAsync<FormatException>(
            () => EnumerateAsync("{\"commandLineOptions\": {\"foo\": {}}}"));

        Assert.Contains("foo", ex.Message);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_NestedObject_IsRejected()
    {
        FormatException ex = await Assert.ThrowsExactlyAsync<FormatException>(
            () => EnumerateAsync("{\"commandLineOptions\": {\"foo\": {\"nested\": \"value\"}}}"));

        Assert.Contains("foo", ex.Message);
        // The error message should render the entry name relative to the section
        // (e.g. 'foo') rather than the redundant flattened key ('commandLineOptions:foo').
        Assert.DoesNotContain("'commandLineOptions:foo'", ex.Message);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_ArrayOfObjects_IsRejected()
    {
        FormatException ex = await Assert.ThrowsExactlyAsync<FormatException>(
            () => EnumerateAsync("{\"commandLineOptions\": {\"foo\": [{\"k\":\"v\"}]}}"));

        Assert.Contains("foo", ex.Message);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_SectionIsScalar_IsRejected()
    {
        FormatException ex = await Assert.ThrowsExactlyAsync<FormatException>(
            () => EnumerateAsync("{\"commandLineOptions\": \"not-an-object\"}"));

        Assert.Contains("commandLineOptions", ex.Message);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_SectionIsArray_IsRejected()
    {
        FormatException ex = await Assert.ThrowsExactlyAsync<FormatException>(
            () => EnumerateAsync("{\"commandLineOptions\": [\"a\",\"b\"]}"));

        Assert.Contains("commandLineOptions", ex.Message);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_SectionAbsent_ReturnsEmpty()
    {
        IReadOnlyList<JsonCommandLineOptionEntry> entries = await EnumerateAsync("{}");
        Assert.IsEmpty(entries);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_MixedEntries_AllReturned()
    {
        IReadOnlyList<JsonCommandLineOptionEntry> entries = await EnumerateAsync(
            "{\"commandLineOptions\": {\"timeout\": \"30s\", \"no-banner\": true, \"hangdump\": false, \"filter-uid\": [\"a\",\"b\"]}}");

        Assert.HasCount(4, entries);

        JsonCommandLineOptionEntry timeout = entries.Single(e => e.OptionName == "timeout");
        Assert.AreSequenceEqual(["30s"], timeout.Arguments.ToArray());
        Assert.IsFalse(timeout.IsDisabled);

        JsonCommandLineOptionEntry noBanner = entries.Single(e => e.OptionName == "no-banner");
        Assert.IsEmpty(noBanner.Arguments);
        Assert.IsFalse(noBanner.IsDisabled);

        JsonCommandLineOptionEntry hangdump = entries.Single(e => e.OptionName == "hangdump");
        Assert.IsTrue(hangdump.IsDisabled);

        JsonCommandLineOptionEntry filterUid = entries.Single(e => e.OptionName == "filter-uid");
        Assert.AreSequenceEqual(["a", "b"], filterUid.Arguments.ToArray());
    }

    // ---------------------------------------------------------------------
    // CommandLineOptionsValidator JSON-aware passes
    // ---------------------------------------------------------------------
    [TestMethod]
    public async Task Validator_UnknownOptionInJson_FailsWithJsonPrefix()
    {
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("not-a-real-option", [], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("not-a-real-option", result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonArityTooFew_Fails()
    {
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("timeout", [], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("timeout", result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonArityTooMany_Fails()
    {
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("timeout", ["30s", "60s"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("timeout", result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonZeroArityWithArguments_Fails()
    {
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("flag", "desc", ArgumentArity.Zero, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("flag", ["unexpected"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("flag", result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonDisabledEntry_Skipped()
    {
        // "foo": false should NOT trigger arity errors even though arity.Min > 0 — disabled
        // entries convey "the option is not set" and have no arguments to validate.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("timeout", [], isDisabled: true)]);

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonPerArgValidatorFailure_Fails()
    {
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false),
            validateOptionArgumentsAsync: (option, args) =>
                Task.FromResult(args[0] == "bad" ? ValidationResult.Invalid("bad value") : ValidationResult.Valid()));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("timeout", ["bad"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("bad value", result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonArityFailure_DoesNotInvokePerArgValidator()
    {
        // Per-arg validators may index into args (e.g., args[0] for ExactlyOne). If we ran the
        // per-arg pass on a JSON entry that already failed arity (Count != 1), it would crash. The
        // validator must skip per-arg checks for entries that fail arity.
        bool perArgInvoked = false;
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false),
            validateOptionArgumentsAsync: (option, args) =>
            {
                perArgInvoked = true;
                _ = args[0]; // would IndexOutOfRange if invoked with empty args
                return Task.FromResult(ValidationResult.Valid());
            });

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("timeout", [], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.IsFalse(perArgInvoked, "Per-arg validator must not run for entries that already fail arity.");
    }

    [TestMethod]
    public async Task Validator_CliShadowsJsonTypo_TypoStillReported()
    {
        // A JSON typo silently overridden by the CLI is still a typo the user wants to know about.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        CommandLineParseResult cliParseResult = new(
            toolName: null,
            options: [new CommandLineParseOption("timeout", ["30s"])],
            errors: []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            cliParseResult,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("timoeut", ["30s"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("timoeut", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_NullJsonList_BackwardCompatibleNoErrors()
    {
        // Existing call sites that pass no JSON list (default null) must continue to pass.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object);

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    // ---------------------------------------------------------------------
    // Bootstrap-only options must not appear under testconfig.json's
    // "commandLineOptions". They are read off CommandLineParseResult before
    // IConfiguration is built (see TestApplication.cs and
    // JsonConfigurationProvider.cs), so a JSON entry is silently ignored.
    // The validator surfaces this with a clear error instead.
    // ---------------------------------------------------------------------
    [TestMethod]
    [DataRow(PlatformCommandLineProvider.ConfigFileOptionKey)]
    [DataRow(PlatformCommandLineProvider.DiagnosticOptionKey)]
    [DataRow(PlatformCommandLineProvider.DiagnosticOutputDirectoryOptionKey)]
    [DataRow(PlatformCommandLineProvider.DiagnosticOutputFilePrefixOptionKey)]
    [DataRow(PlatformCommandLineProvider.DiagnosticVerbosityOptionKey)]
    [DataRow(PlatformCommandLineProvider.DiagnosticFileLoggerSynchronousWriteOptionKey)]
    public async Task Validator_JsonBootstrapOnlyOption_Fails(string optionName)
    {
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption(optionName, "desc", ArgumentArity.ZeroOrOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry(optionName, ["value"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(optionName, result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
        Assert.Contains("bootstrap", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task Validator_JsonBootstrapOnlyOption_CaseInsensitive_Fails()
    {
        // testconfig.json keys are case-insensitive everywhere else; the bootstrap-only check
        // must match regardless of casing so "Config-File" is flagged the same as "config-file".
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption(PlatformCommandLineProvider.ConfigFileOptionKey, "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("Config-File", ["other.json"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("Config-File", result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonBootstrapOnlyOption_DisabledEntry_StillFails()
    {
        // "config-file": false in testconfig.json is just as misleading as setting it to a value:
        // either way the option is read pre-config and the JSON entry has no effect. Flag both.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption(PlatformCommandLineProvider.ConfigFileOptionKey, "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry(PlatformCommandLineProvider.ConfigFileOptionKey, [], isDisabled: true)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(PlatformCommandLineProvider.ConfigFileOptionKey, result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_CliBootstrapOnlyOption_Allowed()
    {
        // Setting bootstrap-only options on the CLI is the supported path and must remain valid.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption(PlatformCommandLineProvider.ConfigFileOptionKey, "desc", ArgumentArity.ExactlyOne, isHidden: false));

        CommandLineParseResult cliParseResult = new(
            toolName: null,
            options: [new CommandLineParseOption(PlatformCommandLineProvider.ConfigFileOptionKey, ["other.json"])],
            errors: []);

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            cliParseResult,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: null);

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonNonBootstrapOption_Allowed()
    {
        // Sanity check: non-bootstrap-only options (e.g. timeout) continue to be honored from
        // testconfig.json. The bootstrap-only check must not regress general JSON support.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("timeout", ["30s"], isDisabled: false)]);

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonMultipleBootstrapOnlyOptions_AllReported()
    {
        // The validator aggregates bootstrap-only violations so users see every offending entry
        // in one pass rather than fixing them one by one across multiple runs.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption(PlatformCommandLineProvider.ConfigFileOptionKey, "desc", ArgumentArity.ExactlyOne, isHidden: false),
            new CommandLineOption(PlatformCommandLineProvider.DiagnosticOptionKey, "desc", ArgumentArity.Zero, isHidden: false),
            new CommandLineOption(PlatformCommandLineProvider.DiagnosticVerbosityOptionKey, "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions:
            [
                new JsonCommandLineOptionEntry(PlatformCommandLineProvider.ConfigFileOptionKey, ["other.json"], isDisabled: false),
                new JsonCommandLineOptionEntry(PlatformCommandLineProvider.DiagnosticOptionKey, [], isDisabled: false),
                new JsonCommandLineOptionEntry(PlatformCommandLineProvider.DiagnosticVerbosityOptionKey, ["trace"], isDisabled: false),
            ]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains(PlatformCommandLineProvider.ConfigFileOptionKey, result.ErrorMessage);
        Assert.Contains(PlatformCommandLineProvider.DiagnosticOptionKey, result.ErrorMessage);
        Assert.Contains(PlatformCommandLineProvider.DiagnosticVerbosityOptionKey, result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonUnknownOptionCaseInsensitive_Accepted()
    {
        // testconfig.json keys are case-insensitive elsewhere in the platform; "Timeout" must
        // resolve to the registered "timeout" option rather than be reported as unknown.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("Timeout", ["30s"], isDisabled: false)]);

        Assert.IsTrue(result.IsValid, result.ErrorMessage);
    }

    [TestMethod]
    public async Task EnumerateCommandLineOptions_SectionNameCaseInsensitive_Honored()
    {
        // The JSON parser's internal dictionary is OrdinalIgnoreCase, so a top-level key spelled
        // "CommandLineOptions" must still surface under the "commandLineOptions" section.
        IReadOnlyList<JsonCommandLineOptionEntry> entries = await EnumerateAsync(
            """{"CommandLineOptions": {"timeout": "30s"}}""");

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(entries);
        Assert.AreEqual("timeout", entry.OptionName);
        Assert.AreEqual("30s", Assert.ContainsSingle(entry.Arguments));
    }

    [TestMethod]
    public async Task Validator_EmptyOptionNameInJson_FailsWithJsonPrefix()
    {
        // Defensive: a user typo such as { "commandLineOptions": { "": "value" } } surfaces as
        // an unknown-option error rather than crashing inside the validator.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry(string.Empty, ["value"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_DuplicateOptionNamesDifferingByCase_FailsGracefully()
    {
        // Two extension providers register the same option with different casing. Pre-fix the
        // default Ordinal comparison in ValidateOptionsAreNotDuplicated treated them as distinct
        // names and both were silently accepted, even though every downstream lookup
        // (TryGetCommandLineOptionFromProviders, testconfig.json) is case-insensitive. The
        // OrdinalIgnoreCase fix surfaces this as a clean ValidationResult.Invalid up front.
        ICommandLineOptionsProvider providerA = new TestProvider(
            new CommandLineOption("Timeout", "desc-A", ArgumentArity.ExactlyOne, isHidden: false));
        ICommandLineOptionsProvider providerB = new TestProvider(
            new CommandLineOption("timeout", "desc-B", ArgumentArity.ExactlyOne, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            systemCommandLineOptionsProviders: [],
            extensionCommandLineOptionsProviders: [providerA, providerB],
            new Mock<ICommandLineOptions>().Object);

        Assert.IsFalse(result.IsValid);
        // Pin the diagnostic to the duplicate-options pass (rather than any failure) by asserting
        // on the unique wording from that pass plus both provider display names appearing in the
        // error so a future refactor that swaps which pass surfaces the failure will be caught.
        Assert.Contains("declared by multiple", result.ErrorMessage);
        Assert.Contains(providerA.DisplayName, result.ErrorMessage);
        Assert.Contains(providerB.DisplayName, result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_DuplicateOptionNamesAcrossSystemAndSystem_FailsGracefully()
    {
        // Symmetric case: two system providers registering the same name with different casing
        // must also fail through the duplicate-options pass rather than throw later.
        ICommandLineOptionsProvider systemA = new TestProvider(
            new CommandLineOption("Verbose", "desc-A", ArgumentArity.Zero, isHidden: false));
        ICommandLineOptionsProvider systemB = new TestProvider(
            new CommandLineOption("verbose", "desc-B", ArgumentArity.Zero, isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            systemCommandLineOptionsProviders: [systemA, systemB],
            extensionCommandLineOptionsProviders: [],
            new Mock<ICommandLineOptions>().Object);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("declared by multiple", result.ErrorMessage);
        Assert.Contains(systemA.DisplayName, result.ErrorMessage);
        Assert.Contains(systemB.DisplayName, result.ErrorMessage);
    }

    [TestMethod]
    public async Task Validator_JsonEntryWithTooFewArguments_FailsArityCheck()
    {
        // A JSON entry whose argument count is below the option's minimum arity must surface
        // through the arity validation pass with a testconfig.json-prefixed error rather than
        // crashing later inside the provider.
        ICommandLineOptionsProvider provider = new TestProvider(
            new CommandLineOption("pair", "desc", new ArgumentArity(2, 2), isHidden: false));

        ValidationResult result = await CommandLineOptionsValidator.ValidateAsync(
            CommandLineParseResult.Empty,
            [provider],
            [],
            new Mock<ICommandLineOptions>().Object,
            jsonCommandLineOptions: [new JsonCommandLineOptionEntry("pair", ["only-one"], isDisabled: false)]);

        Assert.IsFalse(result.IsValid);
        Assert.Contains("pair", result.ErrorMessage);
        Assert.Contains("testconfig.json", result.ErrorMessage);
    }

    // ---------------------------------------------------------------------
    // NormalizeJsonCommandLineOptionScalars (issue #8830 — scalar/bool ambiguity)
    // ---------------------------------------------------------------------
    [TestMethod]
    public async Task NormalizeScalars_ArgBearingOption_BooleanStringTrue_BecomesArgumentValue()
    {
        // The pre-#8830 behavior treated "timeout": "true" as a presence marker because the
        // scalar value parsed as a bool. After normalization the value must be surfaced as the
        // first argument, both through EnumerateJsonCommandLineOptions and through the unified
        // ICommandLineOptions lookup.
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(
            "{\"commandLineOptions\": {\"timeout\": \"true\"}}");

        aggregated.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false)));

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(aggregated.EnumerateJsonCommandLineOptions());
        Assert.AreEqual("timeout", entry.OptionName);
        Assert.IsFalse(entry.IsDisabled);
        Assert.AreSequenceEqual(["true"], entry.Arguments.ToArray());

        Assert.IsTrue(aggregated.TryGetCommandLineOptionFromProviders("timeout", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["true"], arguments);
    }

    [TestMethod]
    public async Task NormalizeScalars_ArgBearingOption_BooleanStringFalse_BecomesArgumentValue()
    {
        // Symmetric to the "true" case: "false" must not be interpreted as an explicit disable for
        // an arg-bearing option. Without normalization the unified lookup would report isSet=false
        // and the option would silently appear unset to the rest of the platform.
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(
            "{\"commandLineOptions\": {\"timeout\": \"false\"}}");

        aggregated.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false)));

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(aggregated.EnumerateJsonCommandLineOptions());
        Assert.AreEqual("timeout", entry.OptionName);
        Assert.IsFalse(entry.IsDisabled);
        Assert.AreSequenceEqual(["false"], entry.Arguments.ToArray());

        Assert.IsTrue(aggregated.TryGetCommandLineOptionFromProviders("timeout", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["false"], arguments);
    }

    [TestMethod]
    public async Task NormalizeScalars_ArgBearingOption_NonBoolScalar_RemainsAsArgument()
    {
        // Non-bool scalars already worked before #8830 thanks to the second branch in
        // TryGetCommandLineOptionFromProviders; normalization must preserve this behavior while
        // moving the storage into the canonical indexed shape.
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(
            "{\"commandLineOptions\": {\"timeout\": \"30s\"}}");

        aggregated.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false)));

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(aggregated.EnumerateJsonCommandLineOptions());
        Assert.AreEqual("timeout", entry.OptionName);
        Assert.AreSequenceEqual(["30s"], entry.Arguments.ToArray());

        Assert.IsTrue(aggregated.TryGetCommandLineOptionFromProviders("timeout", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["30s"], arguments);
    }

    [TestMethod]
    public async Task NormalizeScalars_ZeroArityOption_BooleanScalar_RemainsPresenceMarker()
    {
        // Zero-arity flags must keep their bool→presence-marker semantics. Normalization is a
        // no-op for them because their arity (Min == 0) signals that the bare key is meaningful
        // as-is.
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(
            "{\"commandLineOptions\": {\"no-banner\": true}}");

        aggregated.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("no-banner", "desc", ArgumentArity.Zero, isHidden: false)));

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(aggregated.EnumerateJsonCommandLineOptions());
        Assert.AreEqual("no-banner", entry.OptionName);
        Assert.IsFalse(entry.IsDisabled);
        Assert.IsEmpty(entry.Arguments);

        Assert.IsTrue(aggregated.TryGetCommandLineOptionFromProviders("no-banner", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.IsEmpty(arguments);
    }

    [TestMethod]
    public async Task NormalizeScalars_OptionalArgOption_BooleanScalar_LeftUntouched()
    {
        // Min=0, Max>=1 (optional-arg) is genuinely ambiguous between a presence marker and a
        // scalar argument. We preserve the historical presence-marker interpretation; users that
        // really mean to pass "true"/"false" as a value must use the explicit array form.
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(
            "{\"commandLineOptions\": {\"opt-arg\": \"true\"}}");

        aggregated.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("opt-arg", "desc", ArgumentArity.ZeroOrOne, isHidden: false)));

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(aggregated.EnumerateJsonCommandLineOptions());
        Assert.AreEqual("opt-arg", entry.OptionName);
        Assert.IsFalse(entry.IsDisabled);
        Assert.IsEmpty(entry.Arguments);
    }

    [TestMethod]
    public async Task NormalizeScalars_UnknownOption_LeftUntouched()
    {
        // Unknown options must not be rewritten — the validator's unknown-option pass needs to see
        // the original entry to emit a clear error referencing testconfig.json.
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(
            "{\"commandLineOptions\": {\"unknown\": \"true\"}}");

        aggregated.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("other", "desc", ArgumentArity.ExactlyOne, isHidden: false)));

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(aggregated.EnumerateJsonCommandLineOptions());
        Assert.AreEqual("unknown", entry.OptionName);
        Assert.IsFalse(entry.IsDisabled);
        Assert.IsEmpty(entry.Arguments);
    }

    [TestMethod]
    public async Task NormalizeScalars_IndexedEntries_LeftUntouched()
    {
        // Array form is already in the canonical shape and must not be touched, including when an
        // option that takes multiple args is involved.
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(
            "{\"commandLineOptions\": {\"filter-uid\": [\"a\",\"b\"]}}");

        aggregated.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("filter-uid", "desc", ArgumentArity.OneOrMore, isHidden: false)));

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(aggregated.EnumerateJsonCommandLineOptions());
        Assert.AreSequenceEqual(["a", "b"], entry.Arguments.ToArray());

        Assert.IsTrue(aggregated.TryGetCommandLineOptionFromProviders("filter-uid", out bool isSet, out string[] arguments));
        Assert.IsTrue(isSet);
        Assert.AreSequenceEqual(["a", "b"], arguments);
    }

    [TestMethod]
    public async Task NormalizeScalars_CaseInsensitive_RewritesByOptionName()
    {
        // testconfig.json keys are case-insensitive throughout the platform. A JSON entry "Timeout"
        // must be matched against the registry's "timeout" and normalized accordingly.
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(
            "{\"commandLineOptions\": {\"Timeout\": \"true\"}}");

        aggregated.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false)));

        JsonCommandLineOptionEntry entry = Assert.ContainsSingle(aggregated.EnumerateJsonCommandLineOptions());
        Assert.AreSequenceEqual(["true"], entry.Arguments.ToArray());
    }

    [TestMethod]
    public async Task NormalizeScalars_NoJsonSource_IsNoOp()
    {
        // The platform may build an AggregatedConfiguration without a JSON source registered;
        // normalization must degrade to a no-op rather than throw.
        AggregatedConfiguration configuration = new(
            [],
            new CurrentTestApplicationModuleInfo(new SystemEnvironment(), new SystemProcessHandler()),
            new Mock<IFileSystem>().Object,
            new SystemEnvironment(),
            CommandLineParseResult.Empty);

        configuration.NormalizeJsonCommandLineOptionScalars(OptionsByName(
            new CommandLineOption("timeout", "desc", ArgumentArity.ExactlyOne, isHidden: false)));

        Assert.IsEmpty(configuration.EnumerateJsonCommandLineOptions());

        // The await keeps the test method asynchronous to match the surrounding suite style.
        await Task.CompletedTask;
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------
    private static async Task<IReadOnlyList<JsonCommandLineOptionEntry>> EnumerateAsync(string json)
    {
        AggregatedConfiguration aggregated = await BuildAggregatedAsync(json);
        return aggregated.EnumerateJsonCommandLineOptions();
    }

    private static async Task<AggregatedConfiguration> BuildAggregatedAsync(string json)
    {
        Mock<IFileSystem> fileSystem = new();
        fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(true);
        fileSystem.Setup(x => x.NewFileStream(It.IsAny<string>(), FileMode.Open, FileAccess.Read))
            .Returns(() => new ConfigurationManagerTests.MemoryFileStream(Encoding.UTF8.GetBytes(json)));
        CurrentTestApplicationModuleInfo testApplicationModuleInfo = new(new SystemEnvironment(), new SystemProcessHandler());
        ConfigurationManager configurationManager = new(fileSystem.Object, testApplicationModuleInfo, new SystemEnvironment());
        configurationManager.AddConfigurationSource(() => new JsonConfigurationSource(testApplicationModuleInfo, fileSystem.Object, null));

        IConfiguration configuration = await configurationManager.BuildAsync(null, CommandLineParseResult.Empty);
        return (AggregatedConfiguration)configuration;
    }

    private static IReadOnlyDictionary<string, CommandLineOption> OptionsByName(params CommandLineOption[] options)
    {
        var dict = new Dictionary<string, CommandLineOption>(StringComparer.OrdinalIgnoreCase);
        foreach (CommandLineOption option in options)
        {
            dict[option.Name] = option;
        }

        return dict;
    }

    private sealed class TestProvider : ICommandLineOptionsProvider
    {
        private readonly CommandLineOption[] _options;
        private readonly Func<CommandLineOption, string[], Task<ValidationResult>>? _validateOptionArgumentsAsync;

        public TestProvider(
            params CommandLineOption[] options)
            : this(validateOptionArgumentsAsync: null, options)
        {
        }

        public TestProvider(
            CommandLineOption option,
            Func<CommandLineOption, string[], Task<ValidationResult>> validateOptionArgumentsAsync)
            : this(validateOptionArgumentsAsync, option)
        {
        }

        private TestProvider(
            Func<CommandLineOption, string[], Task<ValidationResult>>? validateOptionArgumentsAsync,
            params CommandLineOption[] options)
        {
            _options = options;
            _validateOptionArgumentsAsync = validateOptionArgumentsAsync;
        }

        public string Uid => nameof(TestProvider);

        public string Version => "1.0.0";

        public string DisplayName => nameof(TestProvider);

        public string Description => "Test provider";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() => _options;

        public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
            => _validateOptionArgumentsAsync?.Invoke(commandOption, arguments) ?? Task.FromResult(ValidationResult.Valid());

        public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
            => Task.FromResult(ValidationResult.Valid());
    }
}
