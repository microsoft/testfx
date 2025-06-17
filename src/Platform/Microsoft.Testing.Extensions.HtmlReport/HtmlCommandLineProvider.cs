// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Reporting;

internal sealed class HtmlCommandLineProvider : ICommandLineOptionsProvider
{
    private static readonly string[] SeverityOptions = ["error", "warning"];

    public string Uid => nameof(HtmlCommandLineProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => HtmlResources.DisplayName;

    public string Description => HtmlResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        =>
        [
            new CommandLineOption(HtmlCommandLineOptions.HtmlOptionName, HtmlResources.OptionDescription, ArgumentArity.Zero, false),
            new CommandLineOption(HtmlCommandLineOptions.HtmlReportSeverity, HtmlResources.SeverityOptionDescription, ArgumentArity.ExactlyOne, false),
        ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == HtmlCommandLineOptions.HtmlReportSeverity)
        {
            if (!SeverityOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, HtmlResources.InvalidSeverity, arguments[0]));
            }
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (!commandLineOptions.IsOptionSet(HtmlCommandLineOptions.HtmlOptionName) &&
            commandLineOptions.IsOptionSet(HtmlCommandLineOptions.HtmlReportSeverity))
        {
            // If report-html is not set, but report-html-severity is set, it's invalid.
            return ValidationResult.InvalidTask(HtmlResources.HtmlReportSeverityRequiresHtml);
        }

        return ValidationResult.ValidTask;
    }
}
