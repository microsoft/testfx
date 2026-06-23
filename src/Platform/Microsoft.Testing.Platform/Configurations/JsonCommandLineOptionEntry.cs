// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Configurations;

/// <summary>
/// A single command-line option entry materialized from the <c>commandLineOptions</c> section of a
/// loaded testconfig.json file by <see cref="JsonConfigurationSource.JsonConfigurationProvider.EnumerateCommandLineOptions"/>.
/// </summary>
internal sealed class JsonCommandLineOptionEntry
{
    public JsonCommandLineOptionEntry(string optionName, IReadOnlyList<string> arguments, bool isDisabled)
    {
        OptionName = optionName;
        Arguments = arguments;
        IsDisabled = isDisabled;
    }

    /// <summary>
    /// Gets the option name as written in the JSON file. The name is the raw key without any
    /// leading <c>--</c> prefix because the JSON section uses the unprefixed form by convention.
    /// </summary>
    public string OptionName { get; }

    /// <summary>
    /// Gets the parsed argument list. Empty when the JSON value was a boolean (true/false) or a
    /// presence marker.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets a value indicating whether the entry was an explicit boolean <c>false</c> in the JSON
    /// file. The validator should skip arity/argument checks for disabled entries because they have
    /// no semantic arguments to validate.
    /// </summary>
    public bool IsDisabled { get; }
}
