// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed class RetryCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    public const string RetryFailedTestsOptionName = "retry-failed-tests";
    public const string RetryFailedTestsMaxPercentageOptionName = "retry-failed-tests-max-percentage";
    public const string RetryFailedTestsMaxTestsOptionName = "retry-failed-tests-max-tests";
    public const string RetryFailedTestsPipeNameOptionName = "internal-retry-pipename";

    public string Uid => nameof(RetryCommandLineOptionsProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
        [
            // Hide the extension for now, we will add tests and we will re-enable when will be good.
            // We'd like to have some iteration in prod with our dogfooders before.
            new(RetryFailedTestsOptionName, ExtensionResources.RetryFailedTestsOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(RetryFailedTestsMaxPercentageOptionName, ExtensionResources.RetryFailedTestsMaxPercentageOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
            new(RetryFailedTestsMaxTestsOptionName, ExtensionResources.RetryFailedTestsMaxTestsOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),

            // Hidden internal args
            new(RetryFailedTestsPipeNameOptionName, "Communication between the test host and the retry infra.", ArgumentArity.ExactlyOne, isHidden: true, isBuiltIn: true)
        ];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.IsOptionSet(RetryFailedTestsMaxPercentageOptionName) && commandLineOptions.IsOptionSet(RetryFailedTestsMaxTestsOptionName))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsPercentageAndCountCannotBeMixedErrorMessage, RetryFailedTestsMaxPercentageOptionName, RetryFailedTestsMaxTestsOptionName));
        }

        if (commandLineOptions.IsOptionSet(RetryFailedTestsMaxPercentageOptionName) && !commandLineOptions.IsOptionSet(RetryFailedTestsOptionName))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryFailedTestsMaxPercentageOptionName, RetryFailedTestsOptionName));
        }

        if (commandLineOptions.IsOptionSet(RetryFailedTestsMaxTestsOptionName) && !commandLineOptions.IsOptionSet(RetryFailedTestsOptionName))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryFailedTestsMaxTestsOptionName, RetryFailedTestsOptionName));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == RetryFailedTestsOptionName && !int.TryParse(arguments[0], out int _))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionSingleIntegerArgumentErrorMessage, RetryFailedTestsOptionName));
        }

        if (commandOption.Name == RetryFailedTestsMaxPercentageOptionName && !int.TryParse(arguments[0], out int _))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionSingleIntegerArgumentErrorMessage, RetryFailedTestsMaxPercentageOptionName));
        }

        if (commandOption.Name == RetryFailedTestsMaxTestsOptionName && !int.TryParse(arguments[0], out int _))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionSingleIntegerArgumentErrorMessage, RetryFailedTestsMaxTestsOptionName));
        }

        // No problem found
        return ValidationResult.ValidTask;
    }
}
