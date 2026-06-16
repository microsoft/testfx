// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || (NET && !WINDOWS_UWP)

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal partial class AssemblyResolver
{
#if NETFRAMEWORK
    /// <summary>
    /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
    /// </summary>
    /// <param name="sender"> The sender App Domain. </param>
    /// <param name="args"> The args. </param>
    /// <returns> The <see cref="Assembly"/>. </returns>
    internal Assembly? ReflectionOnlyOnResolve(object sender, ResolveEventArgs args)
        => OnResolveInternal(sender, args, true);
#endif

    /// <summary>
    /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
    /// </summary>
    /// <param name="sender"> The sender App Domain. </param>
    /// <param name="args"> The args. </param>
    /// <returns> The <see cref="Assembly"/>.  </returns>
    internal Assembly? OnResolve(object? sender, ResolveEventArgs args)
        => OnResolveInternal(sender, args, false);

    /// <summary>
    /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
    /// </summary>
    /// <param name="senderAppDomain"> The sender App Domain. </param>
    /// <param name="args"> The args. </param>
    /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
    /// <returns> The <see cref="Assembly"/>.  </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "senderAppDomain", Justification = "This is an event handler.")]
#pragma warning disable IDE0060 // Remove unused parameter
    private Assembly? OnResolveInternal(object? senderAppDomain, ResolveEventArgs args, bool isReflectionOnly)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (StringEx.IsNullOrEmpty(args.Name))
        {
            Debug.Fail("MSTest.AssemblyResolver.OnResolve: args.Name is null or empty.");
            return null;
        }

        SafeLog(
            args.Name,
            () =>
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTest.AssemblyResolver.OnResolve: Resolving assembly '{0}'", args.Name);
                }
            });

        string assemblyNameToLoad = AppDomain.CurrentDomain.ApplyPolicy(args.Name);

        SafeLog(
            assemblyNameToLoad,
            () =>
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTest.AssemblyResolver.OnResolve: Resolving assembly after applying policy '{0}'", assemblyNameToLoad);
                }
            });

        lock (_syncLock)
        {
            // Since both normal and reflection only cache are accessed in same block, putting only one lock should be sufficient.
            if (TryLoadFromCache(assemblyNameToLoad, isReflectionOnly, out Assembly? assembly))
            {
                return assembly;
            }

            assembly = SearchAssembly(_searchDirectories, assemblyNameToLoad, isReflectionOnly);
            if (assembly != null)
            {
                return assembly;
            }

            // required assembly is not present in searchDirectories??
            // see, if we can find it in user specified search directories.
            while (assembly == null && _directoryList?.Count > 0)
            {
                // instead of loading whole search directory in one time, we are adding directory on the basis of need
                RecursiveDirectoryPath currentNode = _directoryList.Dequeue();

                List<string> incrementalSearchDirectory = [];

                if (DoesDirectoryExist(currentNode.DirectoryPath))
                {
                    incrementalSearchDirectory.Add(currentNode.DirectoryPath);

                    if (currentNode.IncludeSubDirectories)
                    {
                        // Add all its sub-directory in depth first search order.
                        AddSubdirectories(currentNode.DirectoryPath, incrementalSearchDirectory);
                    }

                    // Add this directory list in this.searchDirectories so that when we will try to resolve some other
                    // assembly, then it will look in this whole directory first.
                    _searchDirectories.AddRange(incrementalSearchDirectory);

                    assembly = SearchAssembly(incrementalSearchDirectory, assemblyNameToLoad, isReflectionOnly);
                }
                else
                {
                    // generate warning that path does not exist.
                    SafeLog(
                        assemblyNameToLoad,
                        () =>
                        {
                            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsWarningEnabled)
                            {
                                PlatformServiceProvider.Instance.AdapterTraceLogger.Warning(
                                "MSTest.AssemblyResolver.OnResolve: the directory '{0}', does not exist",
                                currentNode.DirectoryPath);
                            }
                        });
                }
            }

            if (assembly != null)
            {
                return assembly;
            }

            // Try for default load for System dlls that can't be found in search paths. Needs to loaded just by name.
            try
            {
#if NETFRAMEWORK
                if (isReflectionOnly)
                {
                    // Put it in the resolved assembly cache so that if the Load call below
                    // triggers another assembly resolution, then we don't end up in stack overflow.
                    _reflectionOnlyResolvedAssemblies[assemblyNameToLoad] = null;

                    assembly = Assembly.ReflectionOnlyLoad(assemblyNameToLoad);

                    _reflectionOnlyResolvedAssemblies[assemblyNameToLoad] = assembly;

                    return assembly;
                }
#endif

                // Put it in the resolved assembly cache so that if the Load call below
                // triggers another assembly resolution, then we don't end up in stack overflow.
                _resolvedAssemblies[assemblyNameToLoad] = null;

                assembly = Assembly.Load(assemblyNameToLoad);

                _resolvedAssemblies[assemblyNameToLoad] = assembly;

                return assembly;
            }
            catch (Exception ex)
            {
                SafeLog(
                    args.Name,
                    () =>
                    {
                        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                        {
                            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason: {1}", assemblyNameToLoad, ex);
                        }
                    });
            }

            return assembly;
        }
    }

    /// <summary>
    /// Load assembly from cache if available.
    /// </summary>
    /// <param name="assemblyName"> The assembly Name. </param>
    /// <param name="isReflectionOnly">Indicates if this is a reflection-only context.</param>
    /// <param name="assembly"> The assembly. </param>
    /// <returns> The <see cref="bool"/>. </returns>
    private bool TryLoadFromCache(string assemblyName, bool isReflectionOnly, out Assembly? assembly)
    {
        bool isFoundInCache = isReflectionOnly
            ? _reflectionOnlyResolvedAssemblies.TryGetValue(assemblyName, out assembly)
            : _resolvedAssemblies.TryGetValue(assemblyName, out assembly);
        if (isFoundInCache)
        {
            SafeLog(
                assemblyName,
                () =>
                {
                    if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTest.AssemblyResolver.OnResolve: Resolved '{0}'", assemblyName);
                    }
                });
            return true;
        }

        return false;
    }

    /// <summary>
    /// Call logger APIs safely. We do not want a stackoverflow when objectmodel assembly itself
    /// is being resolved and an PlatformServiceProvider.Instance.AdapterTraceLogger message prompts the load of the same dll again.
    /// CLR does not trigger a load when the PlatformServiceProvider.Instance.AdapterTraceLogger messages are in a lambda expression. Leaving it that way
    /// to preserve readability instead of creating wrapper functions.
    /// </summary>
    /// <param name="assemblyName">The assembly being resolved.</param>
    /// <param name="loggerAction">The logger function.</param>
    private static void SafeLog(string? assemblyName, Action loggerAction)
    {
        // Logger assembly was in `Microsoft.VisualStudio.TestPlatform.ObjectModel` assembly in legacy versions and we need to omit it as well.
        if (!StringEx.IsNullOrEmpty(assemblyName)
            && !assemblyName.StartsWith(LoggerAssemblyName, StringComparison.Ordinal)
            && !assemblyName.StartsWith(LoggerAssemblyNameLegacy, StringComparison.Ordinal)
            && !assemblyName.StartsWith(PlatformServicesResourcesName, StringComparison.Ordinal))
        {
            loggerAction.Invoke();
        }
    }
}
#endif
