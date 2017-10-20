// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// This platform service is responsible for any data or operations to validate
    /// the test sources provided to the adapter.
    /// </summary>
    public class TestSource : ITestSource
    {
        /// <summary>
        /// Gets the set of valid extensions for sources targeting this platform.
        /// </summary>
        public IEnumerable<string> ValidSourceExtensions
        {
            get
            {
                // Since desktop Platform service would also discover other platform tests on dekstop,
                // this extension list needs to be updated with all platforms supported file extensions.
                return new List<string>
                           {
                               Constants.DllExtension,
                               Constants.PhoneAppxPackageExtension,
                               Constants.ExeExtension
                           };
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
            // This loads the dll in a different app domain. We can optimize this to load in the current domain since this code ould be run in a new app domain anyway.
            bool? utfReference = AssemblyHelper.DoesReferencesAssembly(source, assemblyName);

            // If no reference to UTF don't run discovery. Take conservative approach. If not able to find proceed with discovery.
            if (utfReference.HasValue && utfReference.Value == false)
            {
                return false;
            }

            return true;
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

        /// <inheritdoc />
        public int GetParallelizationLevel(string source)
        {
            var inAssemblyParallelizationLevel = -1;

            var customAttribute = this.GetCustomAttributeFromAssembly(source, typeof(TestParallelizationLevelAttribute));

            var parallelizationLevelAttribute = (TestParallelizationLevelAttribute)customAttribute;
            if (parallelizationLevelAttribute != null)
            {
                inAssemblyParallelizationLevel = parallelizationLevelAttribute.ParallelizationLevel;
            }

            if (inAssemblyParallelizationLevel == 0)
            {
                inAssemblyParallelizationLevel = Environment.ProcessorCount;
            }

            return inAssemblyParallelizationLevel;
        }

        /// <inheritdoc />
        public TestParallelizationMode GetParallelizationMode(string source)
        {
            // TODO we should retrieve all assembly level properties for the execution at once
            var inAssemblyParallelizationMode = TestParallelizationMode.ClassLevel;
            var customAttribute = this.GetCustomAttributeFromAssembly(source, typeof(TestParallelizationModeAttribute));
            var parallelizationModeAttribute = (TestParallelizationModeAttribute)customAttribute;

            if (parallelizationModeAttribute != null)
            {
                inAssemblyParallelizationMode = parallelizationModeAttribute.TestParallelizationMode;
            }

            return inAssemblyParallelizationMode;
        }

        private object GetCustomAttributeFromAssembly(string source, Type type)
        {
            var asm = Assembly.LoadFrom(source);

            var customAttribute = asm.GetCustomAttribute(type);

            return customAttribute;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
