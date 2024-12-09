// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.CommandLine;

internal sealed class MaxFailedTestsCommandLineOptionsProvider(IExtension extension) : ICommandLineOptionsProvider
{
    internal const string MaxFailedTestsOptionKey = "maximum-failed-tests";

    private static readonly IReadOnlyCollection<CommandLineOption> OptionsCache =
    [
        new(MaxFailedTestsOptionKey, PlatformResources.PlatformCommandLineMaxFailedTestsOptionDescription, ArgumentArity.ExactlyOne, isHidden: false/*TODO: Should we pass isBuiltIn: true??*/),
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
            // We consider --maximum-failed-tests 0 as valid.
            // The idea is that we stop the execution when we *exceed* the max failed tests, not when *reach*.
            // So zero means, stop execution on the first failure.
            return int.TryParse(arg, out int maxFailedTestsResult) && maxFailedTestsResult >= 0
                ? ValidationResult.ValidTask
                : ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.MaxFailedTestsMustBePositive, arg));
        }

        throw ApplicationStateGuard.Unreachable();
    }
}
