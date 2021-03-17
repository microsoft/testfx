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
        /// Gets the declaring class full name. This will be used while getting navigation data.
        /// This will be null if AssemblyName is same as DeclaringAssemblyName.
        /// Reason to set to null in the above case is to minimize the transfer of data across appdomains and not have a performance hit.
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

        /// <summary>
        /// Gets the fully specified type name metadata format.
        /// </summary>
        /// <example>
        ///     <code>NamespaceA.NamespaceB.ClassName`1+InnerClass`2</code>
        /// </example>
        string ManagedTypeName { get; }

        /// <summary>
        /// Gets the fully specified method name metadata format.
        /// </summary>
        /// <example>
        ///     <code>MethodName`2(ParamTypeA,ParamTypeB,...)</code>
        /// </example>
        string ManagedMethodName { get; }

        /// <summary>
        /// Gets a value indicating whether both <see cref="ManagedTypeName"/> and <see cref="ManagedMethodName"/> are not null or whitespace.
        /// </summary>
        bool HasManagedMethodAndTypeProperties { get; }
    }
}
