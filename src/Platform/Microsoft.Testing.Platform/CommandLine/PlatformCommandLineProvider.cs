﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class PlatformCommandLineProvider : ICommandLineOptionsProvider
{
    public const string HelpOptionKey = "help";
    public const string HelpOptionQuestionMark = "?";
    public const string TimeoutOptionKey = "timeout";
    public const string InfoOptionKey = "info";
    public const string DiagnosticOptionKey = "diagnostic";
    public const string DiagnosticOutputFilePrefixOptionKey = "diagnostic-output-fileprefix";
    public const string DiagnosticOutputDirectoryOptionKey = "diagnostic-output-directory";
    public const string DiagnosticVerbosityOptionKey = "diagnostic-verbosity";
    public const string DiagnosticFileLoggerSynchronousWriteOptionKey = "diagnostic-filelogger-synchronouswrite";
    public const string VSTestAdapterModeOptionKey = "internal-vstest-adapter";
    public const string NoBannerOptionKey = "no-banner";
    public const string SkipBuildersNumberCheckOptionKey = "internal-testingplatform-skipbuildercheck";
    public const string DiscoverTestsOptionKey = "list-tests";
    public const string ResultDirectoryOptionKey = "results-directory";
    public const string IgnoreExitCodeOptionKey = "ignore-exit-code";
    public const string MinimumExpectedTestsOptionKey = "minimum-expected-tests";
    public const string TestHostControllerPIDOptionKey = "internal-testhostcontroller-pid";
    public const string ExitOnProcessExitOptionKey = "exit-on-process-exit";
    public const string ConfigFileOptionKey = "config-file";

    public const string ServerOptionKey = "server";
    public const string ClientPortOptionKey = "client-port";
    public const string ClientHostOptionKey = "client-host";
    public const string JsonRpcProtocolName = "jsonrpc";
    public const string DotNetTestPipeOptionKey = "dotnet-test-pipe";
    public const string DotnetTestCliProtocolName = "dotnettestcli";

    private static readonly string[] VerbosityOptions = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"];

    private static readonly CommandLineOption MinimumExpectedTests = new(MinimumExpectedTestsOptionKey, PlatformResources.PlatformCommandLineMinimumExpectedTestsOptionDescription, ArgumentArity.ZeroOrOne, false, isBuiltIn: true);

    private static readonly IReadOnlyCollection<CommandLineOption> PlatformCommandLineProviderCache =
    [
        // Visible options
        new(HelpOptionKey, PlatformResources.PlatformCommandLineHelpOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        new(InfoOptionKey, PlatformResources.PlatformCommandLineInfoOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        new(TimeoutOptionKey, PlatformResources.PlatformCommandLineTimeoutOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(ResultDirectoryOptionKey, PlatformResources.PlatformCommandLineResultDirectoryOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiagnosticOptionKey, PlatformResources.PlatformCommandLineDiagnosticOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        new(DiagnosticOutputFilePrefixOptionKey, PlatformResources.PlatformCommandLineDiagnosticOutputFilePrefixOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiagnosticOutputDirectoryOptionKey, PlatformResources.PlatformCommandLineDiagnosticOutputDirectoryOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiagnosticVerbosityOptionKey, PlatformResources.PlatformCommandLineDiagnosticVerbosityOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(DiagnosticFileLoggerSynchronousWriteOptionKey, PlatformResources.PlatformCommandLineDiagnosticFileLoggerSynchronousWriteOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        MinimumExpectedTests,
        new(DiscoverTestsOptionKey, PlatformResources.PlatformCommandLineDiscoverTestsOptionDescription, ArgumentArity.Zero, false, isBuiltIn: true),
        new(IgnoreExitCodeOptionKey, PlatformResources.PlatformCommandLineIgnoreExitCodeOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(ExitOnProcessExitOptionKey, PlatformResources.PlatformCommandLineExitOnProcessExitOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
        new(ConfigFileOptionKey, PlatformResources.PlatformCommandLineConfigFileOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),

        // Hidden options
        new(HelpOptionQuestionMark, PlatformResources.PlatformCommandLineHelpOptionDescription, ArgumentArity.Zero, true, isBuiltIn: true),
        new(ServerOptionKey, PlatformResources.PlatformCommandLineServerOptionDescription, ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
        new(ClientPortOptionKey, PlatformResources.PlatformCommandLineClientPortOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
        new(ClientHostOptionKey, PlatformResources.PlatformCommandLineClientHostOptionDescription, ArgumentArity.ExactlyOne, true, isBuiltIn: true),
        new(SkipBuildersNumberCheckOptionKey, PlatformResources.PlatformCommandLineSkipBuildersNumberCheckOptionDescription, ArgumentArity.Zero, true, isBuiltIn: true),
        new(VSTestAdapterModeOptionKey, PlatformResources.PlatformCommandLineVSTestAdapterModeOptionDescription, ArgumentArity.Zero, true, isBuiltIn: true),
        new(NoBannerOptionKey, PlatformResources.PlatformCommandLineNoBannerOptionDescription, ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
        new(TestHostControllerPIDOptionKey, PlatformResources.PlatformCommandLineTestHostControllerPIDOptionDescription, ArgumentArity.ZeroOrOne, true, isBuiltIn: true),
        new(DotNetTestPipeOptionKey, PlatformResources.PlatformCommandLineDotnetTestPipe, ArgumentArity.ExactlyOne, true, isBuiltIn: true)
    ];

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
        => PlatformCommandLineProviderCache;

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == DiagnosticVerbosityOptionKey)
        {
            if (!VerbosityOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineDiagnosticOptionExpectsSingleArgumentErrorMessage);
            }
        }

        if (commandOption.Name == ClientPortOptionKey && (!int.TryParse(arguments[0], out int _)))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLinePortOptionSingleArgument, ClientPortOptionKey));
        }

        if (commandOption.Name == ExitOnProcessExitOptionKey && (!int.TryParse(arguments[0], out int _)))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineExitOnProcessExitSingleArgument, ExitOnProcessExitOptionKey));
        }

        if (commandOption.Name == TimeoutOptionKey)
        {
            string arg = arguments[0];
            int size = arg.Length;
            if ((char.ToLowerInvariant(arg[size - 1]) != 'h' && char.ToLowerInvariant(arg[size - 1]) != 'm' && char.ToLowerInvariant(arg[size - 1]) != 's') || !float.TryParse(arg[..(size - 1)], out float _))
            {
                return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineTimeoutArgumentErrorMessage);
            }
        }

        if (commandOption.Name == ConfigFileOptionKey)
        {
            string arg = arguments[0];
            if (!File.Exists(arg))
            {
                try
                {
                    // Get the full path for better error messages.
                    // As this is only for the purpose of throwing an exception, ignore any exceptions during the GetFullPath call.
                    arg = Path.GetFullPath(arg);
                }
                catch
                {
                }

                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.ConfigurationFileNotFound, arg));
            }
        }

        // Now validate the minimum expected tests option
        return IsMinimumExpectedTestsOptionValidAsync(commandOption, arguments);
    }

    public static int GetMinimumExpectedTests(ICommandLineOptions commandLineOptions)
    {
        bool hasMinimumExpectedTestsOptionKey = commandLineOptions.TryGetOptionArgumentList(MinimumExpectedTestsOptionKey, out string[]? minimumExpectedTests);
        if (!hasMinimumExpectedTestsOptionKey || !IsMinimumExpectedTestsOptionValidAsync(MinimumExpectedTests, minimumExpectedTests ?? Array.Empty<string>()).Result.IsValid)
        {
            return 0;
        }

        ApplicationStateGuard.Ensure(commandLineOptions.TryGetOptionArgumentList(MinimumExpectedTestsOptionKey, out string[]? arguments));
        return int.Parse(arguments[0], CultureInfo.InvariantCulture);
    }

    private static Task<ValidationResult> IsMinimumExpectedTestsOptionValidAsync(CommandLineOption option, string[] arguments)
        => option.Name == MinimumExpectedTestsOptionKey
            && (arguments.Length != 1 || !int.TryParse(arguments[0], out int value) || value == 0)
            ? ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineMinimumExpectedTestsOptionSingleArgument)
            : ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (!commandLineOptions.IsOptionSet(DiagnosticOptionKey))
        {
            if (commandLineOptions.IsOptionSet(DiagnosticOutputDirectoryOptionKey))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiagnosticOptionIsMissing, DiagnosticOutputDirectoryOptionKey));
            }

            if (commandLineOptions.IsOptionSet(DiagnosticOutputFilePrefixOptionKey))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineDiagnosticOptionIsMissing, DiagnosticOutputFilePrefixOptionKey));
            }
        }

        if (commandLineOptions.IsOptionSet(DiscoverTestsOptionKey)
            && commandLineOptions.IsOptionSet(MinimumExpectedTestsOptionKey))
        {
            return ValidationResult.InvalidTask(PlatformResources.PlatformCommandLineMinimumExpectedTestsIncompatibleDiscoverTests);
        }

        if (commandLineOptions.IsOptionSet(ExitOnProcessExitOptionKey))
        {
            _ = commandLineOptions.TryGetOptionArgumentList(ExitOnProcessExitOptionKey, out string[]? pid);
            ApplicationStateGuard.Ensure(pid is not null);
            RoslynDebug.Assert(pid.Length == 1);
            int parentProcessPid = int.Parse(pid[0], CultureInfo.InvariantCulture);
            try
            {
                // We let the api to do the validity check before to go down the subscription path.
                // If we don't fail here but we fail below means that the parent process is not there anymore and we can take it as exited.
                _ = Process.GetProcessById(parentProcessPid);
            }
            catch (ArgumentException ex)
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.PlatformCommandLineExitOnProcessExitInvalidDependentProcess, parentProcessPid, ex));
            }
        }

        // Validation succeeded
        return ValidationResult.ValidTask;
    }
}
