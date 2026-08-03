// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Discovers extension manifests next to the test application, loads the declared extension assemblies and
/// invokes their static <c>AddExtensions</c> hook — the same hook shape the MSBuild-generated
/// <c>SelfRegisteredExtensions</c> class calls for statically referenced extensions.
/// </summary>
/// <remarks>
/// See <see href="https://github.com/microsoft/testfx/blob/main/docs/RFCs/023-Dynamic-Extension-Loading.md"/>.
/// </remarks>
internal sealed class DynamicExtensionLoader
{
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironment _environment;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IDynamicExtensionAssemblyLoader _assemblyLoader;
    private readonly ILogger? _logger;

    public DynamicExtensionLoader(
        IFileSystem fileSystem,
        IEnvironment environment,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IDynamicExtensionAssemblyLoader assemblyLoader,
        ILogger? logger)
    {
        _fileSystem = fileSystem;
        _environment = environment;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _assemblyLoader = assemblyLoader;
        _logger = logger;
    }

    /// <summary>
    /// Registers every enabled extension declared by the manifests found next to the test application.
    /// </summary>
    /// <param name="builder">The builder handed to each extension hook.</param>
    /// <param name="args">The command line arguments handed to each extension hook.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadAsync(ITestApplicationBuilder builder, string[] args)
    {
        if (IsDisabledByEnvironment())
        {
            await LogDebugAsync($"Dynamic extension loading is disabled by '{EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS}'.").ConfigureAwait(false);
            return;
        }

        IReadOnlyList<string> manifestPaths = DiscoverManifests();
        if (manifestPaths.Count == 0)
        {
            return;
        }

        IReadOnlyList<DynamicExtensionEntry> entries = await ParseManifestsAsync(manifestPaths).ConfigureAwait(false);
        IReadOnlyList<DynamicExtensionEntry> entriesToLoad = await FilterEntriesAsync(entries).ConfigureAwait(false);
        if (entriesToLoad.Count == 0)
        {
            return;
        }

        foreach (DynamicExtensionEntry entry in entriesToLoad)
        {
            await LoadEntryAsync(builder, args, entry).ConfigureAwait(false);
        }
    }

    private bool IsDisabledByEnvironment()
    {
        string? value = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS);
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> DiscoverManifests()
    {
        string directory = _testApplicationModuleInfo.GetCurrentTestApplicationDirectory();
        if (RoslynString.IsNullOrEmpty(directory))
        {
            return [];
        }

        string[] candidates;
        try
        {
            candidates = _fileSystem.GetFiles(directory, DynamicExtensionConstants.ManifestSearchPattern, SearchOption.TopDirectoryOnly);
        }
        catch (DirectoryNotFoundException)
        {
            // The directory genuinely does not exist, so nothing was declared. Note this is deliberately not
            // pre-checked with Directory.Exists: that returns false for an unreadable directory too, which
            // would silently turn "cannot tell" into "nothing declared".
            return [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Continuing would mean the policy a manifest may encode did not apply and nobody noticed, which is
            // exactly the failure mode this feature exists to avoid: we cannot tell whether there was one.
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionManifestDirectoryNotReadableErrorMessage,
                    directory,
                    EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS),
                ex);
        }

        // Directory search patterns can match more than the literal suffix (a pattern whose extension is longer
        // than three characters also matches longer extensions on Windows), so re-filter explicitly.
        List<string> manifests = [];
        foreach (string candidate in candidates)
        {
            if (candidate.EndsWith(DynamicExtensionConstants.ManifestFileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                manifests.Add(candidate);
            }
        }

        // Deterministic, reproducible ordering: by file name, then by full path to break ties.
        manifests.Sort(static (left, right) =>
        {
            int result = string.CompareOrdinal(Path.GetFileName(left), Path.GetFileName(right));
            return result != 0 ? result : string.CompareOrdinal(left, right);
        });

        return manifests;
    }

    private async Task<IReadOnlyList<DynamicExtensionEntry>> ParseManifestsAsync(IReadOnlyList<string> manifestPaths)
    {
        List<DynamicExtensionEntry> entries = [];
        foreach (string manifestPath in manifestPaths)
        {
            await LogDebugAsync($"Reading extension manifest '{manifestPath}'.").ConfigureAwait(false);

            string content;
            try
            {
                content = await _fileSystem.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    string.Format(CultureInfo.InvariantCulture, PlatformResources.DynamicExtensionManifestReadFailedErrorMessage, manifestPath),
                    ex);
            }

            DynamicExtensionManifest manifest = DynamicExtensionManifestParser.Parse(manifestPath, content);
            foreach (string unknownProperty in manifest.UnknownProperties)
            {
                await LogDebugAsync($"Ignoring unknown property '{unknownProperty}' in extension manifest '{manifestPath}'.").ConfigureAwait(false);
            }

            entries.AddRange(manifest.Extensions);
        }

