// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace TestingPlatformExplorer.TestingFramework;

internal sealed class TestingFrameworkCommandLineOptions : ICommandLineOptionsProvider
{
    public const string DopOption = "dop";
    public const string GenerateReportOption = "generatereport";
    public const string ReportFilenameOption = "reportfilename";

    public string Uid => nameof(TestingFrameworkCommandLineOptions);

    public string Version => "1.0.0";

    public string DisplayName => nameof(TestingFrameworkCommandLineOptions);

    public string Description => "Testing framework command line options";

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() => new[]
    {
        new CommandLineOption(DopOption,"Degree of parallelism", ArgumentArity.ExactlyOne, false),
        new CommandLineOption(GenerateReportOption,"Generate a test report file", ArgumentArity.Zero, false),
        new CommandLineOption(ReportFilenameOption,"Report file name to use together with --generatereport", ArgumentArity.ExactlyOne, false),
    };

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == DopOption)
        {
            if (!int.TryParse(arguments[0], out int dopValue) || dopValue <= 0)
            {
                return ValidationResult.InvalidTask("Dop must be a positive integer");
            }
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        bool generateReportEnabled = commandLineOptions.IsOptionSet(GenerateReportOption);
        bool reportFileName = commandLineOptions.TryGetOptionArgumentList(ReportFilenameOption, out string[]? _);

        return (generateReportEnabled || reportFileName) && !(generateReportEnabled && reportFileName)
            ? ValidationResult.InvalidTask("--generatereport and --reportfilename must be specified together")
            : ValidationResult.ValidTask;
    }
}
