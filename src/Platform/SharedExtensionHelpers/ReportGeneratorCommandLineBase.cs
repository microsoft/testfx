// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Microsoft.Testing.Extensions;

internal abstract class ReportGeneratorCommandLineBase : CommandLineOptionsProviderBase
{
    private readonly string _reportOptionName;
    private readonly string _reportFileNameOptionName;
    private readonly string _fileExtension;
    private readonly string _fileNameMustNotBeEmpty;
    private readonly string _fileNameExtensionIsNotExpected;
    private readonly string _relativePathMustStayUnderResultsDirectory;
    private readonly string _fileNameRequiresReport;
    private readonly string _reportIsNotValidForDiscovery;

    protected ReportGeneratorCommandLineBase(
        string providerName,
        string displayName,
        string description,
        string reportOptionName,
        string reportFileNameOptionName,
        string reportOptionDescription,
        string reportFileNameOptionDescription,
        string fileExtension,
        string fileNameMustNotBeEmpty,
        string fileNameExtensionIsNotExpected,
        string relativePathMustStayUnderResultsDirectory,
        string fileNameRequiresReport,
        string reportIsNotValidForDiscovery)
        : base(
            providerName,
            ExtensionVersion.DefaultSemVer,
            displayName,
            description,
            [
                new(reportOptionName, reportOptionDescription, ArgumentArity.Zero, false),
                new(reportFileNameOptionName, reportFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
            ])
    {
        _reportOptionName = reportOptionName;
        _reportFileNameOptionName = reportFileNameOptionName;
        _fileExtension = fileExtension;
        _fileNameMustNotBeEmpty = fileNameMustNotBeEmpty;
        _fileNameExtensionIsNotExpected = fileNameExtensionIsNotExpected;
        _relativePathMustStayUnderResultsDirectory = relativePathMustStayUnderResultsDirectory;
        _fileNameRequiresReport = fileNameRequiresReport;
        _reportIsNotValidForDiscovery = reportIsNotValidForDiscovery;
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => commandOption.Name == _reportFileNameOptionName
            ? ReportFileNameValidator.ValidateReportFileNameArgumentAsync(
                arguments,
                _fileExtension,
                _fileNameMustNotBeEmpty,
                _fileNameExtensionIsNotExpected,
                _relativePathMustStayUnderResultsDirectory)
            : ValidationResult.ValidTask;

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ReportFileNameValidator.ValidateReportCommandLineOptionsAsync(
            commandLineOptions,
            _reportOptionName,
            _reportFileNameOptionName,
            _fileNameRequiresReport,
            _reportIsNotValidForDiscovery,
            PlatformCommandLineProvider.DiscoverTestsOptionKey);
}
