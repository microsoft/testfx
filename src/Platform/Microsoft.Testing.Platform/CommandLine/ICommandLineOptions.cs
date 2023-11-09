// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Testing.Platform.CommandLine;

public interface ICommandLineOptions
{
    bool IsOptionSet(string optionName);

    bool TryGetOptionArgumentList(string optionName, [NotNullWhen(true)] out string[]? arguments);
}
