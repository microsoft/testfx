// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Central policy describing which adapter features are enabled for the application model this build targets.
/// </summary>
/// <remarks>
/// <para>
/// Classic UWP (<c>uap10.0</c>, <c>WINDOWS_UWP</c>) consumes the <c>netstandard2.0</c> test framework and lacks
/// several APIs (for example <c>DeploymentItemAttribute</c>), so code depending on those APIs keeps compile-time guards.
/// </para>
/// <para>
/// Modern UWP and WinUI share a full .NET build (<c>WIN_UI</c>). There the features below are available at compile
/// time and are disabled by policy instead, so call sites can query this class rather than adding new
/// <c>#if WIN_UI</c> blocks. Selection stays compile-time: a runtime "is packaged" check is not equivalent because a
/// packaged desktop process is not necessarily sandboxed, while the WinUI build only ever runs inside an application.
/// </para>
/// </remarks>
internal static class AppModelCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the adapter is built for a UI application model (classic UWP, modern UWP or WinUI).
    /// </summary>
    public static bool IsUIAppModel =>
#if WINDOWS_UWP || WIN_UI
        true;
#else
        false;
#endif

    /// <summary>
    /// Gets a value indicating whether <c>DeploymentItemAttribute</c> deployment is supported. UI application models
    /// must ship their files inside the application package instead.
    /// </summary>
    public static bool SupportsDeployment => !IsUIAppModel;

    /// <summary>
    /// Gets a value indicating whether MSTest telemetry is collected and sent.
    /// </summary>
    public static bool SupportsTelemetry => !IsUIAppModel;

    /// <summary>
    /// Gets a value indicating whether the <c>CaptureAssertionFailureDiagnostics</c> setting is honored.
    /// </summary>
    public static bool SupportsAssertionFailureDiagnostics => !IsUIAppModel;

    /// <summary>
    /// Gets a value indicating whether the Microsoft.Testing.Platform discovery cache may be used.
    /// </summary>
    public static bool SupportsDiscoveryCache => !IsUIAppModel;
}
