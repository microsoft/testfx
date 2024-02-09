﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Represents an interface for asynchronously cleaning up resources.
/// </summary>
public interface IAsyncCleanableExtension
{
    /// <summary>
    /// Asynchronously cleans up the resources.
    /// </summary>
    Task CleanupAsync();
}
