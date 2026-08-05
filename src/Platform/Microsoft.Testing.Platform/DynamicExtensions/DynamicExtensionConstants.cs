// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Well-known names used by the dynamic extension loading feature. See
/// <see href="https://github.com/microsoft/testfx/blob/main/docs/RFCs/023-Dynamic-Extension-Loading.md"/>.
/// </summary>
internal static class DynamicExtensionConstants
{
    /// <summary>
    /// Suffix identifying an extension manifest file.
    /// </summary>
    public const string ManifestFileSuffix = ".testingplatformextensions.json";

    /// <summary>
    /// Search pattern used to enumerate manifests in the test application directory.
    /// </summary>
    public const string ManifestSearchPattern = "*" + ManifestFileSuffix;

    /// <summary>
    /// Name of the static hook method a dynamically loaded extension must expose. Deliberately the same
    /// method name and signature as the MSBuild <c>TestingPlatformBuilderHook</c> contract, so that a single
    /// hook works for both static and dynamic registration.
    /// </summary>
    public const string HookMethodName = "AddExtensions";

    /// <summary>
    /// Simple name of the core platform assembly.
    /// </summary>
    public const string SharedPlatformAssemblyName = "Microsoft.Testing.Platform";

    /// <summary>
    /// Name of the manifest property holding the array of extension declarations.
    /// </summary>
    public const string ExtensionsPropertyName = "extensions";

    /// <summary>
    /// Name of the optional manifest entry property holding the stable de-duplication identifier.
    /// </summary>
    public const string IdPropertyName = "id";

    /// <summary>
    /// Name of the optional manifest entry property holding the human-readable name used in diagnostics.
    /// </summary>
    public const string DisplayNamePropertyName = "displayName";

    /// <summary>
    /// Name of the manifest entry property holding the path to the extension assembly.
    /// </summary>
    public const string AssemblyPathPropertyName = "assemblyPath";

    /// <summary>
    /// Name of the manifest entry property holding the full name of the type declaring the hook.
    /// </summary>
    public const string TypeFullNamePropertyName = "typeFullName";

    /// <summary>
    /// Name of the optional manifest entry property controlling whether the extension is loaded.
    /// </summary>
    public const string EnabledPropertyName = "enabled";

    /// <summary>
    /// How two file paths are compared. Windows paths are case-insensitive, other platforms' are not, and using
    /// a single comparison everywhere keeps de-duplication and the load-context cache in agreement.
    /// </summary>
    /// <remarks>
    /// This keys off the OS rather than the actual volume, which is an approximation: a default macOS volume is
    /// case-insensitive, and a Linux or macOS volume can be either. The approximation is deliberately biased
    /// toward the visible failure. Treating a case-insensitive volume as case-sensitive means two manifests that
    /// spell the same file differently register it twice — which the run then lists on its output, so it can be
    /// seen and fixed. The opposite bias would silently drop one of two genuinely distinct extensions, which is
    /// the failure mode this design exists to avoid. Resolving it properly needs canonical file identity rather
    /// than a path comparison, which is not worth a syscall per resolved assembly here.
    /// </remarks>
    public static readonly StringComparison PathComparison =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Comparer matching <see cref="PathComparison"/>.
    /// </summary>
    public static readonly StringComparer PathComparer =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Simple names of the assemblies that are always resolved from the default load context, so that the
    /// platform contract types have a single identity shared between the host and every dynamically loaded
    /// extension.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Microsoft.Testing.Platform</c> is the core contract: without it the <c>ITestApplicationBuilder</c>
    /// an extension sees would be a different type from the one the platform passes to it.
    /// </para>
    /// <para>
    /// The remaining entries are the first-party <c>*.Abstractions</c> assemblies whose types are *exchanged*
    /// between extensions — for example <c>ITrxReportCapability</c>, which one extension implements and
    /// another queries for. Sharing them by name rather than relying on whether the file happens to sit next
    /// to the extension keeps identity deterministic instead of dependent on the deployment layout.
    /// </para>
    /// <para>
    /// Anything not listed here is isolated. Add to this list only assemblies that carry types crossing the
    /// host/extension boundary; adding an implementation assembly would reintroduce the dependency conflicts
    /// this design exists to avoid.
    /// </para>
    /// </remarks>
    public static readonly string[] SharedContractAssemblyNames =
    [
        SharedPlatformAssemblyName,
        "Microsoft.Testing.Extensions.TrxReport.Abstractions",
    ];
}
