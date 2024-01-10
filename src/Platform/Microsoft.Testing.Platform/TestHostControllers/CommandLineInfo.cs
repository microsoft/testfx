// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.TestHostControllers;

internal sealed class CommandLineInfo(string fileName, IReadOnlyCollection<string> arguments, string testApplicationFullPath)
{
    public string FileName { get; } = fileName;

    public IReadOnlyCollection<string> Arguments { get; } = arguments;

    public string TestApplicationFullPath { get; } = testApplicationFullPath;
}
