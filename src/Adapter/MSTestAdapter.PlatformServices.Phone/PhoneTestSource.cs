// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// This platform service is responsible for any data or operations to validate
    /// the test sources provided to the adapter.
    /// </summary>
    public class TestSource : ITestSource
    {
        /// <summary>
        /// Returns the set of valid extensions for sources targetting this platform.
        /// </summary>
        public IEnumerable<string> ValidSourceExtensions
        {
            get
            {
                return new List<string> { Constants.PhoneXapPackageExtension, Constants.PhoneAppxPackageExtension };
            }
        }

        /// <summary>
        /// Verifies if the assembly provided is referenced by the source.
        /// </summary>
        /// <param name="assemblyName"> The assembly name. </param>
        /// <param name="source"> The source. </param>
        /// <returns> True if the assembly is referenced. </returns>
        public bool IsAssemblyReferenced(AssemblyName assemblyName, string source)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Gets the set of sources (dll's/exe's) that contain tests. If a source is a package(appx), return the file(dll/exe) that contains tests from it.
        /// </summary>
        /// <param name="sources"> Sources given to the adapter.  </param>
        /// <returns> Sources that contains tests. <see cref="IEnumerable"/>. </returns>
        public IEnumerable<string> GetTestSources(IEnumerable<string> sources)
        {
            return sources;
        }
    }
}
