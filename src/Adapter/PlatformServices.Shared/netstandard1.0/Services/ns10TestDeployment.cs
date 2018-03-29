// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// The test deployment.
    /// </summary>
    public class TestDeployment : ITestDeployment
    {
        /// <summary>
        /// Deploy deployment items for the specified test cases.
        /// </summary>
        /// <param name="testCases"> The test cases. </param>
        /// <param name="runContext"> The run context. </param>
        /// <param name="frameworkHandle"> The framework handle. </param>
        /// <returns> True if deployment is done. </returns>
        public bool Deploy(IEnumerable<TestCase> testCases, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            return false;
        }

        /// <summary>
        /// Gets the set of deployment items on a method and its corresponding class.
        /// </summary>
        /// <param name="method"> The method. </param>
        /// <param name="type"> The type. </param>
        /// <param name="warnings"> The warnings. </param>
        /// <returns> The <see cref="KeyValuePair{TKey,TValue}"/> of deployment items. </returns>
        public KeyValuePair<string, string>[] GetDeploymentItems(MethodInfo method, Type type, ICollection<string> warnings)
        {
            return null;
        }

        /// <summary>
        /// Cleanup deployment item directories.
        /// </summary>
        public void Cleanup()
        {
        }

        /// <summary>
        /// Gets the deployment output directory where the source file along with all its dependencies is dropped.
        /// </summary>
        /// <returns> The deployment output directory. </returns>
        public string GetDeploymentDirectory()
        {
            return null;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
