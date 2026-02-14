// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.Diagnostics.Helpers;

internal sealed class ProcessTreeNode
{
    public IProcess? Process { get; set; }

    public int Level { get; set; }

    public int ParentId { get; set; }

    public Process? ParentProcess { get; set; }
}
