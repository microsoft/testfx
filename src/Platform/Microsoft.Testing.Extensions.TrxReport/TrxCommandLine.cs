// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGeneratorCommandLine : CommandLineOptionsProviderBase
{
    public const string TrxReportOptionName = "report-trx";
    public const string TrxReportFileNameOptionName = "report-trx-filename";

    public TrxReportGeneratorCommandLine()
        : base(
            nameof(TrxReportGeneratorCommandLine),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.TrxReportGeneratorDisplayName,
            ExtensionResources.TrxReportGeneratorDescription,
            [
                new(TrxReportOptionName, ExtensionResources.TrxReportOptionDescription, ArgumentArity.Zero, false),
                new(TrxReportFileNameOptionName, ExtensionResources.TrxReportFileNameOptionDescription, ArgumentArity.ExactlyOne, false)
            ])
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == TrxReportFileNameOptionName)
        {
            if (arguments.Length is 0)
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameMustNotBeEmpty);
            }

            string argument = arguments[0];

            // We accept relative or absolute paths, but the leaf must be a non-empty file name
            // that ends with ".trx". Relative paths must stay under the test results directory,
            // while the directory portion of valid paths is treated as a literal path and validated
            // by the OS when we open the file.
            string fileNamePart = Path.GetFileName(argument);
            if (RoslynString.IsNullOrWhiteSpace(fileNamePart))
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameMustNotBeEmpty);
            }

            if (!fileNamePart.EndsWith(".trx", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameExtensionIsNotTrx);
            }

            if (ReportFileNameValidator.EscapesResultsDirectory(argument))
            {
                return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameRelativePathMustStayUnderResultsDirectory);
            }
        }

        return ValidationResult.ValidTask;
    }

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.IsOptionSet(TrxReportFileNameOptionName)
            && !commandLineOptions.IsOptionSet(TrxReportOptionName))
        {
            return ValidationResult.InvalidTask(ExtensionResources.TrxReportFileNameRequiresTrxReport);
        }

        if (commandLineOptions.IsOptionSet(TrxReportOptionName)
            && commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey))
        {
            return ValidationResult.InvalidTask(ExtensionResources.TrxReportIsNotValidForDiscovery);
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
