// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class HangDumpCommandLineProvider : CommandLineOptionsProviderBase
{
    public const string HangDumpOptionName = "hangdump";
    public const string HangDumpFileNameOptionName = "hangdump-filename";
    public const string HangDumpTimeoutOptionName = "hangdump-timeout";
    public const string HangDumpTypeOptionName = "hangdump-type";

#if NETCOREAPP
    private static readonly string[] HangDumpTypeOptions = ["Mini", "Heap", "Full", "Triage", "None"];
#else
    private static readonly string[] HangDumpTypeOptions = ["Mini", "Heap", "Full", "None"];
#endif

    private static readonly string HangDumpTypeOptionsFormatted = string.Join(", ", Array.ConvertAll(HangDumpTypeOptions, option => $"'{option}'"));

    private static readonly IReadOnlyCollection<CommandLineOption> CachedCommandLineOptions =
    [
        new(HangDumpOptionName, ExtensionResources.HangDumpOptionDescription, ArgumentArity.Zero, false),
        new(HangDumpTimeoutOptionName, ExtensionResources.HangDumpTimeoutOptionDescription, ArgumentArity.ExactlyOne, false),
        new(HangDumpFileNameOptionName, ExtensionResources.HangDumpFileNameOptionDescription, ArgumentArity.ExactlyOne, false),
        new(
            HangDumpTypeOptionName,
            string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeOptionDescription, HangDumpTypeOptionsFormatted),
            ArgumentArity.ExactlyOne,
            false)
    ];

    public HangDumpCommandLineProvider()
        : base(
            nameof(HangDumpCommandLineProvider),
            ExtensionVersion.DefaultSemVer,
            ExtensionResources.HangDumpExtensionDisplayName,
            ExtensionResources.HangDumpExtensionDescription,
            CachedCommandLineOptions)
    {
    }

    public override Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        if (commandOption.Name == HangDumpTimeoutOptionName && !TimeSpanParser.TryParse(arguments[0], out TimeSpan _))
        {
            return ValidationResult.InvalidTask(ExtensionResources.HangDumpTimeoutOptionInvalidArgument);
        }

        if (commandOption.Name == HangDumpTypeOptionName)
        {
            if (!HangDumpTypeOptions.Contains(arguments[0], StringComparer.OrdinalIgnoreCase))
            {
                return ValidationResult.InvalidTask(string.Format(
                    CultureInfo.InvariantCulture,
                    ExtensionResources.HangDumpTypeOptionInvalidType,
                    arguments[0],
                    HangDumpTypeOptionsFormatted));
            }
        }

        return ValidationResult.ValidTask;
    }

    public override Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => (commandLineOptions.IsOptionSet(HangDumpTimeoutOptionName) ||
            commandLineOptions.IsOptionSet(HangDumpFileNameOptionName) ||
            commandLineOptions.IsOptionSet(HangDumpTypeOptionName)) &&
            !commandLineOptions.IsOptionSet(HangDumpOptionName)
            ? ValidationResult.InvalidTask(ExtensionResources.MissingHangDumpMainOption)
            : ValidationResult.ValidTask;
}
