// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class PlatformCommandLineProvider : ICommandLineOptionsProvider
{
    public const string HelpOptionKey = "help";
    public const string InfoOptionKey = "info";
    public const string DiagnosticOptionKey = "diagnostic";
    public const string DiagnosticOutputFilePrefixOptionKey = "diagnostic-output-fileprefix";
    public const string DiagnosticOutputDirectoryOptionKey = "diagnostic-output-directory";
    public const string DiagnosticVerbosityOptionKey = "diagnostic-verbosity";
    public const string DiagnosticFileLoggerSynchronousWriteOptionKey = "diagnostic-filelogger-synchronouswrite";
    public const string ServerOptionKey = "server";
    public const string PortOptionKey = "port";
    public const string ClientPortOptionKey = "client-port";
    public const string ClientHostOptionKey = "client-host";
    public const string VSTestAdapterModeOptionKey = "internal-vstest-adapter";
    public const string NoBannerOptionKey = "no-banner";
    public const string SkipBuildersNumberCheckOptionKey = "internal-testingplatform-skipbuildercheck";
    public const string DiscoverTestsOptionKey = "list-tests";
    public const string ResultDirectoryOptionKey = "results-directory";
    public const string IgnoreExitCodeOptionKey = "ignore-exit-code";
    public const string MinimumExpectedTestsOptionKey = "minimum-expected-tests";
    public const string TestHostControllerPIDOptionKey = "internal-testhostcontroller-pid";

    private static readonly CommandLineOption MinimumExpectedTests = new(MinimumExpectedTestsOptionKey, "Specifies the minimum number of tests that are expected to run.", ArgumentArity.ZeroOrOne, false, isBuiltIn: true);

    /// <inheritdoc />
    public string Uid { get; } = nameof(PlatformCommandLineProvider);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = PlatformResources.PlatformCommandLineProviderDisplayName;

    /// <inheritdoc />
    public string Description { get; } = PlatformResources.PlatformCommandLineProviderDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => new CommandLineOption[]
        {
            // Visible options
            new(HelpOptionKey, PlatformResources.PlatformCommandLineHelpOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
            new(InfoOptionKey, PlatformResources.PlatformCommandLineInfoOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
            new(ResultDirectoryOptionKey, PlatformResources.PlatformCommandLineResultDirectoryOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(DiagnosticOptionKey, PlatformResources.PlatformCommandLineDiagnosticOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
            new(DiagnosticOutputFilePrefixOptionKey, PlatformResources.PlatformCommandLineDiagnosticOutputFilePrefixOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(DiagnosticOutputDirectoryOptionKey, PlatformResources.PlatformCommandLineDiagnosticOutputDirectoryOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(DiagnosticVerbosityOptionKey, PlatformResources.PlatformCommandLineDiagnosticVerbosityOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(DiagnosticFileLoggerSynchronousWriteOptionKey, PlatformResources.PlatformCommandLineDiagnosticFileLoggerSynchronousWriteOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
            MinimumExpectedTests,
            new(DiscoverTestsOptionKey, PlatformResources.PlatformCommandLineDiscoverTestsOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
            new(IgnoreExitCodeOptionKey, PlatformResources.PlatformCommandLineIgnoreExitCodeOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),

            // Hidden options
            new(ServerOptionKey, PlatformResources.PlatformCommandLineServerOptionDescription, ArgumentArity.Zero, true, isBuiltIn: true),
            new(PortOptionKey, PlatformResources.PlatformCommandLinePortOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
            new(ClientPortOptionKey, PlatformResources.PlatformCommandLineClientPortOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
            new(ClientHostOptionKey, PlatformResources.PlatformCommandLineClientHostOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
            new(SkipBuildersNumberCheckOptionKey, PlatformResources.PlatformCommandLineSkipBuildersNumberCheckOptionDescription, ArgumentArity.Zero, true, isBuiltIn: true),
            new(VSTestAdapterModeOptionKey, PlatformResources.PlatformCommandLineVSTestAdapterModeOptionDescription, ArgumentArity.Zero, true, isBuiltIn: true),
            new(NoBannerOptionKey, PlatformResources.PlatformCommandLineNoBannerOptionDescription, ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
            new(TestHostControllerPIDOptionKey, PlatformResources.PlatformCommandLineTestHostControllerPIDOptionDescription, ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
        };

    public bool OptionArgumentsAreValid(CommandLineOption option, string[] arguments, out string error)
    {
        if (option.Name == HelpOptionKey && arguments.Length > 0)
        {
            error = string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineOptionExpectsNoArgumentErrorMessage, HelpOptionKey);
            return false;
        }

        if (option.Name == InfoOptionKey && arguments.Length > 0)
        {
            error = string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineOptionExpectsNoArgumentErrorMessage, InfoOptionKey);
            return false;
        }

        if (option.Name == DiagnosticVerbosityOptionKey)
        {
            if (arguments.Length != 1
                || (!arguments[0].Equals("Trace", StringComparison.OrdinalIgnoreCase)
                    && !arguments[0].Equals("Debug", StringComparison.OrdinalIgnoreCase)
                    && !arguments[0].Equals("Information", StringComparison.OrdinalIgnoreCase)
                    && !arguments[0].Equals("Warning", StringComparison.OrdinalIgnoreCase)
                    && !arguments[0].Equals("Error", StringComparison.OrdinalIgnoreCase)
                    && !arguments[0].Equals("Critical", StringComparison.OrdinalIgnoreCase)))
            {
                error = PlatformResources.PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage;
                return false;
            }
        }

        if (option.Name == DiagnosticOutputDirectoryOptionKey && arguments.Length != 1)
        {
            error = PlatformResources.PlatformCommandLineDiagnosticOutputDirectoryOptionSingleArgument;
            return false;
        }

        if (option.Name == DiagnosticOutputFilePrefixOptionKey && arguments.Length != 1)
        {
            error = PlatformResources.PlatformCommandLineDiagnosticFilePrefixOptionSingleArgument;
            return false;
        }

        if (option.Name == ResultDirectoryOptionKey && arguments.Length != 1)
        {
            error = $"Invalid arguments for --{ResultDirectoryOptionKey}, expected usage: --results-directory ./CustomTestResultsFolder";
            return false;
        }

        if (option.Name == PortOptionKey && (arguments.Length != 1 || !int.TryParse(arguments[0], out int _)))
        {
            error = string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, PortOptionKey);
            return false;
        }

        if (option.Name == ClientPortOptionKey && (arguments.Length != 1 || !int.TryParse(arguments[0], out int _)))
        {
            error = string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, ClientPortOptionKey);
            return false;
        }

        if (option.Name == ClientHostOptionKey && arguments.Length != 1)
        {
            error = PlatformResources.PlatformCommandLineClientHostOptionSingleArgument;
            return false;
        }

        return IsMinimumExpectedTestsOptionValid(option, arguments, out error);
    }

    public static int GetMinimumExpectedTests(CommandLineParseResult parseResult)
    {
        OptionRecord? minimumExpectedTests = parseResult.Options.SingleOrDefault(o => o.Option == MinimumExpectedTestsOptionKey);
        if (minimumExpectedTests is null || !IsMinimumExpectedTestsOptionValid(MinimumExpectedTests, minimumExpectedTests.Arguments, out string _))
        {
            return 0;
        }

        ApplicationStateGuard.Ensure(parseResult.TryGetOptionArgumentList(MinimumExpectedTestsOptionKey, out string[]? arguments));
        return int.Parse(arguments[0], CultureInfo.InvariantCulture);
    }

    private static bool IsMinimumExpectedTestsOptionValid(CommandLineOption option, string[] arguments, out string error)
    {
        if (option.Name == MinimumExpectedTestsOptionKey
            && (arguments.Length != 1 || !int.TryParse(arguments[0], out int value) || value == 0))
        {
            error = PlatformResources.PlatformCommandLineMinimumExpectedTestsOptionSingleArgument;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool IsValidConfiguration(ICommandLineOptions commandLineOptions, out string? errorMessage)
    {
        errorMessage = string.Empty;

        if (!commandLineOptions.IsOptionSet(DiagnosticOptionKey))
        {
            if (commandLineOptions.IsOptionSet(DiagnosticOutputDirectoryOptionKey))
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiagnosticOptionIsMissing, DiagnosticOutputDirectoryOptionKey);
                return false;
            }

            if (commandLineOptions.IsOptionSet(DiagnosticOutputFilePrefixOptionKey))
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiagnosticOptionIsMissing, DiagnosticOutputFilePrefixOptionKey);
                return false;
            }
        }

        if (commandLineOptions.IsOptionSet(DiscoverTestsOptionKey)
            && commandLineOptions.IsOptionSet(MinimumExpectedTestsOptionKey))
        {
            errorMessage = PlatformResources.PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests;
            return false;
        }

        return true;
    }
}
