// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.AppContainer;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

    /// <summary>
    /// This platform service is responsible for any data or operations to validate
    /// the test sources provided to the adapter.
    /// </summary>
    public class TestSource : ITestSource
    {
        private const string SystemAssembliesPrefix = "system.";

        /// <summary>
        /// Gets the set of valid extensions for sources targeting this platform.
        /// </summary>
        public IEnumerable<string> ValidSourceExtensions
        {
            get
            {
                return new[] {
                    Constants.DllExtension,
                    Constants.ExeExtension,
                    Constants.PackageExtension
                };
            }
        }

        private static readonly HashSet<string> systemAssemblies = new HashSet<string>(new string[]
        {
            "MICROSOFT.CSHARP.DLL",
            "MICROSOFT.VISUALBASIC.DLL",
            "CLRCOMPRESSION.DLL",
        });

        // Well known platform assemblies.
        private static readonly HashSet<string> platformAssemblies = new HashSet<string>(new string[]
        {
            "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.TESTFRAMEWORK.DLL",
            "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.TESTFRAMEWORK.EXTENSIONS.CORE.DLL",
            "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.CORE.DLL",
            "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.COMMON.DLL",
            "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.TESTEXECUTOR.CORE.DLL",
            "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.EXTENSIONS.MSAPPCONTAINERADAPTER.DLL",
            "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.EXTENSIONS.MSPHONEADAPTER.DLL",
            "MICROSOFT.VISUALSTUDIO.TESTPLATFORM.OBJECTMODEL.DLL",
            "VSTEST_EXECUTIONENGINE_PLATFORMBRIDGE.DLL",
            "VSTEST_EXECUTIONENGINE_PLATFORMBRIDGE.WINMD",
            "VSTEST.EXECUTIONENGINE.WINDOWSPHONE.DLL",
        });

        /// <summary>
        /// Verifies if the assembly provided is referenced by the source.
        /// </summary>
        /// <param name="assemblyName"> The assembly name. </param>
        /// <param name="source"> The source. </param>
        /// <returns> True if the assembly is referenced. </returns>
        public bool IsAssemblyReferenced(AssemblyName assemblyName, string source)
        {
#if NETFRAMEWORK
            // This loads the dll in a different app domain.
            // If no reference to UTF don't run discovery. Take conservative approach. If not able to find proceed with discovery.
            var utfReference = AssemblyHelper.DoesReferencesAssembly(source, assemblyName) ?? true;
            if (!utfReference)
            {
                return false;
            }

            return true;
#elif NET5_0_OR_GREATER || NETSTANDARD1_5_OR_GREATER
            return AssemblyUtility.IsAssemblyReferenced(assemblyName, source);
#else
            // There is no way currently in dotnet core to determine referenced assemblies for a source.
            return true;
#endif
        }

        /// <summary>
        /// Gets the set of sources (dll's/exe's) that contain tests. If a source is a package(appx), return the file(dll/exe) that contains tests from it.
        /// </summary>
        /// <param name="sources"> Sources given to the adapter.  </param>
        /// <returns> Sources that contains tests. <see cref="IEnumerable{T}"/>. </returns>
        public IEnumerable<string> GetTestSources(IEnumerable<string> sources)
        {
            if (this.ContainsAppxSource(sources))
            {
                var newSources = new List<string>();

                var applicationPath = AppModel.GetCurrentPackagePath(PackagePathType.Install);
                var filePaths = Directory.GetFiles(applicationPath);
                foreach (var filePath in filePaths)
                {
                    var fileName = Path.GetFileName(filePath);
                    var isExtSupported =
                        ValidSourceExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

                    if (isExtSupported && !fileName.StartsWith(SystemAssembliesPrefix, StringComparison.OrdinalIgnoreCase)
                            && !platformAssemblies.Contains(fileName.ToUpperInvariant())
                            && !systemAssemblies.Contains(fileName.ToUpperInvariant()))
                    {
                        // WinUI Desktop uses .net 5, which builds both a .dll and an .exe.
                        // The manifest will provide the .exe, but the tests are inside the .dll,
                        // If we fail on exe - we might need to replace with dll on winui
                        // TODO @haplois CHECK IT
                        newSources.Add(filePath);
                    }
                }

                return newSources;
            }

            return sources;
        }

        /// <summary>
        /// Checks if given list of sources contains any ".appx" source.
        /// </summary>
        /// <param name="sources">The list of sources.</param>
        /// <returns>True if there is an appx source.</returns>
        private bool ContainsAppxSource(IEnumerable<string> sources)
        {
            foreach (string source in sources)
            {
                if (string.Compare(Path.GetExtension(source), Constants.PackageExtension, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
