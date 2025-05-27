// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.CommandLine;

internal struct ToolCommandLineOptionsProviderCache(IToolCommandLineOptionsProvider commandLineOptionsProvider) : IToolCommandLineOptionsProvider
{
    private readonly IToolCommandLineOptionsProvider _commandLineOptionsProvider = commandLineOptionsProvider;
    private IReadOnlyCollection<CommandLineOption>? _commandLineOptions;
    private readonly ConcurrentDictionary<string, Task<ValidationResult>> _validationCache = new();

    public readonly string Uid => _commandLineOptionsProvider.Uid;

    public readonly string Version => _commandLineOptionsProvider.Version;

    public readonly string DisplayName => _commandLineOptionsProvider.DisplayName;

    public readonly string Description => _commandLineOptionsProvider.Description;

    public readonly string ToolName => _commandLineOptionsProvider.ToolName;

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions()
    {
        _commandLineOptions ??= _commandLineOptionsProvider.GetCommandLineOptions();

        return _commandLineOptions;
    }

    public readonly Task<bool> IsEnabledAsync()
        => _commandLineOptionsProvider.IsEnabledAsync();

    public readonly Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
        => _commandLineOptionsProvider.ValidateCommandLineOptionsAsync(commandLineOptions);

    public Task<ValidationResult> ValidateOptionArgumentsAsync(CommandLineOption commandOption, string[] arguments)
    {
        // Create a cache key from the option name and arguments
        string key = GenerateCacheKey(commandOption, arguments);
        
        // Return the cached result if available, otherwise compute and cache it
        return _validationCache.GetOrAdd(key, _ => _commandLineOptionsProvider.ValidateOptionArgumentsAsync(commandOption, arguments));
    }

    private static string GenerateCacheKey(CommandLineOption commandOption, string[] arguments)
    {
        // Combine the option name with all arguments to form a unique key
        return string.Join("|", new[] { commandOption.Name }.Concat(arguments));
    }
}
