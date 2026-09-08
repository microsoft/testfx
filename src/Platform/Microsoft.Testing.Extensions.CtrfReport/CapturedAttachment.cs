// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CapturedAttachment
{
    public required string Name { get; init; }

    public required string ContentType { get; init; }

    public required string Path { get; init; }

    public string? Description { get; init; }
}
