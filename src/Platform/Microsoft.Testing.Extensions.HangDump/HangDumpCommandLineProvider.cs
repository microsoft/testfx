// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpCommandLineProvider : ICommandLineOptionsProvider
{
    public const string HangDumpOptionName = "hangdump";
    public const string HangDumpFileNameOptionName = "hangdump-filename";
    public const string HangDumpTimeoutOptionName = "hangdump-timeout";
    public const string HangDumpTypeOptionName = "hangdump-type";

#if NETCOREAPP
    private static readonly string[] HangDumpTypeOptions = ["Mini", "Heap", "Full", "Triage"];
#else
    private static readonly string[] HangDumpTypeOptions = ["Mini", "Heap", "Full"];
#endif

    private static readonly IReadOnlyCollection<CommandLineOption> CachedCommandLineOptions =
    [
        new(HangDumpOptionName, ExtensionResources.HangDumpOptionDescription, ArgumentArity.Zero, false),
        new(HangDumpTimeoutOptionName, ExtensionResources.HangDumpTimeoutOptionDescription, ArgumentArity.ExactlyOne, false),
        new(HangDumpFileNameOptionName, ExtensionResources.HangDumpFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
        new(HangDumpTypeOptionName, ExtensionResources.HangDumpTypeOptionDescription, ArgumentArity.ExactlyOne, false)
    ];

    private readonly HangDumpConfiguration _hangDumpConfiguration;

    public HangDumpCommandLineProvider(HangDumpConfiguration hangDumpConfiguration) => _hangDumpConfiguration = hangDumpConfiguration;

    public string Uid => nameof(HangDumpCommandLineProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() => CachedCommandLineOptions;

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == HangDumpTimeoutOptionName && !TimeSpanParser.TryParse(arguments[0], out TimeSpan _))
        {
            return ValidationResult.InvalidTask(ExtensionResources.HangDumpTimeoutOptionInvalidArgument);
        }

        if (commandOption.Name == HangDumpTypeOptionName)
        {
            if (!HangDumpTypeOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeOptionInvalidType, arguments[0]));
            }
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => (commandLineOptions.IsOptionSet(HangDumpTimeoutOptionName) ||
            commandLineOptions.IsOptionSet(HangDumpFileNameOptionName) ||
            commandLineOptions.IsOptionSet(HangDumpTypeOptionName)) &&
            !commandLineOptions.IsOptionSet(HangDumpOptionName)
            ? ValidationResult.InvalidTask(ExtensionResources.MissingHangDumpMainOption)
            : ValidationResult.ValidTask;
}
