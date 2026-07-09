// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Services;

/// <summary>
/// Provides file-name templating for artifacts produced by the test platform and its extensions.
/// </summary>
/// <remarks>
/// The service expands the standard placeholders and sanitizes the resulting leaf file name so it is
/// valid on the current operating system. It is registered as a common service and can be resolved
/// through <see cref="ServiceProviderExtensions.GetArtifactNamingService(System.IServiceProvider)"/>.
/// </remarks>
public interface IArtifactNamingService
{
    /// <summary>
    /// Resolves a file-name template into a concrete file name.
    /// </summary>
    /// <param name="template">
    /// The template to resolve. The following placeholders are expanded:
    /// <list type="bullet">
    /// <item><description><c>{pname}</c> — the current test application process name.</description></item>
    /// <item><description><c>{pid}</c> — the current process id.</description></item>
    /// <item><description><c>{asm}</c> — the entry assembly name.</description></item>
    /// <item><description><c>{tfm}</c> — the short target framework moniker (including platform).</description></item>
    /// <item><description><c>{arch}</c> — the process architecture.</description></item>
    /// <item><description><c>{time}</c> — the current UTC timestamp.</description></item>
    /// </list>
    /// Unknown placeholders are preserved as-is. Any directory portion of the template is preserved
    /// while only the leaf file name is sanitized.
    /// </param>
    /// <returns>The resolved file name, with the leaf sanitized to be valid for the current file system.</returns>
    string ResolveFileName(string template);
}
