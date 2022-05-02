// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// The test run directories.
    /// </summary>
    [Serializable]
    public class TestRunDirectories
    {
        /// <summary>
        /// The default deployment root directory. We do not want to localize it.
        /// </summary>
        internal const string DefaultDeploymentRootDirectory = "TestResults";

        /// <summary>
        /// The deployment in directory suffix.
        /// </summary>
        internal const string DeploymentInDirectorySuffix = "In";

        /// <summary>
        /// The deployment out directory suffix.
        /// </summary>
        internal const string DeploymentOutDirectorySuffix = "Out";

        public TestRunDirectories(string rootDirectory)
        {
            Debug.Assert(!string.IsNullOrEmpty(rootDirectory), "rootDirectory");

            this.RootDeploymentDirectory = rootDirectory;
        }

        /// <summary>
        /// Gets or sets the root deployment directory
        /// </summary>
        public string RootDeploymentDirectory { get; set; }

        /// <summary>
        /// Gets the In directory
        /// </summary>
        public string InDirectory
        {
            get
            {
                return Path.Combine(this.RootDeploymentDirectory, DeploymentInDirectorySuffix);
            }
        }

        /// <summary>
        /// Gets the Out directory
        /// </summary>
        public string OutDirectory
        {
            get
            {
                return Path.Combine(this.RootDeploymentDirectory, DeploymentOutDirectorySuffix);

                // Previous corelib impl was:
                // return Directory.GetCurrentDirectory();
            }
        }

        /// <summary>
        /// Gets In\MachineName directory
        /// </summary>
        public string InMachineNameDirectory
        {
            get
            {
                var machineName = GetMachineName();

                return Path.Combine(Path.Combine(this.RootDeploymentDirectory, DeploymentInDirectorySuffix), machineName);
            }
        }

        private static string _machineName = null;
        private static string GetMachineName()
        {
            if (_machineName != null)
            {
                return _machineName;
            }

#if NETSTANDARD1_5_OR_GREATER
            _machineName = Environment.MachineName;
            
#else
            // Try getting the machine name using the API anyway,
            var machineNameProperty = typeof(Environment).GetTypeInfo().DeclaredMembers.SingleOrDefault(p => p.Name == "MachineName") as PropertyInfo;
            if (machineNameProperty != null)
            {
                try
                {
                    _machineName = machineNameProperty.GetValue(null) as string;
                }
                catch
                {
                }
            }

            if (string.IsNullOrEmpty(_machineName))
            {
                _machineName = Environment.GetEnvironmentVariable("COMPUTERNAME")
                    ?? Environment.GetEnvironmentVariable("HOSTNAME")
                    ?? "localhost";
            }
#endif
            return _machineName;
        }
    }
}
