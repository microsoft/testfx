// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

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
    public const string MinimumExpectedTestsOptionKey = "minimum-expected-tests";
    public const string TestHostControllerPIDOptionKey = "internal-testhostcontroller-pid";

    private static readonly CommandLineOption MinimumExpectedTests = new(MinimumExpectedTestsOptionKey, "Specifies the minimum number of tests that are expected to run.", ArgumentArity.ZeroOrOne, false, isBuiltIn: true);

    /// <inheritdoc />
    public string Uid { get; } = nameof(PlatformCommandLineProvider);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = "Microsoft Testing Platform command line provider";

    /// <inheritdoc />
    public string Description { get; } = "Built-in command line provider";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public CommandLineOption[] GetCommandLineOptions()
        => new CommandLineOption[]
        {
            // Visible options
            new(HelpOptionKey, "Show command line help.", ArgumentArity.Zero, false, isBuiltIn: true),
            new(InfoOptionKey, "Display .NET test application information.", ArgumentArity.Zero, false, isBuiltIn: true),
            new(ResultDirectoryOptionKey, "The directory where the test results are going to be placed. If the specified directory doesn't exist, it's created. The default is TestResults in the directory that contains the test application.", ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(DiagnosticOptionKey, "Enable the diagnostic logging. The default log level is 'Information'. The file will be written in the output directory with the name log_[MMddHHssfff].diag", ArgumentArity.Zero, false, isBuiltIn: true),
            new(DiagnosticOutputFilePrefixOptionKey, "Prefix for the log file name that will replace [log]_.", ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(DiagnosticOutputDirectoryOptionKey, "Output directory of the diagnostic logging, if not specified the file will be generated inside the default 'TestResults' directory.", ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(DiagnosticVerbosityOptionKey, "Define the level of the verbosity for the --diagnostic. The available values are Trace, Debug, Information, Warning, Error, Critical", ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(DiagnosticFileLoggerSynchronousWriteOptionKey, "Force the built-in file logger to write the log synchronously. Useful for scenario where you don't want to lose any log (i.e. in case of crash). The effect is to slow down the test execution.", ArgumentArity.Zero, false, isBuiltIn: true),
            MinimumExpectedTests,
            new(DiscoverTestsOptionKey, "List available tests.", ArgumentArity.Zero, false, isBuiltIn: true),

            // Hidden options
            new(ServerOptionKey, "Enable server mode.", ArgumentArity.Zero, true, isBuiltIn: true),
            new(PortOptionKey, "Specify the port of the server.", ArgumentArity.ExactlyOne, true, isBuiltIn: true),
            new(ClientPortOptionKey, "Specify the port of the client.", ArgumentArity.ExactlyOne, true, isBuiltIn: true),
            new(ClientHostOptionKey, "Specify the hostname of the client.", ArgumentArity.ExactlyOne, true, isBuiltIn: true),
            new(SkipBuildersNumberCheckOptionKey, "Used to for testing purpose.", ArgumentArity.Zero, true, isBuiltIn: true),
            new(VSTestAdapterModeOptionKey, "Used to bridge tpv2 to test anywhere.", ArgumentArity.Zero, true, isBuiltIn: true),
            new(NoBannerOptionKey, "Doesn't display the startup banner, the copyright message or the telemetry banner.", ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
            new(TestHostControllerPIDOptionKey, "Eventual parent eventual test host controller PID.", ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
        };

    public bool OptionArgumentsAreValid(CommandLineOption option, string[] arguments, out string error)
    {
        if (option.Name == HelpOptionKey && arguments.Length > 0)
        {
            error = $"Invalid arguments for --{HelpOptionKey} option. This option does not accept any argument.";
            return false;
        }

        if (option.Name == InfoOptionKey && arguments.Length > 0)
        {
            error = $"Invalid arguments for --{InfoOptionKey} option. This option does not accept any argument.";
            return false;
        }

        if (option.Name == DiagnosticVerbosityOptionKey && arguments.Length == 0)
        {
            error = $"Invalid arguments for --{DiagnosticVerbosityOptionKey} option. Missing level.";
            return false;
        }

        if (option.Name == DiagnosticVerbosityOptionKey && arguments.Length > 1)
        {
            error = $"Invalid arguments for --{DiagnosticVerbosityOptionKey} option. Expected only one level.";
            return false;
        }

        if (option.Name == DiagnosticOutputDirectoryOptionKey && arguments.Length != 1)
        {
            error = $"Invalid arguments for --{DiagnosticOutputDirectoryOptionKey} option. Expected one directory name.";
            return false;
        }

        if (option.Name == DiagnosticOutputFilePrefixOptionKey && arguments.Length != 1)
        {
            error = $"Invalid arguments for --{DiagnosticOutputFilePrefixOptionKey} option. Expected one prefix file name.";
            return false;
        }

        if (option.Name == DiagnosticVerbosityOptionKey &&
            !(
                arguments[0].Equals("Trace", StringComparison.OrdinalIgnoreCase) ||
                arguments[0].Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
                arguments[0].Equals("Information", StringComparison.OrdinalIgnoreCase) ||
                arguments[0].Equals("Warning", StringComparison.OrdinalIgnoreCase) ||
                arguments[0].Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                arguments[0].Equals("Critical", StringComparison.OrdinalIgnoreCase)))
        {
            error = $"Invalid arguments for --{DiagnosticVerbosityOptionKey} option. Expected Trace, Debug, Information, Warning, Error, Critical.";
            return false;
        }

        if (option.Name == ResultDirectoryOptionKey && arguments.Length != 1)
        {
            error = $"Invalid arguments for --{ResultDirectoryOptionKey}, expected usage: --results-directory ./CustomTestResultsFolder";
            return false;
        }

        if (option.Name == PortOptionKey && arguments.Length > 1)
        {
            error = $"Invalid arguments for --{PortOptionKey} option. Expected only one port.";
            return false;
        }

        if (option.Name == PortOptionKey && !int.TryParse(arguments[0], out int _))
        {
            error = $"Invalid arguments for --{PortOptionKey} option. Expected a valid port number.";
            return false;
        }

        if (option.Name == ClientPortOptionKey && arguments.Length != 1)
        {
            error = $"Invalid arguments for --{ClientPortOptionKey} option. Expected one port.";
            return false;
        }

        if (option.Name == ClientPortOptionKey && !int.TryParse(arguments[0], out int _))
        {
            error = $"Invalid arguments for --{ClientPortOptionKey} option. Expected a valid port number.";
            return false;
        }

        if (option.Name == ClientHostOptionKey && arguments.Length != 1)
        {
            error = $"Invalid arguments for --{ClientHostOptionKey} option. Expected one host.";
            return false;
        }

        return IsMinimumExpectedTestsOptionValid(option, arguments, out error);
    }

    public static int GetMinimumExpectedTests(CommandLineParseResult parseResult)
    {
        OptionRecord? minimumExpectedTests = parseResult.Options.SingleOrDefault(o => o.Option == MinimumExpectedTestsOptionKey);
        if (minimumExpectedTests is not null)
        {
            if (IsMinimumExpectedTestsOptionValid(MinimumExpectedTests, minimumExpectedTests.Arguments, out string _))
            {
                return !parseResult.TryGetOptionArgumentList(MinimumExpectedTestsOptionKey, out string[]? arguments)
                    ? throw new InvalidOperationException("Unexpected missing MinimumExpectedTests option")
                    : int.Parse(arguments[0], CultureInfo.InvariantCulture);
            }
        }

        return 0;
    }

    private static bool IsMinimumExpectedTestsOptionValid(CommandLineOption option, string[] arguments, out string error)
    {
        error = string.Empty;

        if (option.Name == MinimumExpectedTestsOptionKey && arguments.Length != 1)
        {
            error = $"Invalid arguments for --{MinimumExpectedTestsOptionKey}, expected usage: --{MinimumExpectedTestsOptionKey} 10";
            return false;
        }

        if (option.Name == MinimumExpectedTestsOptionKey && !int.TryParse(arguments[0], out int _))
        {
            error = $"Invalid arguments for --{MinimumExpectedTestsOptionKey}, expected argument should be an integer, actual value '{arguments[0]}'";
            return false;
        }

        if (option.Name == MinimumExpectedTestsOptionKey && int.Parse(arguments[0], CultureInfo.InvariantCulture) == 0)
        {
            error = $"Invalid arguments for --{MinimumExpectedTestsOptionKey}, value 0 not allowed.";
            return false;
        }

        return true;
    }

    public bool IsValidConfiguration(ICommandLineOptions commandLineOptions, out string? errorMessage)
    {
        errorMessage = string.Empty;
        if (commandLineOptions.IsOptionSet(DiagnosticOutputFilePrefixOptionKey)
            && commandLineOptions.IsOptionSet(DiagnosticOutputDirectoryOptionKey)
            && !commandLineOptions.IsOptionSet(DiagnosticOptionKey))
        {
            errorMessage = $"Option '{DiagnosticOptionKey}' must be present when using '{DiagnosticOutputFilePrefixOptionKey}' and '{DiagnosticOutputDirectoryOptionKey}'.";
            return false;
        }

        if (commandLineOptions.IsOptionSet(DiagnosticOutputFilePrefixOptionKey)
            && !commandLineOptions.IsOptionSet(DiagnosticOptionKey))
        {
            errorMessage = $"Option '{DiagnosticOptionKey}' must be present when using '{DiagnosticOutputFilePrefixOptionKey}'.";
            return false;
        }

        if (commandLineOptions.IsOptionSet(DiagnosticOutputDirectoryOptionKey)
            && !commandLineOptions.IsOptionSet(DiagnosticOptionKey))
        {
            errorMessage = $"Option '{DiagnosticOptionKey}' must be present when using '{DiagnosticOutputDirectoryOptionKey}'.";
            return false;
        }

        if (commandLineOptions.IsOptionSet(DiscoverTestsOptionKey)
            && commandLineOptions.IsOptionSet(MinimumExpectedTestsOptionKey))
        {
            errorMessage = $"Option '{DiscoverTestsOptionKey}' is incompatible with option '{MinimumExpectedTestsOptionKey}'.";
            return false;
        }

        return true;
    }
}
