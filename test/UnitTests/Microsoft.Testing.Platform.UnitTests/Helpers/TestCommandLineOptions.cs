// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.UnitTests.Helpers;

internal class TestCommandLineOptions : ICommandLineOptions
{
    private readonly Dictionary<string, string[]> _options;

    public TestCommandLineOptions(Dictionary<string, string[]> options) => _options = options;

    public bool IsOptionSet(string optionName) => _options.ContainsKey(optionName);

    public bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments) => _options.TryGetValue(optionName, out arguments);
}
