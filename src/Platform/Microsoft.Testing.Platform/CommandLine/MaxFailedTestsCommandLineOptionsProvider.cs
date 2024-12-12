// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class MaxFailedTestsCommandLineOptionsProvider(IExtension extension, IServiceProvider serviceProvider) : ICommandLineOptionsProvider
{
    internal const string MaxFailedTestsOptionKey = "maximum-failed-tests";

    private static readonly IReadOnlyCollection<CommandLineOption> OptionsCache =
    [
        new(MaxFailedTestsOptionKey, PlatformResources.PlatformCommandLineMaxFailedTestsOptionDescription, ArgumentArity.ExactlyOne, isHidden: false),
    ];

    public string Uid => extension.Uid;

    public string Version => extension.Version;

    public string DisplayName => extension.DisplayName;

    public string Description => extension.Description;

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => OptionsCache;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == MaxFailedTestsOptionKey)
        {
            string arg = arguments[0];
            // We consider --maximum-failed-tests 0 as invalid.
            // The idea is that we stop the execution when we *reach* the max failed tests, not when *exceed*.
            // So the value 1 means, stop execution on the first failure.
            return int.TryParse(arg, out int maxFailedTestsResult) && maxFailedTestsResult > 0
                ? ValidateCapabilityAsync()
                : ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.MaxFailedTestsMustBePositive, arg));
        }

        throw ApplicationStateGuard.Unreachable();
    }

    private Task<ValidationResult> ValidateCapabilityAsync()
        => serviceProvider.GetTestFrameworkCapabilities().Capabilities.OfType<IGracefulStopTestExecutionCapability>().Any()
            ? ValidationResult.ValidTask
            : ValidationResult.InvalidTask(PlatformResources.AbortForMaxFailedTestsCapabilityNotAvailable);
}
