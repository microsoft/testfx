// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

public sealed class ProcessHandleInfo
{
    public string? ProcessName { get; internal set; }

    public int Id { get; internal set; }
}
