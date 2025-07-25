// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Framework.Extensions;

internal sealed class DebuggerLaunchCommandLineProvider : ICommandLineOptionsProvider
{
    public const string DebuggerLaunchOnFailureOptionName = "debug-on-failure";

    private static readonly IReadOnlyCollection<CommandLineOption> CachedCommandLineOptions =
    [
        new(DebuggerLaunchOnFailureOptionName, "Launch debugger when test assertion fails", ArgumentArity.Zero, false)
    ];

    public string Uid => nameof(DebuggerLaunchCommandLineProvider);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => "Debugger Launch Extension";

    public string Description => "Enables launching debugger on test assertion failures";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() => CachedCommandLineOptions;

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
        => ValidationResult.ValidTask;

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => ValidationResult.ValidTask;
}