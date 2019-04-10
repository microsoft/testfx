// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel
{
    /// <summary>
    /// TestMethod structure that is shared between adapter and platform services only.
    /// </summary>
    public interface ITestMethod
    {
        /// <summary>
        /// Gets the name of the test method
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the full class name of the test method
        /// </summary>
        string FullClassName { get; }

        /// <summary>
        /// Gets the declaring class full name.
        /// This will be used for resolving overloads and while getting navigation data.
        /// </summary>
        string DeclaringClassFullName { get; }

        /// <summary>
        /// Gets the name of the test assembly
        /// </summary>
        string AssemblyName { get; }

        /// <summary>
        /// Gets a value indicating whether test method is async
        /// </summary>
        bool IsAsync { get; }
    }
}
