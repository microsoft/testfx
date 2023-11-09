// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

internal sealed class ExecutableInfo(string fileName, string[] arguments, string workspace)
{
    public string FileName { get; } = fileName;

    public string[] Arguments { get; } = arguments;

    public string Workspace { get; } = workspace;
}
