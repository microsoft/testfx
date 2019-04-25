// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel
{
    using System;
    using System.Diagnostics;

    using MSTestAdapter.PlatformServices.Interface.ObjectModel;

    /// <summary>
    /// TestMethod contains information about a unit test method that needs to be executed
    /// </summary>
    [Serializable]
    public sealed class TestMethod : ITestMethod
    {
        #region Fields

        /// <summary>
        /// Member field for the property 'DeclaringClassFullName'
        /// </summary>
        private string declaringClassFullName = null;

        /// <summary>
        /// Member field for the property 'DeclaringAssemblyName'
        /// </summary>
        private string declaringAssemblyName = null;

        #endregion

        public TestMethod(string name, string fullClassName, string assemblyName,  bool isAsync)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }

            Debug.Assert(!string.IsNullOrEmpty(name), "TestName cannot be empty");
            Debug.Assert(!string.IsNullOrEmpty(fullClassName), "Full className cannot be empty");

            this.Name = name;
            this.FullClassName = fullClassName;
            this.AssemblyName = assemblyName;
            this.IsAsync = isAsync;
        }

        /// <summary>
        /// Gets the name of the test method
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the full classname of the test method
        /// </summary>
        public string FullClassName { get; private set; }

        /// <summary>
        /// Gets or sets the declaring class full name. This will be used while getting navigation data.
        /// This will be null if AssemblyName is same as DeclaringAssemblyName.
        /// Reason to set to null in the above case is to minimise the transfer of data across appdomains and not have a perf hit.
        /// </summary>
        public string DeclaringAssemblyName
        {
            get
            {
                return this.declaringAssemblyName;
            }

            set
            {
                Debug.Assert(value != this.AssemblyName, "DeclaringAssemblyName should not be the same as AssemblyName.");
                this.declaringAssemblyName = value;
            }
        }

        /// <summary>
        /// Gets or sets the declaring class full name.
        /// This will be used to resolve overloads and while getting navigation data.
        /// This will be null if FullClassName is same as DeclaringClassFullName.
        /// Reason to set to null in the above case is to minimise the transfer of data across appdomains and not have a perf hit.
        /// </summary>
        public string DeclaringClassFullName
        {
            get
            {
                return this.declaringClassFullName;
            }

            set
            {
                Debug.Assert(value != this.FullClassName, "DeclaringClassFullName should not be the same as FullClassName.");
                this.declaringClassFullName = value;
            }
        }

        /// <summary>
        /// Gets the name of the test assembly
        /// </summary>
        public string AssemblyName { get; private set; }

        /// <summary>
        /// Gets a value indicating whether specifies test method is async
        /// </summary>
        public bool IsAsync { get; private set; }
    }
}
