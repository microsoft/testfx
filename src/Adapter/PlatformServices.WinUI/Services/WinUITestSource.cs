// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using global::System;
    using global::System.Collections.Generic;
    using global::System.IO;
    using global::System.Linq;
    using global::System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// This platform service is responsible for any data or operations to validate
    /// the test sources provided to the adapter.
    /// </summary>
    public class TestSource : ITestSource
    {
        private const string SystemAssembliesPrefix = "system.";

        private static IEnumerable<string> executableExtensions = new HashSet<string>()
        {
             Constants.ExeExtension
        };

        private static HashSet<string> systemAssemblies = new HashSet<string>(new string[]
        {
            "MICROSOFT.CSHARP.DLL",
            "MICROSOFT.VISUALBASIC.DLL",
            "CLRCOMPRESSION.DLL",
        });

        // Well known platform assemblies.
        private static HashSet<string> platformAssemblies = new HashSet<string>(new string[]
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
        /// Gets the set of valid extensions for sources targeting this platform.
        /// </summary>
        public IEnumerable<string> ValidSourceExtensions => new List<string> { Constants.DllExtension, Constants.ExeExtension, Constants.AppxPackageExtension };

        /// <summary>
        /// Verifies if the assembly provided is referenced by the source.
        /// </summary>
        /// <param name="assemblyName"> The assembly name. </param>
        /// <param name="source"> The source. </param>
        /// <returns> True if the assembly is referenced. </returns>
        public bool IsAssemblyReferenced(AssemblyName assemblyName, string source)
        {
            // This code will get hit when Discovery happens during Run Tests.
            // Since Discovery during Discover Tests would have validated the presence of Unit Test Framework as reference,
            // no need to do validation again.
            // Simply return true.
            return true;
        }

        /// <summary>
        /// Gets the set of sources (dll's/exe's) that contain tests. If a source is a package(appx), return the file(dll/exe) that contains tests from it.
        /// </summary>
        /// <param name="sources"> Sources given to the adapter.  </param>
        /// <returns> Sources that contains tests. <see cref="IEnumerable{T}"/>. </returns>
        public IEnumerable<string> GetTestSources(IEnumerable<string> sources)
        {
            string appxSource;
            if ((appxSource = this.FindAppxSource(sources)) != null)
            {
                var appxSourceDirectory = Path.GetDirectoryName(appxSource);

                List<string> newSources = new List<string>();

                var fileSearchTask = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFilesAsync().AsTask();
                fileSearchTask.Wait();
                foreach (var filePath in fileSearchTask.Result)
                {
                    var fileName = filePath.Name;
                    var isExtSupported =
                        executableExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

                    if (isExtSupported && !fileName.StartsWith(SystemAssembliesPrefix, StringComparison.OrdinalIgnoreCase)
                            && !platformAssemblies.Contains(fileName.ToUpperInvariant())
                            && !systemAssemblies.Contains(fileName.ToUpperInvariant()))
                    {
                        // WinUI Desktop uses .net 5, which builds both a .dll and an .exe.
                        // The manifest will provide the .exe, but the tests are inside the .dll, so we replace the name here.
                        newSources.Add(Path.Combine(appxSourceDirectory, Path.ChangeExtension(fileName, Constants.DllExtension)));
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
        private string FindAppxSource(IEnumerable<string> sources)
        {
            foreach (string source in sources)
            {
                if (string.Compare(Path.GetExtension(source), Constants.AppxPackageExtension, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return source;
                }
            }

            return null;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
