// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Implemented by the real test application builder so that a dynamically loaded extension's hook can be given
/// the genuine builder while still being prevented from registering a test framework.
/// </summary>
/// <remarks>
/// <para>
/// Dynamic extensions run before the test application registers its own framework, so without a guard an
/// extension declared in a manifest could claim the framework slot and silently decide which tests run and what
/// results they report.
/// </para>
/// <para>
/// The guard deliberately lives on the builder rather than in a wrapper implementing
/// <c>ITestApplicationBuilder</c>. Several shipped helpers — <c>AddOpenTelemetryProvider</c>,
/// <c>AddRunSettingsService</c>, MSTest's <c>AddMSTest</c> — reach through the interface to the concrete
/// builder, so handing a hook anything other than the real instance would make those helpers throw or, worse,
/// silently do nothing.
/// </para>
/// </remarks>
internal interface IDynamicExtensionRegistrationGuard
{
    /// <summary>
    /// Marks the calling scope as running a dynamically loaded extension's hook. While the returned scope is
    /// alive, registering a test framework fails with an error naming the extension and its manifest.
    /// </summary>
    /// <param name="displayName">Display name of the extension whose hook is about to run.</param>
    /// <param name="manifestPath">Full path of the manifest that declared the extension.</param>
    /// <returns>A scope that must be disposed once the hook has returned.</returns>
    IDisposable EnterDynamicExtensionScope(string displayName, string manifestPath);
}
