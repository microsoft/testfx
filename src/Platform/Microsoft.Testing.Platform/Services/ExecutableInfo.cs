// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal sealed class ExecutableInfo(string filePath, IEnumerable<string> arguments)
{
    public string FilePath { get; } = filePath;

    public IEnumerable<string> Arguments { get; } = arguments;

    public override string ToString()
        => $"Process: {FilePath}, Arguments: {string.Join(' ', Arguments)}";
}
