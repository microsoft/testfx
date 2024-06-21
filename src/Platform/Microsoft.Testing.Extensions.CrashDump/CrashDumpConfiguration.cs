// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Diagnostics;

internal sealed class CrashDumpConfiguration
{
    public string? DumpFileNamePattern { get; set; }

    public bool Enable { get; set; } = true;
}
