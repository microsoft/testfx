// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.UnitTests.Helpers;

internal class TestCommandLineOptions : ICommandLineOptions, ICommandLineOptionsWithDefaults
{
    private readonly Dictionary<string, string[]> _options;
    private readonly Dictionary<string, string[]> _defaults;

    public TestCommandLineOptions(Dictionary<string, string[]> options)
        : this(options, [])
    {
    }

    public TestCommandLineOptions(Dictionary<string, string[]> options, Dictionary<string, string[]> defaults)
    {
        _options = options;
        _defaults = defaults;
    }

    public bool IsOptionSet(string optionName) => _options.ContainsKey(optionName);

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments) => _options.TryGetValue(optionName, out arguments);

    bool ICommandLineOptionsWithDefaults.TryGetOptionArgumentListOrDefault(string optionName, [NotNullWhen(true)] out string[]? arguments)
        => _options.TryGetValue(optionName, out arguments) || _defaults.TryGetValue(optionName, out arguments);
}
