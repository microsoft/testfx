// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || (NET && !WINDOWS_UWP)

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal partial class AssemblyResolver
{
    /// <summary>
    /// Adds the subdirectories of the provided path to the collection.
    /// </summary>
    /// <param name="path"> Path to get subdirectories for. </param>
    /// <param name="searchDirectories"> The search Directories. </param>
    internal
#if NET
    static
#endif
    void AddSubdirectories(string path, List<string> searchDirectories)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(path), "'path' cannot be null or empty.");
        DebugEx.Assert(searchDirectories != null, "'searchDirectories' cannot be null.");

        // If the directory exists, get its subdirectories
        if (DoesDirectoryExist(path))
        {
            // Get the directories in the path provided.
            string[] directories = GetDirectories(path);

            // Add each directory and its subdirectories to the collection.
            foreach (string directory in directories)
            {
                searchDirectories.Add(directory);

                AddSubdirectories(directory, searchDirectories);
            }
        }
    }

    /// <summary>
    /// It will search for a particular assembly in the given list of directory.
    /// </summary>
    /// <param name="searchDirectorypaths"> The search directory paths. </param>
    /// <param name="name"> The name. </param>
    /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
    /// <returns> The <see cref="Assembly"/>. </returns>
#if NETFRAMEWORK
    protected virtual
#else
    private
#endif
    Assembly? SearchAssembly(List<string> searchDirectorypaths, string name, bool isReflectionOnly)
    {
        if (searchDirectorypaths.Count == 0)
        {
            return null;
        }

        // args.Name is like: "Microsoft.VisualStudio.TestTools.Common, Version=[VersionMajor].0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a".
        AssemblyName? requestedName;

        try
        {
            // Can throw ArgumentException, FileLoadException if arg is empty/wrong format, etc. Should not return null.
            requestedName = new AssemblyName(name);
        }
        catch (Exception ex)
        {
            SafeLog(
                name,
                () =>
                {
                    if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Info(
                            "MSTest.AssemblyResolver.OnResolve: Failed to create assemblyName '{0}'. Reason: {1} ",
                            name,
                            ex);
                    }
                });

            return null;
        }

        DebugEx.Assert(!StringEx.IsNullOrEmpty(requestedName.Name), "MSTest.AssemblyResolver.OnResolve: requested name is empty!");

        foreach (string dir in searchDirectorypaths)
        {
            if (StringEx.IsNullOrEmpty(dir))
            {
                continue;
            }

            SafeLog(
                name,
                () =>
                {
                    if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsVerboseEnabled)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.Verbose("MSTest.AssemblyResolver.OnResolve: Searching assembly '{0}' in the directory '{1}'", requestedName.Name, dir);
                    }
                });

            foreach (string extension in new string[] { ".dll", ".exe" })
            {
                string assemblyPath = Path.Combine(dir, requestedName.Name + extension);

                bool isPushed = false;
                bool isResource = requestedName.Name.EndsWith(".resources", StringComparison.InvariantCulture);
                if (isResource)
                {
                    // Are we recursively looking up the same resource?  Note - our backout code will set
                    // the ResourceHelper's currentlyLoading stack to null if an exception occurs.
                    if (s_currentlyLoading != null && s_currentlyLoading.Count > 0 && s_currentlyLoading.LastIndexOf(assemblyPath) != -1)
                    {
                        SafeLog(
                            name,
                            () =>
                            {
                                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                                {
                                    PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTest.AssemblyResolver.OnResolve: Assembly '{0}' is searching for itself recursively '{1}', returning as not found.", name, assemblyPath);
                                }
                            });
                        _resolvedAssemblies[name] = null;
                        return null;
                    }

                    s_currentlyLoading ??= [];
                    s_currentlyLoading.Add(assemblyPath); // Push
                    isPushed = true;
                }

                Assembly? assembly = SearchAndLoadAssembly(assemblyPath, name, requestedName, isReflectionOnly);
                if (isResource && isPushed)
                {
                    DebugEx.Assert(s_currentlyLoading is not null, "_currentlyLoading should not be null");
                    s_currentlyLoading.RemoveAt(s_currentlyLoading.Count - 1); // Pop
                }

                if (assembly != null)
                {
                    return assembly;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Verifies that found assembly name matches requested to avoid security issues.
    /// Looks only at PublicKeyToken and Version, empty matches anything.
    /// </summary>
    /// <param name="requestedName"> The requested Name. </param>
    /// <param name="foundName"> The found Name. </param>
    /// <returns> The <see cref="bool"/>. </returns>
    private static bool RequestedAssemblyNameMatchesFound(AssemblyName requestedName, AssemblyName foundName)
    {
        DebugEx.Assert(requestedName != null, "requested assembly name should not be null.");
        DebugEx.Assert(foundName != null, "found assembly name should not be null.");

        byte[]? requestedPublicKey = requestedName.GetPublicKeyToken();
        if (requestedPublicKey != null)
        {
            byte[]? foundPublicKey = foundName.GetPublicKeyToken();
            if (foundPublicKey == null)
            {
                return false;
            }

            for (int i = 0; i < requestedPublicKey.Length; ++i)
            {
                if (requestedPublicKey[i] != foundPublicKey[i])
                {
                    return false;
                }
            }
        }

        return requestedName.Version == null || requestedName.Version.Equals(foundName.Version);
    }

    /// <summary>
    /// Search for assembly and if exists then load.
    /// </summary>
    /// <param name="assemblyPath"> The assembly Path. </param>
    /// <param name="assemblyName"> The assembly Name. </param>
    /// <param name="requestedName"> The requested Name. </param>
    /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
    /// <returns> The <see cref="Assembly"/>. </returns>
    [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFrom", Justification = "The assembly location is figured out from the configuration that the user passes in.")]
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    private Assembly? SearchAndLoadAssembly(string assemblyPath, string assemblyName, AssemblyName requestedName, bool isReflectionOnly)
    {
        try
        {
            if (!DoesFileExist(assemblyPath))
            {
                return null;
            }

            var foundName = AssemblyName.GetAssemblyName(assemblyPath);

            if (!RequestedAssemblyNameMatchesFound(requestedName, foundName))
            {
                return null; // File exists but version/public key is wrong. Try next extension.
            }

            Assembly assembly;

#if NETFRAMEWORK
            if (isReflectionOnly)
            {
                assembly = ReflectionOnlyLoadAssemblyFrom(assemblyPath);
                _reflectionOnlyResolvedAssemblies[assemblyName] = assembly;
            }
            else
#endif
            {
                assembly = LoadAssemblyFrom(assemblyPath);
                _resolvedAssemblies[assemblyName] = assembly;
            }

            SafeLog(
                assemblyName,
                () =>
                    {
                        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                        {
                            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTest.AssemblyResolver.OnResolve: Resolved assembly '{0}'", assemblyName);
                        }
                    });

            return assembly;
        }
        catch (FileLoadException ex)
        {
            SafeLog(
                assemblyName,
                () =>
                    {
                        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                        {
                            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason:{1} ", assemblyName, ex);
                        }
                    });

            // Re-throw FileLoadException, because this exception means that the assembly
            // was found, but could not be loaded. This will allow us to report a more
            // specific error message to the user for things like access denied.
            throw;
        }
        catch (Exception ex)
        {
            // For all other exceptions, try the next extension.
            SafeLog(
                assemblyName,
                () =>
                    {
                        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                        {
                            PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason:{1} ", assemblyName, ex);
                        }
                    });
        }

        return null;
    }
}
#endif
