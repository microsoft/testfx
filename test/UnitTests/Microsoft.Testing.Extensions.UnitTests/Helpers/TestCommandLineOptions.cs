// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

internal sealed class TestCommandLineOptions : ICommandLineOptions, ICommandLineOptionsWithDefaults
{
    private readonly Dictionary<string, string[]> _options;
    private readonly Dictionary<string, string[]>? _defaults;

    public TestCommandLineOptions(Dictionary<string, string[]> options, Dictionary<string, string[]>? defaults = null)
    {
        _options = options;
        _defaults = defaults;
    }

    public bool IsOptionSet(string optionName) => _options.ContainsKey(optionName);

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments) => _options.TryGetValue(optionName, out arguments);

    bool ICommandLineOptionsWithDefaults.TryGetOptionArgumentListOrDefault(string optionName, [NotNullWhen(true)] out string[]? arguments)
        => _options.TryGetValue(optionName, out arguments)
            || (_defaults is not null && _defaults.TryGetValue(optionName, out arguments));
}
