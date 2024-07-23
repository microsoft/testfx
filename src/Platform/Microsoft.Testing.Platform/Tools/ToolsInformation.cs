// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Tools;

internal sealed class ToolsInformation(IReadOnlyList<ITool> tools)
{
    public bool HasTools => Tools.Count > 0;

    public IReadOnlyList<ITool> Tools { get; } = tools;
}
