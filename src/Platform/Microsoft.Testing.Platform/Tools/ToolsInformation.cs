// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Tools;

internal sealed class ToolsInformation(ITool[] tools)
{
    public bool HasTools => Tools.Length > 0;

    public ITool[] Tools { get; } = tools;
}
