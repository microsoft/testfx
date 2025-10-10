// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal sealed class CommandLineArgumentsProvider(string[] originalArgs) : ICommandLineArgumentsProvider
{
    private readonly string[] _originalArgs = originalArgs;

    public string[] GetOriginalCommandLineArguments() => _originalArgs;
}