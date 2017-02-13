// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Security.Permissions;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using System.Runtime.InteropServices.WindowsRuntime;

    /// <summary>
    /// Helps resolve MSTestFramework assemblies for CLR loader.
    /// The idea is that Unit Test Adapter creates App Domain for running tests and sets AppBase to tests dir.
    /// Since we don't want to put our assemblies to GAC and they are not in tests dir, we use custom way to resolve them.
    /// </summary>
    public class AssemblyResolver : MarshalByRefObject, IDisposable
    {
        /// <summary>
        /// The assembly name of the dll containing logger APIs(EqtTrace) from the TestPlatform.
        /// </summary>
        /// <remarks>
        /// The reason we have this is because the AssemblyResolver itself logs information during resolution. 
        /// If the resolver is called for the assembly containing the logger APIs, we do not log so as to prevent a stack overflow.
        /// </remarks>
        private const string LoggerAssemblyName = "Microsoft.VisualStudio.TestPlatform.ObjectModel";
        
        /// <summary>
        /// Constructor which takes a list of directories for resolution path
        /// If you have some more path where you want to search recursively
        /// call AddSearchDirectoryFromRunSetting method with that list
        /// </summary>
        /// <param name="directories">
        /// The directories. 
        /// </param>
        public AssemblyResolver(IList<string> directories)
        {
            if (directories == null || directories.Count == 0)
            {
                throw new ArgumentNullException("directories");
            }

            this.searchDirectories = new List<string>(directories);
            this.directoryList = new Queue<RecursiveDirectoryPath>();

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(this.OnResolve);
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += new ResolveEventHandler(this.ReflectionOnlyOnResolve);

            // This is required for winmd resolution for arm built sources discovery on desktop.
            WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve += new EventHandler<NamespaceResolveEventArgs>(this.WindowsRuntimeMetadataReflectionOnlyNamespaceResolve);
        }

        /// <summary>
        /// It will add a list of search directories path with property recursive/non-recursive in assembly resolver .
        /// </summary>
        /// <param name="recursiveDirectoryPath">
        /// The recursive Directory Path. 
        /// </param>
        public void AddSearchDirectoriesFromRunSetting(List<RecursiveDirectoryPath> recursiveDirectoryPath)
        {
            // Enqueue elements from the list in Queue
            if (recursiveDirectoryPath == null)
            {
                return;
            }

            foreach (var recPath in recursiveDirectoryPath)
            {
                this.directoryList.Enqueue(recPath);
            }
        }
        
        /// <summary>
        /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
        /// </summary>
        /// <param name="sender"> The sender App Domain. </param>
        /// <param name="args"> The args. </param>
        /// <returns> The <see cref="Assembly"/>. </returns>
        internal Assembly ReflectionOnlyOnResolve(object sender, ResolveEventArgs args)
        {
            return this.OnResolveInternal(sender, args, true);
        }

        /// <summary>
        /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
        /// </summary>
        /// <param name="sender"> The sender App Domain. </param>
        /// <param name="args"> The args. </param>
        /// <returns> The <see cref="Assembly"/>.  </returns>
        internal Assembly OnResolve(object sender, ResolveEventArgs args)
        {
            return this.OnResolveInternal(sender, args, false);
        }

        /// <summary>
        /// Event handler for windows winmd resolution.
        /// </summary>
        /// <param name="sender"> The sender App Domain. </param>
        /// <param name="args"> The args. </param>
        private void WindowsRuntimeMetadataReflectionOnlyNamespaceResolve(object sender, NamespaceResolveEventArgs args)
        {
            // Note: This will throw on pre-Win8 OS versions
            IEnumerable<string> fileNames = WindowsRuntimeMetadata.ResolveNamespace(
                args.NamespaceName,
                null,   // Will use OS installed .winmd files, you can pass explicit Windows SDK path here for searching 1st party WinRT types
                this.searchDirectories);  // You can pass package graph paths, they will be used for searching .winmd files with 3rd party WinRT types

            foreach (string fileName in fileNames)
            {
                args.ResolvedAssemblies.Add(Assembly.ReflectionOnlyLoadFrom(fileName));
            }
        }

        /// <summary>
        /// Assembly Resolve event handler for App Domain - called when CLR loader cannot resolve assembly.
        /// </summary>
        /// <param name="senderAppDomain"> The sender App Domain. </param>
        /// <param name="args"> The args. </param>
        /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
        /// <returns> The <see cref="Assembly"/>.  </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters",
            MessageId = "senderAppDomain")]
        private Assembly OnResolveInternal(object senderAppDomain, ResolveEventArgs args, bool isReflectionOnly)
        {
            if (string.IsNullOrEmpty(args?.Name))
            {
                Debug.Assert(false, "AssemblyResolver.OnResolve: args.Name is null or empty.");
                return null;
            }
            
            this.SafeLog(
                args.Name,
                () =>
                {
                    if (EqtTrace.IsInfoEnabled)
                    {
                        EqtTrace.Info("AssemblyResolver: Resolving assembly: {0}.", args.Name);
                    }
                });

            string assemblyNameToLoad = AppDomain.CurrentDomain.ApplyPolicy(args.Name);

            this.SafeLog(
                assemblyNameToLoad,
                () =>
                    {
                        if (EqtTrace.IsInfoEnabled)
                        {
                            EqtTrace.Info("AssemblyResolver: Resolving assembly after applying policy: {0}.", assemblyNameToLoad);
                        }
                    });

            lock (this.resolvedAssemblies)
            {
                // Since both normal and reflection only cache are accessed in same block, putting only one lock should be sufficient.
                Assembly assembly = null;

                if (this.TryLoadFromCache(assemblyNameToLoad, out assembly))
                {
                    return assembly;
                }

                assembly = this.SearchAssembly(this.searchDirectories, assemblyNameToLoad, isReflectionOnly);

                if (assembly != null)
                {
                    return assembly;
                }

                if (this.directoryList != null && this.directoryList.Any())
                {
                    // required assembly is not present in searchDirectories??
                    // see, if we can find it in user specified search directories.
                    while (assembly == null && this.directoryList.Count > 0)
                    {
                        // instead of loading whole saerch directory in one time, we are adding directory on the basis of need
                        var currentNode = this.directoryList.Dequeue();

                        List<string> increamentalSearchDirectory = new List<String>();

                        if (this.DoesDirectoryExist(currentNode.DirectoryPath))
                        {
                            increamentalSearchDirectory.Add(currentNode.DirectoryPath);

                            if (currentNode.IncludeSubDirectories)
                            {
                                // Add all its sub-directory in depth first search order.
                                this.AddSubdirectories(currentNode.DirectoryPath, increamentalSearchDirectory);
                            }

                            // Add this directory list in this.searchDirectories so that when we will try to resolve some other 
                            // assembly, then it will look in this whole directory first.
                            this.searchDirectories.AddRange(increamentalSearchDirectory);

                            assembly = this.SearchAssembly(increamentalSearchDirectory, assemblyNameToLoad, isReflectionOnly);
                        }
                        else
                        {
                            // generate warning that path doesnot exist.
                            this.SafeLog(
                                assemblyNameToLoad,
                                () =>
                                {
                                    if (EqtTrace.IsWarningEnabled)
                                    {
                                        EqtTrace.Warning(
                                        "The Directory: {0}, does not exist",
                                        currentNode.DirectoryPath);
                                    }
                                });
                        }
                    }

                    if (assembly != null)
                    {
                        return assembly;
                    }
                }

                // Try for default load for System dlls that can't be found in search paths. Needs to loaded just by name.
                try
                {
                    // Put it in the resolved assembly cache so that if the Load call below
                    // triggers another assembly resolution, then we dont end up in stack overflow.
                    this.resolvedAssemblies[assemblyNameToLoad] = null;

                    if (isReflectionOnly)
                    {
                        assembly = Assembly.ReflectionOnlyLoad(assemblyNameToLoad);
                    }
                    else
                    {
                        assembly = Assembly.Load(assemblyNameToLoad);
                    }

                    if (assembly != null)
                    {
                        this.resolvedAssemblies[assemblyNameToLoad] = assembly;
                        return assembly;
                    }
                }
                catch (Exception ex)
                {
                    this.SafeLog(
                                args?.Name,
                                () =>
                                {
                                    if (EqtTrace.IsInfoEnabled)
                                    {
                                        EqtTrace.Info(
                                            "AssemblyResolver: {0}: Failed to load assembly. Reason:{1} ",
                                            assemblyNameToLoad,
                                            ex);
                                    }
                                });
                }
                
                return assembly;
            }
        }

        /// <summary>
        /// Call logger APIs safely. We do not want a stackoverflow when objectmodel assembly itself
        /// is being resolved and an EqtTrace message prompts the load of the same dll again.
        /// CLR does not trigger a load when the EqtTrace messages are in a lamda expression. Leaving it that way
        /// to preserve readability instead of creating wrapper functions.
        /// </summary>
        /// <param name="assemblyName">The assembly being rsolved.</param>
        /// <param name="loggerAction">The logger function.</param>
        private void SafeLog(string assemblyName, Action loggerAction)
        {
            if (!string.IsNullOrEmpty(assemblyName) && !assemblyName.StartsWith(LoggerAssemblyName))
            {
                loggerAction.Invoke();
            }
        }

        /// <summary>
        /// Adds the subdirectories of the provided path to the collection.
        /// </summary>
        /// <param name="path"> Path go get subdirectories for. </param>
        /// <param name="searchDirectories"> The search Directories. </param>
        internal void AddSubdirectories(string path, List<string> searchDirectories)
        {
            Debug.Assert(!string.IsNullOrEmpty(path), "'path' cannot be null or empty.");
            Debug.Assert(searchDirectories != null, "'searchDirectories' cannot be null.");

            // If the directory exists, get it's subdirectories
            if (this.DoesDirectoryExist(path))
            {
                // Get the directories in the path provided.
                var directories = this.GetDirectories(path);

                // Add each directory and its subdirectories to the collection.
                foreach (var directory in directories)
                {
                    searchDirectories.Add(directory);

                    this.AddSubdirectories(directory, searchDirectories);
                }
            }
        }
        
        /// <summary>
        /// Verifies if a directory exists.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <remarks>Only present for unit testing scenarios.</remarks>
        protected virtual bool DoesDirectoryExist(string path)
        {
            return Directory.Exists(path);
        }

        /// <summary>
        /// Gets the directories from a path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <remarks>Only present for unit testing scenarios.</remarks>
        protected virtual string[] GetDirectories(string path)
        {
            return Directory.GetDirectories(path);
        }

        /// <summary>
        /// It will search for a particular assembly in the given list of directory.
        /// </summary>
        /// <param name="searchDirectorypaths"> The search Directorypaths. </param>
        /// <param name="name"> The name. </param>
        /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
        /// <returns> The <see cref="Assembly"/>. </returns>
        protected virtual Assembly SearchAssembly(List<string> searchDirectorypaths, string name, bool isReflectionOnly)
        {
            if (searchDirectorypaths == null || searchDirectorypaths.Count == 0)
            {
                return null;
            }

            // args.Name is like: "Microsoft.VisualStudio.TestTools.Common, Version=[VersionMajor].0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a".
            AssemblyName requestedName = null;

            try
            {
                // Can throw ArgumentException, FileLoadException if arg is empty/wrong format, etc. Should not return null.
                requestedName = new AssemblyName(name);
            }
            catch (Exception ex)
            {
                this.SafeLog(
                    name,
                    () =>
                        {
                            if (EqtTrace.IsInfoEnabled)
                            {
                                EqtTrace.Info(
                                    "AssemblyResolver: {0}: Failed to create assemblyName. Reason:{1} ",
                                    name,
                                    ex);
                            }
                        });

                return null;
            }

            Debug.Assert(requestedName != null && !string.IsNullOrEmpty(requestedName.Name), "AssemblyResolver.OnResolve: requested is null or name is empty!");

            foreach (var dir in searchDirectorypaths)
            {
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }

                this.SafeLog(
                    name,
                    () =>
                        {
                            if (EqtTrace.IsVerboseEnabled)
                            {
                                EqtTrace.Verbose("AssemblyResolver: Searching assembly: {0} in the directory: {1}", requestedName.Name, dir);
                            }
                        });

                foreach (var extension in new string[] { ".dll", ".exe" })
                {
                    var assemblyPath = Path.Combine(dir, requestedName.Name + extension);

                    var assembly = this.SearchAndLoadAssembly(assemblyPath, name, requestedName, isReflectionOnly);
                    if (assembly != null)
                    {
                        return assembly;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Search for assembly and if exists then load.
        /// </summary>
        /// <param name="assemblyPath"> The assembly Path. </param>
        /// <param name="assemblyName"> The assembly Name. </param>
        /// <param name="requestedName"> The requested Name. </param>
        /// <param name="isReflectionOnly"> Indicates whether this is called under a Reflection Only Load context. </param>
        /// <returns> The <see cref="Assembly"/>. </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability",
            "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFrom")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private Assembly SearchAndLoadAssembly(string assemblyPath, string assemblyName, AssemblyName requestedName, bool isReflectionOnly)
        {
            try
            {
                if (!File.Exists(assemblyPath))
                {
                    return null;
                }

                var foundName = AssemblyName.GetAssemblyName(assemblyPath);

                if (!RequestedAssemblyNameMatchesFound(requestedName, foundName))
                {
                    return null; // File exists but version/public key is wrong. Try next extension.
                }
                Assembly assembly;

                if (isReflectionOnly)
                {
                    assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                }
                else
                {
                    assembly = Assembly.LoadFrom(assemblyPath);
                }
                
                this.resolvedAssemblies[assemblyName] = assembly;

                this.SafeLog(
                    assemblyName,
                    () =>
                        {
                            if (EqtTrace.IsInfoEnabled)
                            {
                                EqtTrace.Info("AssemblyResolver: Resolved assembly: {0}. ", assemblyName);
                            }
                        });
                return assembly;
            }
            catch (FileLoadException ex)
            {
                this.SafeLog(
                    assemblyName,
                    () =>
                        {
                            if (EqtTrace.IsInfoEnabled)
                            {
                                EqtTrace.Info("AssemblyResolver: Failed to load assembly: {0}. Reason:{1} ", assemblyName, ex);
                            }
                        });
                // Rethrow FileLoadException, because this exception means that the assembly
                // was found, but could not be loaded. This will allow us to report a more
                // specific error message to the user for things like access denied.
                throw;
            }
            catch (Exception ex)
            {
                // For all other exceptions, try the next extension.
                this.SafeLog(
                    assemblyName,
                    () =>
                        {
                            if (EqtTrace.IsInfoEnabled)
                            {
                                EqtTrace.Info("AssemblyResolver: Failed to load assembly: {0}. Reason:{1} ", assemblyName, ex);
                            }
                        });
            }

            return null;
        }

        /// <summary>
        /// Load assembly from cache if available.
        /// </summary>
        /// <param name="assemblyName"> The assembly Name. </param>
        /// <param name="assembly"> The assembly. </param>
        /// <returns> The <see cref="bool"/>. </returns>
        private bool TryLoadFromCache(string assemblyName, out Assembly assembly)
        {
            if (this.resolvedAssemblies.TryGetValue(assemblyName, out assembly))
            {
                this.SafeLog(
                    assemblyName,
                    () =>
                        {
                            if (EqtTrace.IsInfoEnabled)
                            {
                                EqtTrace.Info("AssemblyResolver: Resolved: {0}.", assemblyName);
                            }
                        });
                return true;
            }

            return false;
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
            Debug.Assert(requestedName != null);
            Debug.Assert(foundName != null);

            var requestedPublicKey = requestedName.GetPublicKeyToken();
            if (requestedPublicKey != null)
            {
                var foundPublicKey = foundName.GetPublicKeyToken();
                if (foundPublicKey == null)
                {
                    return false;
                }

                for (var i = 0; i < requestedPublicKey.Length; ++i)
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
        /// Finalizes an instance of the <see cref="AssemblyResolver"/> class. 
        /// </summary>
        ~AssemblyResolver()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// The dispose.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The dispose.
        /// </summary>
        /// <param name="disposing">
        /// The disposing.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    // cleanup Managed resourceslike calling dispose on other managed object created.
                    AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(this.OnResolve);
                    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= new ResolveEventHandler(this.ReflectionOnlyOnResolve);

                    WindowsRuntimeMetadata.ReflectionOnlyNamespaceResolve -= new EventHandler<NamespaceResolveEventArgs>(this.WindowsRuntimeMetadataReflectionOnlyNamespaceResolve);
                }

                // cleanup native resources
                this.disposed = true;
            }
        }

        /// <summary>
        /// Returns object to be used for conrtolling lifetime, null means infinite lifetime.
        /// </summary>
        /// <remarks>
        /// Note that LinkDemand is needed by FxCop.
        /// </remarks>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        [SecurityCritical]
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        /// This will have the list of all directories read from runsettings.
        /// </summary>
        private Queue<RecursiveDirectoryPath> directoryList;

        /// <summary>
        /// The directories to look for assemblies to resolve.
        /// </summary>
        private List<string> searchDirectories;

        /// <summary>
        /// Dictionary of Assemblies discovered to date. Must be locked as it may
        /// be accessed in a multi-threaded context.
        /// </summary>
        private Dictionary<string, Assembly> resolvedAssemblies = new Dictionary<string, Assembly>();


        private bool disposed;
    }
}
