// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions;

/// <summary>
/// Represents an interface for asynchronously initializing an extension.
/// </summary>
public interface IAsyncInitializableExtension
{
    /// <summary>
    /// Asynchronously initializes the extension.
    /// </summary>
    Task InitializeAsync();
}

internal static class InitializableExtension
{
    public static async Task TryInitialize(this object target)
    {
        if (target is IAsyncInitializableExtension initializable)
        {
            await initializable.InitializeAsync();
        }
    }
}
