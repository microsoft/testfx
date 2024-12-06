// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.TestFramework;

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class MaxFailedTestsCommandLineOptionsProvider : ICommandLineOptionsProvider
{
    // TODO: We have 'minimum-expected-tests', so should we use "maximum" instead of "max" here as well for consistency?
    internal const string MaxFailedTestsOptionKey = "max-failed-tests";

    private static readonly IReadOnlyCollection<CommandLineOption> OptionsCache =
    [
        new(MaxFailedTestsOptionKey, PlatformResources.PlatformCommandLineMaxFailedTestsOptionDescription, ArgumentArity.ExactlyOne, false, isBuiltIn: true),
    ];

    public string Uid => nameof(MaxFailedTestsCommandLineOptionsProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => PlatformResources.PlatformCommandLineProviderDisplayName; /*TODO: New display name?*/

    public string Description => PlatformResources.PlatformCommandLineProviderDescription; /*TODO: New description?*/

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
        => OptionsCache;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) => throw new NotImplementedException();

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == MaxFailedTestsOptionKey)
        {
            string arg = arguments[0];
            if (!int.TryParse(arg, out int maxFailedTestsResult) || maxFailedTestsResult <= 0)
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, PlatformResources.MaxFailedTestsMustBePositive, arg));
            }
        }

        throw ApplicationStateGuard.Unreachable();
    }
}
