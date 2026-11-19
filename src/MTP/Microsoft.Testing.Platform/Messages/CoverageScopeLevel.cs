// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.Messages;

/// <summary>The entity a coverage / threshold entry describes. Append-only.</summary>
public enum CoverageScopeLevel
{
    /// <summary>The whole run.</summary>
    Overall = 0,

    /// <summary>A module (assembly file on disk).</summary>
    Module = 1,

    /// <summary>An assembly.</summary>
    Assembly = 2,

    /// <summary>A namespace.</summary>
    Namespace = 3,

    /// <summary>A type.</summary>
    Type = 4,

    /// <summary>A source file.</summary>
    File = 5,
}
