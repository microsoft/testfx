// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Configurations;

namespace Microsoft.Testing.Framework;

public interface ITestSessionContext
{
    CancellationToken CancellationToken { get; }

    IConfiguration Configuration { get; }

    Task AddTestAttachmentAsync(FileInfo file, string displayName, string? description = null);
}