        return entries;
    }

    private async Task<IReadOnlyList<DynamicExtensionEntry>> FilterEntriesAsync(IReadOnlyList<DynamicExtensionEntry> entries)
    {
        List<DynamicExtensionEntry> result = [];
        Dictionary<string, DynamicExtensionEntry> seenIds = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (DynamicExtensionEntry entry in entries)
        {
            if (!entry.IsEnabled)
            {
                await LogDebugAsync($"Skipping disabled extension '{entry.DisplayName}' declared in '{entry.ManifestPath}'.").ConfigureAwait(false);
                continue;
            }

            if (seenIds.TryGetValue(entry.Id, out DynamicExtensionEntry? previous))
            {
                // The same extension declared twice is the expected case and is silently de-duplicated. Two
                // *different* extensions sharing an id is not: honouring only the first would silently drop a
                // policy someone deliberately deployed, so it has to be reported. This mirrors how the MSBuild
                // task rejects TestingPlatformBuilderHook items whose metadata conflicts.
                if (!IsSameDeclaration(previous, entry))
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.InvariantCulture,
                        PlatformResources.DynamicExtensionDuplicateIdConflictErrorMessage,
                        entry.Id,
                        Describe(previous),
                        previous.ManifestPath,
                        Describe(entry),
                        entry.ManifestPath));
                }

                await LogDebugAsync($"Skipping extension '{entry.DisplayName}' declared in '{entry.ManifestPath}' because id '{entry.Id}' was already registered from '{previous.ManifestPath}'.").ConfigureAwait(false);
                continue;
            }

            seenIds.Add(entry.Id, entry);
            result.Add(entry);
        }

        return result;
    }

    private static bool IsSameDeclaration(DynamicExtensionEntry left, DynamicExtensionEntry right)
        => string.Equals(left.ResolvedAssemblyPath, right.ResolvedAssemblyPath, DynamicExtensionConstants.PathComparison)
            && string.Equals(left.TypeFullName, right.TypeFullName, StringComparison.Ordinal);

    private static string Describe(DynamicExtensionEntry entry)
        => $"{entry.TypeFullName} ({entry.ResolvedAssemblyPath})";

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' may break when trimming", Justification = "The hook type lives in the extension assembly, which is an external file that is not part of the trimmed application, so trimming cannot remove it.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method", Justification = "The hook type lives in the extension assembly, which is an external file that is not part of the trimmed application, so its AddExtensions method cannot be trimmed away.")]
    private async Task LoadEntryAsync(ITestApplicationBuilder builder, string[] args, DynamicExtensionEntry entry)
    {
        if (!_fileSystem.ExistFile(entry.ResolvedAssemblyPath))
        {
            throw new FileNotFoundException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionAssemblyNotFoundErrorMessage,
                    entry.ResolvedAssemblyPath,
                    entry.DisplayName,
                    entry.ManifestPath),
                entry.ResolvedAssemblyPath);
        }

        await LogDebugAsync($"Loading extension '{entry.DisplayName}' from '{entry.ResolvedAssemblyPath}' (declared in '{entry.ManifestPath}', isolated: {_assemblyLoader.IsIsolated}).").ConfigureAwait(false);

        Assembly assembly;
        try
        {
            assembly = _assemblyLoader.LoadAssembly(entry.ResolvedAssemblyPath);
        }
        catch (PlatformNotSupportedException ex)
        {
            // The runtime genuinely cannot load an assembly from disk (native AOT). This is deliberately
            // detected by attempting the load rather than by pre-checking RuntimeFeature.IsDynamicCodeSupported:
            // that switch is also turned off by <PublishAot>true</PublishAot> on builds whose managed output
            // still runs normally (see PublishAotNonNativeTests), and those can load extensions perfectly well.
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionsNotSupportedOnCurrentRuntimeErrorMessage,
                    entry.ManifestPath,
                    DynamicExtensionConstants.EnabledPropertyName,
                    EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS),
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionAssemblyLoadFailedErrorMessage,
                    entry.ResolvedAssemblyPath,
                    entry.DisplayName,
                    entry.ManifestPath),
                ex);
        }

        Type? hookType;
        try
        {
            hookType = assembly.GetType(entry.TypeFullName, throwOnError: false, ignoreCase: false);
        }
        catch (Exception ex)
        {
            // GetType can still throw even with throwOnError: false, for example when resolving the type
            // requires an assembly the extension did not deploy.
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionTypeNotFoundErrorMessage,
                    entry.TypeFullName,
                    entry.ResolvedAssemblyPath,
                    entry.ManifestPath),
                ex);
        }

        if (hookType is null)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionTypeNotFoundErrorMessage,
                entry.TypeFullName,
                entry.ResolvedAssemblyPath,
                entry.ManifestPath));
        }

        MethodInfo? hook;
        try
        {
            hook = hookType.GetMethod(
                DynamicExtensionConstants.HookMethodName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
                binder: null,
                types: [typeof(ITestApplicationBuilder), typeof(string[])],
                modifiers: null);
        }
        catch (Exception ex)
        {
            // Building the method table can throw when a member signature references an assembly the extension
            // did not deploy. Without this the failure would escape naming neither the manifest nor the
            // extension.
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionHookNotFoundErrorMessage,
                    entry.TypeFullName,
                    entry.ResolvedAssemblyPath,
                    entry.ManifestPath,
                    DynamicExtensionConstants.HookMethodName),
                ex);
        }

        if (hook is null)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionHookNotFoundErrorMessage,
                entry.TypeFullName,
                entry.ResolvedAssemblyPath,
                entry.ManifestPath,
                DynamicExtensionConstants.HookMethodName));
        }

        if (hook.ReturnType != typeof(void))
        {
            // The hook is invoked synchronously, so a returned Task would never be awaited: registrations made
            // after the first await would race with BuildAsync and any failure would be swallowed. Rejecting it
            // up front is the only way an author finds out.
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionHookMustReturnVoidErrorMessage,
                entry.TypeFullName,
                DynamicExtensionConstants.HookMethodName,
                entry.ResolvedAssemblyPath,
                entry.ManifestPath,
                hook.ReturnType.FullName ?? hook.ReturnType.Name));
        }

        if (hook.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false))
        {
            // 'async void' satisfies the return-type check above but behaves exactly like the Task-returning
            // hook it rejects: Invoke returns at the first await, the registration guard is disposed, whatever
            // the hook does afterwards races the application's own setup, and an exception past that point is
            // never seen by the try/catch below. The compiler marks every async method with this attribute, so
            // it is the one reliable way to tell the two apart through reflection.
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                PlatformResources.DynamicExtensionHookMustNotBeAsyncVoidErrorMessage,
                entry.TypeFullName,
                DynamicExtensionConstants.HookMethodName,
                entry.ResolvedAssemblyPath,
                entry.ManifestPath));
        }

        try
        {
            // The hook gets the *real* builder, not a wrapper: shipped helpers such as
            // AddOpenTelemetryProvider and AddRunSettingsService reach through ITestApplicationBuilder to the
            // concrete builder, and would throw or silently no-op against anything else. The framework
            // registration guard therefore lives inside the builder for the duration of the call.
            using IDisposable? guard = (builder as IDynamicExtensionRegistrationGuard)?.EnterDynamicExtensionScope(entry.DisplayName, entry.ManifestPath);
            hook.Invoke(obj: null, parameters: [builder, args]);
        }
        catch (Exception ex)
        {
            // TargetInvocationException carries the extension's own failure; anything else (an open generic
            // hook type, a missing dependency of the hook, ...) is a problem with the declaration itself. Both
            // must name the manifest so the failure can be traced back to the file that has to be fixed.
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.DynamicExtensionHookFailedErrorMessage,
                    entry.TypeFullName,
                    DynamicExtensionConstants.HookMethodName,
                    entry.ResolvedAssemblyPath,
                    entry.ManifestPath),
                (ex as TargetInvocationException)?.InnerException ?? ex);
        }

        await LogDebugAsync($"Registered extension '{entry.DisplayName}' ('{entry.TypeFullName}') from '{entry.ResolvedAssemblyPath}'.").ConfigureAwait(false);
    }

    private Task LogDebugAsync(string message)
        => _logger?.LogDebugAsync(message) ?? Task.CompletedTask;
}
