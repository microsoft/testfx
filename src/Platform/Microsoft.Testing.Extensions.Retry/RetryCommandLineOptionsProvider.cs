// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed class RetryCommandLineOptionsProvider : CommandLineOptionsProviderBase
{
    public const string RetryFailedTestsOptionName = "retry-failed-tests";
    public const string RetryFailedTestsMaxPercentageOptionName = "retry-failed-tests-max-percentage";
    public const string RetryFailedTestsMaxTestsOptionName = "retry-failed-tests-max-tests";
    public const string RetryFailedTestsDelayOptionName = "retry-failed-tests-delay";
    public const string RetryFailedTestsPipeNameOptionName = "internal-retry-pipename";

    public RetryCommandLineOptionsProvider()
        : base(
            nameof(RetryCommandLineOptionsProvider),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.RetryFailedTestsExtensionDisplayName,
            ExtensionResources.RetryFailedTestsExtensionDescription,
            [
                // The retry options are visible in --help today. We're still iterating on the feature
                // with our dogfooders and may revisit visibility (and/or argument shapes) before
                // declaring it stable.
                new(RetryFailedTestsOptionName, ExtensionResources.RetryFailedTestsOptionDescription, ArgumentArity.ExactlyOne, isHidden: false),
                new(RetryFailedTestsMaxPercentageOptionName, ExtensionResources.RetryFailedTestsMaxPercentageOptionDescription, ArgumentArity.ExactlyOne, isHidden: false),
                new(RetryFailedTestsMaxTestsOptionName, ExtensionResources.RetryFailedTestsMaxTestsOptionDescription, ArgumentArity.ExactlyOne, isHidden: false),
                new(RetryFailedTestsDelayOptionName, ExtensionResources.RetryFailedTestsDelayOptionDescription, ArgumentArity.ExactlyOne, isHidden: false),

                // Hidden internal args
                new(RetryFailedTestsPipeNameOptionName, "Communication between the test host and the retry infra.", ArgumentArity.ExactlyOne, isHidden: true, isBuiltIn: true)
            ])
    {
    }

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => commandLineOptions.IsOptionSet(RetryFailedTestsMaxPercentageOptionName) && commandLineOptions.IsOptionSet(RetryFailedTestsMaxTestsOptionName)
            ? ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsPercentageAndCountCannotBeMixedErrorMessage, RetryFailedTestsMaxPercentageOptionName, RetryFailedTestsMaxTestsOptionName))
            : RequiresMainOption(commandLineOptions, [RetryFailedTestsMaxPercentageOptionName], RetryFailedTestsOptionName,
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryFailedTestsMaxPercentageOptionName, RetryFailedTestsOptionName))
            ?? RequiresMainOption(commandLineOptions, [RetryFailedTestsMaxTestsOptionName], RetryFailedTestsOptionName,
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryFailedTestsMaxTestsOptionName, RetryFailedTestsOptionName))
            ?? RequiresMainOption(commandLineOptions, [RetryFailedTestsDelayOptionName], RetryFailedTestsOptionName,
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionIsMissingErrorMessage, RetryFailedTestsDelayOptionName, RetryFailedTestsOptionName))
            ?? ValidationResult.ValidTask;

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == RetryFailedTestsOptionName && !TryParseNonNegativeInt(arguments[0], out int _))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionNonNegativeIntegerArgumentErrorMessage, RetryFailedTestsOptionName));
        }

        if (commandOption.Name == RetryFailedTestsMaxPercentageOptionName
            && (!TryParseNonNegativeInt(arguments[0], out int percentage)
                || percentage > 100))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsMaxPercentageOptionIntegerBetween0And100ArgumentErrorMessage, RetryFailedTestsMaxPercentageOptionName));
        }

        if (commandOption.Name == RetryFailedTestsMaxTestsOptionName && !TryParseNonNegativeInt(arguments[0], out int _))
        {
            return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, ExtensionResources.RetryFailedTestsOptionNonNegativeIntegerArgumentErrorMessage, RetryFailedTestsMaxTestsOptionName));
        }

        if (commandOption.Name == RetryFailedTestsDelayOptionName
            && (!TimeSpanParser.TryParse(arguments[0], out TimeSpan delay)
                || delay < TimeSpan.Zero
                || delay.TotalMilliseconds > int.MaxValue))
        {
            return ValidationResult.InvalidTask(ExtensionResources.RetryFailedTestsDelayOptionInvalidArgument);
        }

        // No problem found
        return ValidationResult.ValidTask;
    }

    private static bool TryParseNonNegativeInt(string value, out int result)
        => int.TryParse(value, out result) && result >= 0;
}
